using UnityEngine;
using UnityEngine.SceneManagement;

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

        public GameObject CharacterInstance => characterInstance;

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
            }

            NetworkPlayer.ActivePlayerRemoved -= HandleNetworkPlayerRemoved;
            ReleaseOccupiedSeat();

            if (characterInstance != null && !usesSceneCharacter)
                Destroy(characterInstance);

            characterInstance = null;
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

            ApplySeatId(networkPlayer.SeatId);
        }

        private void CreateOrClaimCharacter()
        {
            if (networkPlayer.IsOwner && seatManager.LocalLobbyPlayer != null)
            {
                characterInstance = seatManager.LocalLobbyPlayer;
                usesSceneCharacter = true;
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

            characterInstance.transform.SetParent(null, true);
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
        }
    }
}
