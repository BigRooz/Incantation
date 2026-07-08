using System.Collections.Generic;
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

        if (movePlayerToPrison)
            player.SetPositionAndRotation(slot.spawnPoint.position, slot.spawnPoint.rotation);

        if (activateSpectatorCamera)
            ActivateSlotCamera(slot);
        else
            DeactivateAllSlotCameras();

        if (deactivateTablePlayerModel)
            player.gameObject.SetActive(false);

        onSpectatorStarted?.Invoke();
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

        activeSlot.spectatorCamera.gameObject.SetActive(true);
        activeSlot.spectatorCamera.enabled = true;
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

            slot.spectatorCamera.enabled = false;
            slot.spectatorCamera.gameObject.SetActive(false);
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

        public PlayerSpectatorState(Transform player)
        {
            Player = player;
            originalPosition = player.position;
            originalRotation = player.rotation;
            originalScale = player.localScale;
            originalActiveSelf = player.gameObject.activeSelf;
        }

        public void Restore()
        {
            if (Player == null)
                return;

            Player.gameObject.SetActive(originalActiveSelf);
            Player.SetPositionAndRotation(originalPosition, originalRotation);
            Player.localScale = originalScale;
        }
    }
}
