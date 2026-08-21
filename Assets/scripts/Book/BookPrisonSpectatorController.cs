using System.Collections.Generic;
using Incantation.Networking;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class BookPrisonSlot
{
    public Transform spawnPoint;
    public Camera spectatorCamera;
    [System.NonSerialized] public bool occupied;
}

/// <summary>
/// Local prototype spectator transition after the book consumes a player.
/// Uses fixed prison slots only; it does not add free movement or player controls.
/// </summary>
public class BookPrisonSpectatorController : MonoBehaviour
{
    [Header("Current Failed Player Source")]
    [SerializeField] private RitualController ritualController;

    [Header("Book Prison Slots")]
    [SerializeField] private BookPrisonSlot[] prisonSlots;

    [Header("Book Portal")]
    [SerializeField] private Camera bookPortalCamera;
    [SerializeField] private RenderTexture bookPortalRenderTexture;
    [SerializeField] private Renderer portalScreenRenderer;

    [Header("Behavior")]
    [SerializeField] private bool movePlayerToPrison = true;
    [SerializeField] private bool activateSpectatorCamera = true;
    [SerializeField] private bool deactivateTablePlayerModel = false;

    [Header("Events")]
    [SerializeField] private UnityEvent onSpectatorStarted;

    private readonly List<PlayerSpectatorState> playerStates = new List<PlayerSpectatorState>();
    private GhostLifecycleController ghostLifecycleController;

    public void SendPlayerToBookPrison(Transform player)
    {
        if (player == null)
        {
            Debug.LogWarning($"{nameof(BookPrisonSpectatorController)} on '{gameObject.name}' cannot send a player to the Book Prison because the player Transform is null.", this);
            return;
        }

        BookPrisonSlot slot = GetFirstOpenSlot();

        if (slot == null)
        {
            Debug.LogWarning($"{nameof(BookPrisonSpectatorController)} on '{gameObject.name}' cannot send '{player.name}' to the Book Prison because no prison slot with an assigned spawnPoint is available. Add slots with BookPrisonSpawnPoint1, BookPrisonSpawnPoint2, etc. or assign prisonSlots manually.", this);
            return;
        }

        slot.occupied = true;
        StorePlayerState(player);
        ConfigurePortalView();

        bool isLocalDeathCameraTarget = TryResolveLocalDeathCameraTarget(
            out NetworkPlayer eliminatedNetworkPlayer,
            out string resolutionReason);
        NetworkCharacterPresentation eliminatedPresentation =
            eliminatedNetworkPlayer != null
                ? eliminatedNetworkPlayer.GetComponent<NetworkCharacterPresentation>()
                : null;
        GameObject presentedCharacter = eliminatedPresentation != null
            ? eliminatedPresentation.CharacterInstance
            : null;
        bool presentationMatchesFailedTransform =
            presentedCharacter != null &&
            (player == presentedCharacter.transform ||
                player.IsChildOf(presentedCharacter.transform) ||
                presentedCharacter.transform.IsChildOf(player));
        bool isNetworkSession = NetworkRitualAuthority.Instance != null &&
            NetworkRitualAuthority.Instance.IsNetworkSessionActive;
        bool shouldRestoreLocalPresentation = isLocalDeathCameraTarget &&
            (!isNetworkSession || presentationMatchesFailedTransform);

        if (shouldRestoreLocalPresentation)
            PrepareLocalDeathPresentation(player);

        if (movePlayerToPrison)
            MovePlayerToPrisonPreservingRemoteCamera(
                player,
                slot.spawnPoint,
                isLocalDeathCameraTarget);
        if (activateSpectatorCamera && isLocalDeathCameraTarget)
            ActivateSlotCamera(slot);
        else if (!activateSpectatorCamera)
            DeactivateAllSlotCameras();

        Debug.Log(
            "[DeathCamera]\n" +
            $"EliminatedPlayerId = {GetEliminatedPlayerId()}\n" +
            $"EliminatedSeat = {(eliminatedNetworkPlayer != null ? eliminatedNetworkPlayer.SeatId : NetworkPlayer.UnassignedSeatId)}\n" +
            $"OwnerConnectionId = {(eliminatedNetworkPlayer?.Connection != null ? eliminatedNetworkPlayer.Connection.ClientId : -1)}\n" +
            $"Presentation = {(presentedCharacter != null ? presentedCharacter.name : "none")}\n" +
            $"PresentationMatchesFailedTransform = {presentationMatchesFailedTransform}\n" +
            $"LocalOwner = {isLocalDeathCameraTarget}\n" +
            $"CameraActivation = {(activateSpectatorCamera && isLocalDeathCameraTarget ? "accepted" : "ignored")}\n" +
            $"Reason = {resolutionReason}",
            this);

        if (deactivateTablePlayerModel)
            player.gameObject.SetActive(false);

        if (isNetworkSession && eliminatedNetworkPlayer != null)
        {
            eliminatedNetworkPlayer.GetComponent<NetworkGhostPresentation>()?
                .ReachDeathPresentationBarrier(
                    player.gameObject,
                    slot.spawnPoint,
                    slot.spectatorCamera,
                    isLocalDeathCameraTarget ? ResolveGhostLifecycleController() : null);
        }
        else if (isLocalDeathCameraTarget)
        {
            ResolveGhostLifecycleController()?.BeginGhostPresentation(
                player.gameObject,
                slot.spawnPoint,
                slot.spectatorCamera);
        }

        onSpectatorStarted?.Invoke();
    }

    private void PrepareLocalDeathPresentation(Transform player)
    {
        foreach (Camera characterCamera in player.GetComponentsInChildren<Camera>(true))
            characterCamera.enabled = false;

        foreach (AudioListener audioListener in player.GetComponentsInChildren<AudioListener>(true))
            audioListener.enabled = false;

        PlayerAbsorptionController absorptionController =
            GetComponent<PlayerAbsorptionController>();
        if (absorptionController == null ||
            !absorptionController.TryRestoreAbsorbedTargetForPresentation(player))
        {
            Debug.LogWarning(
                $"{nameof(BookPrisonSpectatorController)} on '{gameObject.name}' could not restore " +
                $"the absorbed visual state for locally owned player '{player.name}'. Ensure " +
                $"{nameof(PlayerAbsorptionController)} is on the same GameObject and absorbed the same target.",
                this);
            return;
        }

        foreach (PlayerMovement playerMovement in player.GetComponentsInChildren<PlayerMovement>(true))
            playerMovement.enabled = true;
    }

    public void SendCurrentFailedPlayerToBookPrison()
    {
        Transform failedPlayer = ritualController != null ? ritualController.CurrentFailedPlayer : null;

        if (failedPlayer == null)
        {
            Debug.LogWarning($"{nameof(BookPrisonSpectatorController)} on '{gameObject.name}' cannot send the current failed player to the Book Prison because RitualController.CurrentFailedPlayer is null. Assign ritualController and make sure ritual failure stored a failed player Transform.", this);
            return;
        }

        SendPlayerToBookPrison(failedPlayer);
    }

    public void ResetSpectatorView()
    {
        ResolveGhostLifecycleController()?.ResetGhostPresentation();

        if (prisonSlots != null)
        {
            for (int i = 0; i < prisonSlots.Length; i++)
            {
                if (prisonSlots[i] != null)
                    prisonSlots[i].occupied = false;
            }
        }

        DeactivateAllSlotCameras();
        RestorePlayerStates();
        playerStates.Clear();
    }

    private GhostLifecycleController ResolveGhostLifecycleController()
    {
        if (ghostLifecycleController == null)
        {
            ghostLifecycleController = FindFirstObjectByType<GhostLifecycleController>(
                FindObjectsInactive.Include);
        }

        return ghostLifecycleController;
    }

    private BookPrisonSlot GetFirstOpenSlot()
    {
        if (prisonSlots == null || prisonSlots.Length == 0)
            return null;

        for (int i = 0; i < prisonSlots.Length; i++)
        {
            BookPrisonSlot slot = prisonSlots[i];

            if (slot == null || slot.spawnPoint == null || slot.occupied)
                continue;

            return slot;
        }

        return null;
    }

    private void ActivateSlotCamera(BookPrisonSlot activeSlot)
    {
        DeactivateAllSlotCameras();

        if (activeSlot == null || activeSlot.spectatorCamera == null)
        {
            Debug.LogWarning($"{nameof(BookPrisonSpectatorController)} on '{gameObject.name}' assigned a Book Prison slot, but that slot has no spectatorCamera. Assign a camera for the occupied slot.", this);
            return;
        }

        FindFirstObjectByType<CameraTransitionManager>(
            FindObjectsInactive.Include)?.DisableRendering();

        activeSlot.spectatorCamera.gameObject.SetActive(true);
        activeSlot.spectatorCamera.enabled = true;

        AudioListener spectatorListener =
            activeSlot.spectatorCamera.GetComponent<AudioListener>();
        if (spectatorListener != null)
            spectatorListener.enabled = true;
    }

    private bool TryResolveLocalDeathCameraTarget(
        out NetworkPlayer eliminatedNetworkPlayer,
        out string resolutionReason)
    {
        eliminatedNetworkPlayer = null;
        string eliminatedPlayerId = GetEliminatedPlayerId();
        NetworkRitualAuthority authority = NetworkRitualAuthority.Instance;
        bool isNetworkSession = authority != null &&
            authority.IsNetworkSessionActive;
        if (!isNetworkSession)
        {
            resolutionReason =
                "Offline ritual preserves local death-camera behavior.";
            return true;
        }

        if (string.IsNullOrEmpty(eliminatedPlayerId))
        {
            resolutionReason =
                "The authoritative consequence did not provide an eliminated PlayerId.";
            return false;
        }

        IReadOnlyList<NetworkPlayer> activePlayers = NetworkPlayer.ActivePlayers;
        for (int i = 0; i < activePlayers.Count; i++)
        {
            NetworkPlayer networkPlayer = activePlayers[i];
            if (networkPlayer == null ||
                !string.Equals(
                    networkPlayer.PlayerId,
                    eliminatedPlayerId,
                    System.StringComparison.Ordinal))
            {
                continue;
            }

            eliminatedNetworkPlayer = networkPlayer;
            resolutionReason = networkPlayer.IsOwner
                ? "The authoritative eliminated NetworkPlayer is locally owned."
                : "The authoritative eliminated NetworkPlayer is remotely owned.";
            return networkPlayer.IsOwner;
        }

        resolutionReason =
            $"No active NetworkPlayer matches authoritative PlayerId {eliminatedPlayerId}.";
        return false;
    }

    private string GetEliminatedPlayerId()
    {
        return ritualController != null
            ? ritualController.CurrentFailedPlayerId
            : string.Empty;
    }

    private void DeactivateAllSlotCameras()
    {
        if (prisonSlots == null)
            return;

        for (int i = 0; i < prisonSlots.Length; i++)
        {
            BookPrisonSlot slot = prisonSlots[i];

            if (slot == null || slot.spectatorCamera == null)
                continue;

            AudioListener spectatorListener =
                slot.spectatorCamera.GetComponent<AudioListener>();
            if (spectatorListener != null)
                spectatorListener.enabled = false;

            slot.spectatorCamera.enabled = false;
            slot.spectatorCamera.gameObject.SetActive(false);
        }
    }

    public void DisableSpectatorCameraPresentation()
    {
        DeactivateAllSlotCameras();
    }

    private void MovePlayerToPrisonPreservingRemoteCamera(
        Transform player,
        Transform destination,
        bool isLocalDeathCameraTarget)
    {
        Camera[] cameraCandidates = !isLocalDeathCameraTarget
            ? FindObjectsByType<Camera>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            : System.Array.Empty<Camera>();
        List<Camera> protectedCameras = new List<Camera>();
        List<Vector3> positions = new List<Vector3>();
        List<Quaternion> rotations = new List<Quaternion>();
        for (int i = 0; i < cameraCandidates.Length; i++)
        {
            Camera camera = cameraCandidates[i];
            if (camera == null || !camera.enabled || !camera.gameObject.activeInHierarchy)
                continue;

            Transform cameraTransform = camera.transform;
            if (cameraTransform != player && !cameraTransform.IsChildOf(player))
                continue;

            protectedCameras.Add(camera);
            positions.Add(cameraTransform.position);
            rotations.Add(cameraTransform.rotation);
        }

        player.SetPositionAndRotation(destination.position, destination.rotation);

        for (int i = 0; i < protectedCameras.Count; i++)
        {
            Camera camera = protectedCameras[i];
            if (camera == null || !camera.enabled || !camera.gameObject.activeInHierarchy)
                continue;

            camera.transform.SetPositionAndRotation(positions[i], rotations[i]);
            Debug.Log(
                "[DeathCamera] Camera Mutation Request\n" +
                $"Camera = {camera.name}\n" +
                $"EliminatedPlayerId = {GetEliminatedPlayerId()}\n" +
                "LocalOwner = false\n" +
                "Operation = Move / Rotate by Book Prison ancestor\n" +
                "Result = Ignored\n" +
                $"Source = {nameof(BookPrisonSpectatorController)}",
                this);
        }
    }

    private void StorePlayerState(Transform player)
    {
        for (int i = 0; i < playerStates.Count; i++)
        {
            if (playerStates[i].Player == player)
                return;
        }

        playerStates.Add(new PlayerSpectatorState(player));
    }

    private void RestorePlayerStates()
    {
        for (int i = 0; i < playerStates.Count; i++)
            playerStates[i].Restore();
    }

    private void ConfigurePortalView()
    {
        if (bookPortalCamera != null && bookPortalRenderTexture != null)
        {
            bookPortalCamera.targetTexture = bookPortalRenderTexture;
            bookPortalCamera.gameObject.SetActive(true);
            bookPortalCamera.enabled = true;
        }

        if (portalScreenRenderer != null && bookPortalRenderTexture != null)
            portalScreenRenderer.material.mainTexture = bookPortalRenderTexture;
    }

    private sealed class PlayerSpectatorState
    {
        public Transform Player { get; }

        private readonly Vector3 originalPosition;
        private readonly Quaternion originalRotation;
        private readonly Vector3 originalScale;
        private readonly bool originalActiveSelf;
        private readonly Camera[] cameras;
        private readonly bool[] cameraEnabledStates;
        private readonly AudioListener[] audioListeners;
        private readonly bool[] audioListenerEnabledStates;
        private readonly PlayerMovement[] playerMovements;
        private readonly bool[] playerMovementEnabledStates;

        public PlayerSpectatorState(Transform player)
        {
            Player = player;
            originalPosition = player.position;
            originalRotation = player.rotation;
            originalScale = player.localScale;
            originalActiveSelf = player.gameObject.activeSelf;
            cameras = player.GetComponentsInChildren<Camera>(true);
            cameraEnabledStates = CaptureEnabledStates(cameras);
            audioListeners = player.GetComponentsInChildren<AudioListener>(true);
            audioListenerEnabledStates = CaptureEnabledStates(audioListeners);
            playerMovements = player.GetComponentsInChildren<PlayerMovement>(true);
            playerMovementEnabledStates = CaptureEnabledStates(playerMovements);
        }

        public void Restore()
        {
            if (Player == null)
                return;

            Player.gameObject.SetActive(originalActiveSelf);
            Player.SetPositionAndRotation(originalPosition, originalRotation);
            Player.localScale = originalScale;
            RestoreEnabledStates(cameras, cameraEnabledStates);
            RestoreEnabledStates(audioListeners, audioListenerEnabledStates);
            RestoreEnabledStates(playerMovements, playerMovementEnabledStates);
        }

        private static bool[] CaptureEnabledStates<T>(T[] behaviours) where T : Behaviour
        {
            bool[] states = new bool[behaviours.Length];
            for (int i = 0; i < behaviours.Length; i++)
                states[i] = behaviours[i] != null && behaviours[i].enabled;

            return states;
        }

        private static void RestoreEnabledStates<T>(T[] behaviours, bool[] states)
            where T : Behaviour
        {
            for (int i = 0; i < behaviours.Length && i < states.Length; i++)
            {
                if (behaviours[i] != null)
                    behaviours[i].enabled = states[i];
            }
        }
    }
}
