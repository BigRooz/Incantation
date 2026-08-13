using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace Incantation.Networking
{
    /// <summary>
    /// Relays compact owner-authored look pitch/yaw and applies it only to remote character
    /// presentation. This component has no ritual or gameplay authority.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkPlayer))]
    [RequireComponent(typeof(NetworkCharacterPresentation))]
    public sealed class NetworkCharacterLookPose : NetworkBehaviour
    {
        [SerializeField, Min(1f)] private float sendRate = 15f;
        [SerializeField, Min(0f)] private float changeThreshold = 0.25f;
        [SerializeField, Min(0.1f)] private float heartbeatInterval = 0.75f;
        [SerializeField, Min(0f)] private float maxPitch = 60f;
        [SerializeField, Min(0f)] private float maxYaw = 45f;

        private NetworkCharacterPresentation characterPresentation;
        private PlayerMovement boundPlayerMovement;
        private float targetPitch;
        private float targetYaw;
        private float lastSubmittedPitch;
        private float lastSubmittedYaw;
        private float serverPitch;
        private float serverYaw;
        private float nextSendTime;
        private float nextHeartbeatTime;
        private uint localSequence;
        private uint serverSequence;
        private uint observerSequence;
        private bool hasSubmittedPose;
        private bool hasServerPose;
        private bool hasObserverPose;
        private bool applyObserverPoseImmediately;

        private void Awake()
        {
            characterPresentation = GetComponent<NetworkCharacterPresentation>();
        }

        private void OnEnable()
        {
            if (characterPresentation != null)
            {
                characterPresentation.CharacterInstanceChanged += HandleCharacterInstanceChanged;
                BindCharacter(characterPresentation.CharacterInstance);
            }
        }

        private void Start()
        {
            BindCharacter(characterPresentation != null
                ? characterPresentation.CharacterInstance
                : null);
        }

        private void OnDisable()
        {
            if (characterPresentation != null)
                characterPresentation.CharacterInstanceChanged -= HandleCharacterInstanceChanged;

            boundPlayerMovement = null;
        }

        private void Update()
        {
            if (!IsOwner || !IsClientInitialized || boundPlayerMovement == null ||
                !boundPlayerMovement.isActiveAndEnabled)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (now < nextSendTime)
                return;

            float pitch = boundPlayerMovement.CurrentLookPitch;
            float yaw = boundPlayerMovement.CurrentLookYaw;
            if (!IsFinite(pitch) || !IsFinite(yaw))
                return;

            bool changed = !hasSubmittedPose ||
                Mathf.Abs(pitch - lastSubmittedPitch) >= changeThreshold ||
                Mathf.Abs(yaw - lastSubmittedYaw) >= changeThreshold;
            if (!changed && now < nextHeartbeatTime)
                return;

            localSequence++;
            lastSubmittedPitch = pitch;
            lastSubmittedYaw = yaw;
            hasSubmittedPose = true;
            nextSendTime = now + 1f / Mathf.Max(1f, sendRate);
            nextHeartbeatTime = now + Mathf.Max(0.1f, heartbeatInterval);
            SubmitLookPoseServerRpc(localSequence, pitch, yaw, false, Channel.Unreliable);
        }

        private void LateUpdate()
        {
            if (IsOwner || !hasObserverPose || boundPlayerMovement == null)
                return;

            // The remote input component remains disabled; only its input-free shared visual
            // formula is invoked so local and remote rigs use identical procedural weighting.
            boundPlayerMovement.ApplyLookPose(targetPitch, targetYaw, Time.deltaTime);
        }

        public bool ResetPoseForLobby()
        {
            if (!IsOwner || !IsClientInitialized)
                return false;

            if (boundPlayerMovement == null && characterPresentation != null)
                BindCharacter(characterPresentation.CharacterInstance);

            if (boundPlayerMovement == null)
                return false;

            boundPlayerMovement.ResetLookPose(true);
            localSequence++;
            lastSubmittedPitch = 0f;
            lastSubmittedYaw = 0f;
            hasSubmittedPose = true;
            nextSendTime = Time.unscaledTime + 1f / Mathf.Max(1f, sendRate);
            nextHeartbeatTime = Time.unscaledTime + Mathf.Max(0.1f, heartbeatInterval);
            SubmitLookPoseServerRpc(localSequence, 0f, 0f, true, Channel.Reliable);
            return true;
        }

        /// <summary>
        /// Settles the locally owned visual for result UI without publishing the reliable,
        /// buffered lifecycle reset reserved for Return to Lobby.
        /// </summary>
        public bool NeutralizeLocalPoseForGameOver()
        {
            if (!IsOwner)
                return false;

            if (boundPlayerMovement == null && characterPresentation != null)
                BindCharacter(characterPresentation.CharacterInstance);

            if (boundPlayerMovement == null)
                return false;

            boundPlayerMovement.ResetLookPose(true);
            return true;
        }

        [ServerRpc(RequireOwnership = true)]
        private void SubmitLookPoseServerRpc(
            uint sequence,
            float pitch,
            float yaw,
            bool immediate,
            Channel channel = Channel.Unreliable)
        {
            if (!IsFinite(pitch) || !IsFinite(yaw) ||
                (hasServerPose && !IsNewerSequence(sequence, serverSequence)))
            {
                return;
            }

            serverSequence = sequence;
            hasServerPose = true;
            serverPitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);
            serverYaw = Mathf.Clamp(yaw, -maxYaw, maxYaw);
            PublishLookPoseObserversRpc(sequence, serverPitch, serverYaw, immediate, channel);
        }

        [ObserversRpc(ExcludeOwner = true, BufferLast = true)]
        private void PublishLookPoseObserversRpc(
            uint sequence,
            float pitch,
            float yaw,
            bool immediate,
            Channel channel = Channel.Unreliable)
        {
            if (IsOwner || !IsFinite(pitch) || !IsFinite(yaw) ||
                (hasObserverPose && !IsNewerSequence(sequence, observerSequence)))
            {
                return;
            }

            observerSequence = sequence;
            hasObserverPose = true;
            targetPitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);
            targetYaw = Mathf.Clamp(yaw, -maxYaw, maxYaw);
            applyObserverPoseImmediately = immediate;

            if (applyObserverPoseImmediately && boundPlayerMovement != null)
                boundPlayerMovement.ResetLookPose(true);
        }

        private void HandleCharacterInstanceChanged(GameObject characterInstance)
        {
            BindCharacter(characterInstance);
        }

        private void BindCharacter(GameObject characterInstance)
        {
            boundPlayerMovement = characterInstance != null
                ? characterInstance.GetComponentInChildren<PlayerMovement>(true)
                : null;
            if (boundPlayerMovement == null)
                return;

            boundPlayerMovement.EnsureLookPoseInitialized();
            if (!IsOwner)
            {
                boundPlayerMovement.enabled = false;
                if (applyObserverPoseImmediately)
                    boundPlayerMovement.ResetLookPose(true);
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsNewerSequence(uint candidate, uint current)
        {
            return unchecked((int)(candidate - current)) > 0;
        }
    }
}
