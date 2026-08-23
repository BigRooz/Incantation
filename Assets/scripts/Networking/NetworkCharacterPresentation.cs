using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Incantation.Character;

namespace Incantation.Networking
{
    /// <summary>
    /// Owns the local visual representation of one observed NetworkPlayer.
    /// NetworkPlayer owns identity and SeatId; this component only presents that state.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkPlayer))]
    public sealed class NetworkCharacterPresentation : MonoBehaviour
    {
        [SerializeField] private GameObject characterPrefab;

        private NetworkPlayer networkPlayer;
        private SeatManager seatManager;
        private GameObject characterInstance;
        private Seat occupiedSeat;
        private bool usesSceneCharacter;
        private CharacterAppearancePresentation appearancePresentation;

        public GameObject CharacterInstance => characterInstance;
        public event Action<GameObject> CharacterInstanceChanged;

        private void Awake()
        {
            networkPlayer = GetComponent<NetworkPlayer>();
            networkPlayer.ClientStarted += HandleClientStarted;
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void Start()
        {
            networkPlayer.SeatIdChanged += HandleSeatIdChanged;
            NetworkPlayer.ActivePlayerRemoved += HandleNetworkPlayerRemoved;
            TryBindPresentation();
        }

        private void OnDestroy()
        {
            if (networkPlayer != null)
            {
                networkPlayer.ClientStarted -= HandleClientStarted;
                networkPlayer.SeatIdChanged -= HandleSeatIdChanged;
                networkPlayer.AppearanceSlotChanged -= HandleAppearanceSlotChanged;
            }

            NetworkPlayer.ActivePlayerRemoved -= HandleNetworkPlayerRemoved;
            ReleaseOccupiedSeat();

            if (characterInstance != null && !usesSceneCharacter)
                Destroy(characterInstance);

            characterInstance = null;
            CharacterInstanceChanged?.Invoke(null);
            DetachAppearancePresentation();
            seatManager = null;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
        {
            TryBindPresentation();
        }

        private void HandleClientStarted()
        {
            TryBindPresentation();
        }

        private void HandleSeatIdChanged(int previousSeatId, int currentSeatId)
        {
            ApplySeatId(currentSeatId);
        }

        private void HandleNetworkPlayerRemoved(NetworkPlayer removedPlayer)
        {
            if (removedPlayer != networkPlayer)
                return;

            ReleaseOccupiedSeat();

            if (characterInstance != null && !usesSceneCharacter)
                Destroy(characterInstance);

            characterInstance = null;
            CharacterInstanceChanged?.Invoke(null);
        }

        private void TryBindPresentation()
        {
            if (networkPlayer == null || !networkPlayer.IsClientInitialized)
                return;

            SeatManager resolvedSeatManager = FindFirstObjectByType<SeatManager>();
            if (resolvedSeatManager == null)
                return;

            if (seatManager != resolvedSeatManager)
            {
                ReleaseOccupiedSeat();
                seatManager = resolvedSeatManager;
            }

            if (characterInstance == null)
                CreateOrClaimCharacter();

            BindAppearancePresentation();
            ApplySeatId(networkPlayer.SeatId);
        }

        private void CreateOrClaimCharacter()
        {
            if (networkPlayer.IsOwner && seatManager.LocalLobbyPlayer != null)
            {
                characterInstance = seatManager.LocalLobbyPlayer;
                usesSceneCharacter = true;
            }
            else if (seatManager.LocalLobbyPlayer != null)
            {
                characterInstance = Instantiate(seatManager.LocalLobbyPlayer);
                characterInstance.name = $"NetworkCharacter_{networkPlayer.Owner.ClientId}";
                usesSceneCharacter = false;
                DisableNonOwnerControls(characterInstance);
            }
            else if (characterPrefab != null)
            {
                characterInstance = Instantiate(characterPrefab);
                characterInstance.name = $"NetworkCharacter_{networkPlayer.Owner.ClientId}";
                usesSceneCharacter = false;
                DisableNonOwnerControls(characterInstance);
            }
            else
            {
                Debug.LogError(
                    $"{nameof(NetworkCharacterPresentation)} on {name} has no character prefab assigned.",
                    this);
                return;
            }

            SetLocalMicrophoneCaptureAllowed(characterInstance, networkPlayer.IsOwner);
            characterInstance.transform.SetParent(null, true);
            CharacterInstanceChanged?.Invoke(characterInstance);
        }

        private void BindAppearancePresentation()
        {
            if (characterInstance == null || appearancePresentation != null)
            {
                return;
            }

            appearancePresentation = characterInstance.GetComponent<CharacterAppearancePresentation>();
            if (appearancePresentation == null)
            {
                appearancePresentation = characterInstance.AddComponent<CharacterAppearancePresentation>();
            }

            appearancePresentation.Initialize();
            networkPlayer.AppearanceSlotChanged += HandleAppearanceSlotChanged;

            foreach (AppearanceSlotValue value in networkPlayer.AppearanceSlots)
            {
                appearancePresentation.Apply(value);
            }

            if (!networkPlayer.IsOwner)
            {
                return;
            }

            appearancePresentation.LocalSelectionChanged += HandleLocalSelectionChanged;
            foreach (AppearanceSlotValue value in appearancePresentation.CaptureSupportedAppearance())
            {
                networkPlayer.RequestAppearanceSlot(value.Slot, value.ValueId);
            }
        }

        private void DetachAppearancePresentation()
        {
            if (networkPlayer != null)
            {
                networkPlayer.AppearanceSlotChanged -= HandleAppearanceSlotChanged;
            }

            if (appearancePresentation != null)
            {
                appearancePresentation.LocalSelectionChanged -= HandleLocalSelectionChanged;
            }

            appearancePresentation = null;
        }

        private void HandleAppearanceSlotChanged(AppearanceSlotValue value)
        {
            appearancePresentation?.Apply(value);
        }

        private void HandleLocalSelectionChanged(AppearanceSlot slot, int valueId)
        {
            networkPlayer.RequestAppearanceSlot(slot, valueId);
        }

        private void ApplySeatId(int seatId)
        {
            if (seatManager == null || characterInstance == null)
                return;

            Seat targetSeat = seatManager.GetSeatById(seatId);
            if (occupiedSeat != targetSeat)
                ReleaseOccupiedSeat();

            if (targetSeat == null)
            {
                if (!usesSceneCharacter)
                    characterInstance.SetActive(false);

                return;
            }

            if (targetSeat.playerSpawn == null)
            {
                Debug.LogWarning(
                    $"{nameof(NetworkCharacterPresentation)} cannot place {characterInstance.name} at " +
                    $"{targetSeat.name} because PlayerSpawn is not assigned.",
                    targetSeat);
                return;
            }

            characterInstance.SetActive(true);
            characterInstance.transform.SetPositionAndRotation(
                targetSeat.playerSpawn.position,
                targetSeat.playerSpawn.rotation);

            targetSeat.Occupy(characterInstance);
            occupiedSeat = targetSeat;
        }

        public void ReapplyCurrentSeatPresentation()
        {
            ApplySeatId(networkPlayer != null ? networkPlayer.SeatId : NetworkPlayer.UnassignedSeatId);
        }

        private void ReleaseOccupiedSeat()
        {
            if (occupiedSeat != null && occupiedSeat.currentPlayer == characterInstance)
                occupiedSeat.Free();

            occupiedSeat = null;
        }

        private static void DisableNonOwnerControls(GameObject character)
        {
            foreach (Camera characterCamera in character.GetComponentsInChildren<Camera>(true))
                characterCamera.enabled = false;

            foreach (AudioListener audioListener in character.GetComponentsInChildren<AudioListener>(true))
                audioListener.enabled = false;

            foreach (PlayerMovement playerMovement in character.GetComponentsInChildren<PlayerMovement>(true))
                playerMovement.enabled = false;

            foreach (VoiceAmplitudeProvider provider in character.GetComponentsInChildren<VoiceAmplitudeProvider>(true))
                provider.SetMicrophoneCaptureAllowed(false);
        }

        private static void SetLocalMicrophoneCaptureAllowed(GameObject character, bool allowed)
        {
            if (character == null)
                return;

            foreach (VoiceAmplitudeProvider provider in character.GetComponentsInChildren<VoiceAmplitudeProvider>(true))
                provider.SetMicrophoneCaptureAllowed(allowed);
        }
    }
}
