using Incantation.Networking.Ritual;
using UnityEngine;

namespace Incantation.Networking
{
    /// <summary>
    /// Coordinates local presentation restoration after NetworkRitualAuthority commits the shared
    /// Completed-to-Inactive transition. It never writes ritual state.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkRitualAuthority))]
    [RequireComponent(typeof(NetworkBookAuthority))]
    public sealed class NetworkPostGameLifecycleController : MonoBehaviour
    {
        private NetworkRitualAuthority ritualAuthority;
        private NetworkBookAuthority bookAuthority;
        private NetworkGameOverPresentationController gameOverPresentation;
        private uint completedRitualSequence;
        private uint locallyResetRitualSequence;

        private void Awake()
        {
            ritualAuthority = GetComponent<NetworkRitualAuthority>();
            bookAuthority = GetComponent<NetworkBookAuthority>();
            gameOverPresentation = GetComponent<NetworkGameOverPresentationController>();
        }

        private void Start()
        {
            ritualAuthority.SnapshotChanged += HandleSnapshotChanged;
            HandleSnapshotChanged(ritualAuthority.Snapshot);
        }

        private void OnDestroy()
        {
            if (ritualAuthority != null)
                ritualAuthority.SnapshotChanged -= HandleSnapshotChanged;
        }

        private void HandleSnapshotChanged(RitualSnapshot snapshot)
        {
            if (snapshot.IsGameOver && snapshot.Phase == RitualPhase.Completed)
            {
                completedRitualSequence = snapshot.SequenceId.Value;
                return;
            }

            if (snapshot.Phase != RitualPhase.Inactive || snapshot.IsGameOver ||
                completedRitualSequence == 0 ||
                locallyResetRitualSequence == completedRitualSequence)
            {
                return;
            }

            locallyResetRitualSequence = completedRitualSequence;
            ResetLocalPresentation();
        }

        private void ResetLocalPresentation()
        {
            gameOverPresentation?.ResetForLobby();

            RitualController ritualController = FindFirstObjectByType<RitualController>(
                FindObjectsInactive.Include);
            ritualController?.StopRitual();

            foreach (BookPrisonSpectatorController spectator in
                FindObjectsByType<BookPrisonSpectatorController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None))
            {
                spectator.ResetSpectatorView();
            }

            foreach (PlayerAbsorptionController absorption in
                FindObjectsByType<PlayerAbsorptionController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None))
            {
                absorption.ResetAbsorption();
            }

            foreach (NetworkCharacterPresentation presentation in
                FindObjectsByType<NetworkCharacterPresentation>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None))
            {
                presentation.ReapplyCurrentSeatPresentation();
            }

            NetworkPlayer.LocalPlayer?
                .GetComponent<NetworkCharacterLookPose>()?
                .ResetPoseForLobby();

            FindFirstObjectByType<DeathVisionVignetteController>(
                FindObjectsInactive.Include)?.ResetVignette();
            FindFirstObjectByType<DemonHandController>(
                FindObjectsInactive.Include)?.ResetSequence();
            FindFirstObjectByType<LobbyController>(
                FindObjectsInactive.Include)?.ReturnToLobbyPresentation();
            FindFirstObjectByType<BookMenuController>(
                FindObjectsInactive.Include)?.PresentConnectedLobbyAfterMatch();

            if (bookAuthority.IsServerInitialized)
                bookAuthority.TryResetToLobbyPose();

            Debug.Log(
                "[PostGameLifecycle]\n" +
                "Local Lobby Presentation Restored\n" +
                $"CompletedRitualSequence = {locallyResetRitualSequence}\n" +
                $"ServerBookReset = {bookAuthority.IsServerInitialized}",
                this);
        }
    }
}
