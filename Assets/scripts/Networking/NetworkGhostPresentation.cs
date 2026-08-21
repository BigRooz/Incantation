using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using Incantation.Networking.Ritual;
using UnityEngine;

namespace Incantation.Networking
{
    /// <summary>
    /// Presents one observed NetworkPlayer as a Ghost after authoritative elimination.
    /// The owner keeps immediate local movement; this component only relays position and yaw.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkPlayer))]
    [RequireComponent(typeof(NetworkCharacterPresentation))]
    public sealed class NetworkGhostPresentation : NetworkBehaviour
    {
        [Header("Presentation")]
        [SerializeField] private GameObject ghostVisualPrefab;
        [SerializeField, Min(0f)] private float remoteInterpolationSpeed = 18f;

        [Header("Pose Transport")]
        [SerializeField, Min(1f)] private float sendRate = 15f;
        [SerializeField, Min(0f)] private float positionChangeThreshold = 0.01f;
        [SerializeField, Min(0f)] private float yawChangeThreshold = 0.25f;
        [SerializeField, Min(0.1f)] private float heartbeatInterval = 0.75f;
        [SerializeField, Min(1f)] private float maximumWorldMagnitude = 1000f;

        private readonly SyncVar<bool> presentationActive = new(false);
        private readonly SyncVar<Vector3> activationPosition = new(Vector3.zero);
        private readonly SyncVar<float> activationYaw = new(0f);

        private NetworkPlayer networkPlayer;
        private NetworkCharacterPresentation characterPresentation;
        private NetworkRitualAuthority ritualAuthority;
        private GhostLifecycleController localGhostLifecycle;
        private GameObject remoteGhost;
        private GameObject hiddenPriest;
        private Transform localGhostTransform;
        private Vector3 targetPosition;
        private float targetYaw;
        private Vector3 lastSubmittedPosition;
        private float lastSubmittedYaw;
        private float nextSendTime;
        private float nextHeartbeatTime;
        private uint localPoseSequence;
        private uint serverPoseSequence;
        private uint observerPoseSequence;
        private bool hasSubmittedPose;
        private bool hasServerPose;
        private bool hasObserverPose;
        private bool deathPresentationReached;
        private bool presentationApplied;

        public bool IsGhostPresentationActive => presentationApplied;

        private void Awake()
        {
            networkPlayer = GetComponent<NetworkPlayer>();
            characterPresentation = GetComponent<NetworkCharacterPresentation>();
        }

        private void Start()
        {
            characterPresentation.CharacterInstanceChanged += HandleCharacterInstanceChanged;
            ResolveRitualAuthority();
            if (presentationActive.Value && IsAuthoritativelyDead())
                deathPresentationReached = true;
            RefreshPresentationState();
        }

        private void OnDestroy()
        {
            if (characterPresentation != null)
                characterPresentation.CharacterInstanceChanged -= HandleCharacterInstanceChanged;

            if (ritualAuthority != null)
                ritualAuthority.SnapshotChanged -= HandleRitualSnapshotChanged;

            if (remoteGhost != null)
                Destroy(remoteGhost);
        }

        private void Update()
        {
            ResolveRitualAuthority();

            if (IsServerInitialized && presentationActive.Value &&
                ritualAuthority != null && ritualAuthority.Snapshot.Phase == RitualPhase.Inactive)
            {
                ResetServerPresentationState();
            }

            RefreshPresentationState();

            if (IsOwner)
                PublishLocalPose();
        }

        private void LateUpdate()
        {
            if (IsOwner || !presentationApplied || remoteGhost == null || !hasObserverPose)
                return;

            float blend = 1f - Mathf.Exp(-remoteInterpolationSpeed * Time.deltaTime);
            Transform ghostTransform = remoteGhost.transform;
            ghostTransform.position = Vector3.Lerp(ghostTransform.position, targetPosition, blend);
            float yaw = Mathf.LerpAngle(ghostTransform.eulerAngles.y, targetYaw, blend);
            ghostTransform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        public void ReachDeathPresentationBarrier(
            GameObject priest,
            Transform prisonSpawn,
            Camera deathCamera,
            GhostLifecycleController authoredGhost)
        {
            deathPresentationReached = true;

            if (!IsOwner)
            {
                RefreshPresentationState();
                return;
            }

            if (priest == null || prisonSpawn == null || deathCamera == null || authoredGhost == null)
            {
                Debug.LogWarning(
                    $"{nameof(NetworkGhostPresentation)} on '{name}' cannot start the owned Ghost " +
                    "because its priest, prison spawn, Death Camera, or authored Ghost is missing.",
                    this);
                return;
            }

            if (!authoredGhost.BeginGhostPresentation(priest, prisonSpawn, deathCamera))
                return;

            localGhostLifecycle = authoredGhost;
            localGhostTransform = authoredGhost.transform;
            hiddenPriest = priest;
            Vector3 position = localGhostTransform.position;
            float yaw = localGhostTransform.eulerAngles.y;
            RequestGhostActivationServerRpc(position, yaw);
            RefreshPresentationState();
        }

        public void ResetPresentationForLobby()
        {
            deathPresentationReached = false;
            ApplyPresentation(false);
            localGhostLifecycle?.ResetGhostPresentation();
            localGhostLifecycle = null;
            localGhostTransform = null;
            hiddenPriest = null;
        }

        [ServerRpc(RequireOwnership = true)]
        private void RequestGhostActivationServerRpc(Vector3 position, float yaw)
        {
            if (!IsValidPose(position, yaw) || !IsValidDeathPresentationBarrier())
                return;

            activationPosition.Value = position;
            activationYaw.Value = NormalizeYaw(yaw);
            presentationActive.Value = true;
        }

        [ServerRpc(RequireOwnership = true)]
        private void SubmitGhostPoseServerRpc(
            uint sequence,
            Vector3 position,
            float yaw,
            Channel channel = Channel.Unreliable)
        {
            if (!presentationActive.Value || !IsAuthoritativelyDead() ||
                !IsValidPose(position, yaw) ||
                (hasServerPose && !IsNewerSequence(sequence, serverPoseSequence)))
            {
                return;
            }

            serverPoseSequence = sequence;
            hasServerPose = true;
            PublishGhostPoseObserversRpc(sequence, position, NormalizeYaw(yaw), channel);
        }

        [ObserversRpc(ExcludeOwner = true, BufferLast = true)]
        private void PublishGhostPoseObserversRpc(
            uint sequence,
            Vector3 position,
            float yaw,
            Channel channel = Channel.Unreliable)
        {
            if (IsOwner || !IsValidPose(position, yaw) ||
                (hasObserverPose && !IsNewerSequence(sequence, observerPoseSequence)))
            {
                return;
            }

            observerPoseSequence = sequence;
            hasObserverPose = true;
            targetPosition = position;
            targetYaw = NormalizeYaw(yaw);
        }

        private void PublishLocalPose()
        {
            if (!presentationApplied || !presentationActive.Value ||
                !IsAuthoritativelyDead() || localGhostTransform == null ||
                localGhostLifecycle == null || !localGhostLifecycle.GhostModeActive)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (now < nextSendTime)
                return;

            Vector3 position = localGhostTransform.position;
            float yaw = localGhostTransform.eulerAngles.y;
            if (!IsValidPose(position, yaw))
                return;

            bool changed = !hasSubmittedPose ||
                Vector3.SqrMagnitude(position - lastSubmittedPosition) >=
                    positionChangeThreshold * positionChangeThreshold ||
                Mathf.Abs(Mathf.DeltaAngle(yaw, lastSubmittedYaw)) >= yawChangeThreshold;
            if (!changed && now < nextHeartbeatTime)
                return;

            localPoseSequence++;
            lastSubmittedPosition = position;
            lastSubmittedYaw = yaw;
            hasSubmittedPose = true;
            nextSendTime = now + 1f / Mathf.Max(1f, sendRate);
            nextHeartbeatTime = now + Mathf.Max(0.1f, heartbeatInterval);
            SubmitGhostPoseServerRpc(localPoseSequence, position, yaw, Channel.Unreliable);
        }

        private void ResolveRitualAuthority()
        {
            NetworkRitualAuthority resolved = NetworkRitualAuthority.Instance;
            if (ritualAuthority == resolved)
                return;

            if (ritualAuthority != null)
                ritualAuthority.SnapshotChanged -= HandleRitualSnapshotChanged;

            ritualAuthority = resolved;
            if (ritualAuthority != null)
                ritualAuthority.SnapshotChanged += HandleRitualSnapshotChanged;
        }

        private void HandleRitualSnapshotChanged(RitualSnapshot snapshot)
        {
            RefreshPresentationState();
        }

        private void RefreshPresentationState()
        {
            bool shouldPresent = presentationActive.Value &&
                deathPresentationReached && IsAuthoritativelyDead();
            ApplyPresentation(shouldPresent);
        }

        private void ApplyPresentation(bool active)
        {
            if (presentationApplied == active)
                return;

            presentationApplied = active;
            if (active)
            {
                hiddenPriest = characterPresentation.CharacterInstance;
                if (hiddenPriest != null)
                    hiddenPriest.SetActive(false);

                if (!IsOwner)
                {
                    EnsureRemoteGhost();
                    if (remoteGhost != null)
                    {
                        targetPosition = activationPosition.Value;
                        targetYaw = activationYaw.Value;
                        hasObserverPose = true;
                        remoteGhost.transform.SetPositionAndRotation(
                            targetPosition,
                            Quaternion.Euler(0f, targetYaw, 0f));
                        remoteGhost.SetActive(true);
                    }
                }
                return;
            }

            if (remoteGhost != null)
                remoteGhost.SetActive(false);

            localGhostLifecycle?.ResetGhostPresentation();
            localGhostLifecycle = null;
            localGhostTransform = null;

            if (hiddenPriest != null)
                hiddenPriest.SetActive(true);

            hiddenPriest = null;
            hasObserverPose = false;
            hasSubmittedPose = false;
        }

        private void EnsureRemoteGhost()
        {
            if (remoteGhost != null || ghostVisualPrefab == null)
                return;

            remoteGhost = Instantiate(ghostVisualPrefab);
            remoteGhost.name = $"NetworkGhost_{networkPlayer.Owner.ClientId}";
            DisableUnexpectedRuntimeComponents(remoteGhost);
            remoteGhost.SetActive(false);
        }

        private void HandleCharacterInstanceChanged(GameObject characterInstance)
        {
            if (!presentationApplied)
                return;

            hiddenPriest = characterInstance;
            if (hiddenPriest != null)
                hiddenPriest.SetActive(false);
        }

        private bool IsValidDeathPresentationBarrier()
        {
            ResolveRitualAuthority();
            if (ritualAuthority == null || string.IsNullOrEmpty(networkPlayer.PlayerId))
                return false;

            RitualSnapshot snapshot = ritualAuthority.Snapshot;
            if (IsPlayerDead(snapshot.Roster))
                return true;

            return snapshot.Phase == RitualPhase.ResolvingTurn &&
                snapshot.Consequence.HasConsequence &&
                snapshot.Consequence.ConsequenceType == RitualConsequenceType.TimerExpired &&
                string.Equals(
                    snapshot.Consequence.PlayerId,
                    networkPlayer.PlayerId,
                    System.StringComparison.Ordinal);
        }

        private bool IsAuthoritativelyDead()
        {
            ResolveRitualAuthority();
            return ritualAuthority != null && IsPlayerDead(ritualAuthority.Snapshot.Roster);
        }

        private bool IsPlayerDead(RitualRosterSnapshot roster)
        {
            if (string.IsNullOrEmpty(networkPlayer.PlayerId) ||
                !roster.TryGetSeatByPlayer(networkPlayer.PlayerId, out int seatId) ||
                !roster.TryGetPlayerBySeat(seatId, out RitualRosterEntrySnapshot entry))
            {
                return false;
            }

            return entry.IsEliminated;
        }

        private void ResetServerPresentationState()
        {
            presentationActive.Value = false;
            activationPosition.Value = Vector3.zero;
            activationYaw.Value = 0f;
            hasServerPose = false;
        }

        private bool IsValidPose(Vector3 position, float yaw)
        {
            return IsFinite(position.x) && IsFinite(position.y) && IsFinite(position.z) &&
                IsFinite(yaw) && position.sqrMagnitude <= maximumWorldMagnitude * maximumWorldMagnitude;
        }

        private static void DisableUnexpectedRuntimeComponents(GameObject ghost)
        {
            foreach (Camera camera in ghost.GetComponentsInChildren<Camera>(true))
                camera.enabled = false;

            foreach (AudioListener listener in ghost.GetComponentsInChildren<AudioListener>(true))
                listener.enabled = false;

            foreach (GhostMovement movement in ghost.GetComponentsInChildren<GhostMovement>(true))
                movement.enabled = false;

            foreach (GhostCameraController cameraController in
                ghost.GetComponentsInChildren<GhostCameraController>(true))
            {
                cameraController.enabled = false;
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static float NormalizeYaw(float yaw)
        {
            return Mathf.Repeat(yaw, 360f);
        }

        private static bool IsNewerSequence(uint candidate, uint current)
        {
            return unchecked((int)(candidate - current)) > 0;
        }
    }
}
