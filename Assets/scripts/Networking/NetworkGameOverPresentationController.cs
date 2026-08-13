using System;
using Incantation.Networking.Ritual;
using Incantation.UI;
using UnityEngine;

namespace Incantation.Networking
{
    /// <summary>
    /// Presents synchronized game-over truth without participating in winner selection or ritual flow.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkRitualAuthority))]
    [RequireComponent(typeof(NetworkBookAuthority))]
    public sealed class NetworkGameOverPresentationController : MonoBehaviour
    {
        [SerializeField] private NetworkRitualAuthority ritualAuthority;
        [SerializeField] private NetworkBookAuthority bookAuthority;
        [SerializeField] private RitualGameOverPresenter resultPresenter;

        private RitualSnapshot pendingSnapshot;
        private bool hasPendingSnapshot;
        private string locallyPresentedResultKey = string.Empty;
        private string serverRequestedMovementKey = string.Empty;

        private void Awake()
        {
            if (ritualAuthority == null)
                ritualAuthority = GetComponent<NetworkRitualAuthority>();

            if (bookAuthority == null)
                bookAuthority = GetComponent<NetworkBookAuthority>();

            if (resultPresenter == null)
                resultPresenter = GetComponent<RitualGameOverPresenter>();
        }

        private void Start()
        {
            ritualAuthority.SnapshotChanged += HandleAuthoritativeSnapshot;
            HandleAuthoritativeSnapshot(ritualAuthority.Snapshot);
        }

        private void OnDestroy()
        {
            if (ritualAuthority != null)
                ritualAuthority.SnapshotChanged -= HandleAuthoritativeSnapshot;

            hasPendingSnapshot = false;
        }

        private void Update()
        {
            if (hasPendingSnapshot)
                TryPresentPendingSnapshot();
        }

        private void HandleAuthoritativeSnapshot(RitualSnapshot snapshot)
        {
            if (!snapshot.IsGameOver ||
                snapshot.Phase != RitualPhase.Completed ||
                snapshot.SequenceId.Value == 0 ||
                string.IsNullOrEmpty(snapshot.WinnerPlayerId))
            {
                return;
            }

            pendingSnapshot = snapshot;
            hasPendingSnapshot = true;
            TryPresentPendingSnapshot();
        }

        private void TryPresentPendingSnapshot()
        {
            if (!TryResolveWinner(pendingSnapshot.WinnerPlayerId, out NetworkPlayer winner))
                return;

            string resultKey = CreateResultKey(pendingSnapshot);
            if (!string.Equals(locallyPresentedResultKey, resultKey, StringComparison.Ordinal))
            {
                bool isLocalWinner = NetworkPlayer.LocalPlayer != null &&
                    string.Equals(
                        NetworkPlayer.LocalPlayer.PlayerId,
                        pendingSnapshot.WinnerPlayerId,
                        StringComparison.Ordinal);
                bool canReturnToLobby = NetworkPlayer.LocalPlayer != null &&
                    NetworkPlayer.LocalPlayer.IsOwner &&
                    RitualSealService.Instance != null &&
                    RitualSealService.Instance.IsHostingRitual;

                if (isLocalWinner)
                {
                    winner.GetComponent<NetworkCharacterLookPose>()?
                        .NeutralizeLocalPoseForGameOver();
                }

                resultPresenter?.ShowResult(
                    winner.PriestName,
                    isLocalWinner,
                    canReturnToLobby,
                    RequestReturnToLobby);
                locallyPresentedResultKey = resultKey;
            }

            if (bookAuthority.IsServerInitialized &&
                !string.Equals(serverRequestedMovementKey, resultKey, StringComparison.Ordinal))
            {
                if (winner.SeatId == NetworkPlayer.UnassignedSeatId ||
                    !bookAuthority.TryExecuteWinnerPresentationMovement(
                        pendingSnapshot.RitualSessionId,
                        pendingSnapshot.SequenceId.Value,
                        pendingSnapshot.WinnerPlayerId,
                        winner.SeatId))
                {
                    return;
                }

                serverRequestedMovementKey = resultKey;
            }

            if (!bookAuthority.IsServerInitialized ||
                string.Equals(serverRequestedMovementKey, resultKey, StringComparison.Ordinal))
            {
                hasPendingSnapshot = false;
            }
        }

        public void ResetForLobby()
        {
            hasPendingSnapshot = false;
            pendingSnapshot = default;
            locallyPresentedResultKey = string.Empty;
            serverRequestedMovementKey = string.Empty;
            resultPresenter?.HideResult();
        }

        private static void RequestReturnToLobby()
        {
            NetworkPlayer.LocalPlayer?.RequestReturnToLobby();
        }

        private static bool TryResolveWinner(string winnerPlayerId, out NetworkPlayer winner)
        {
            foreach (NetworkPlayer player in NetworkPlayer.ActivePlayers)
            {
                if (player != null &&
                    string.Equals(player.PlayerId, winnerPlayerId, StringComparison.Ordinal))
                {
                    winner = player;
                    return true;
                }
            }

            winner = null;
            return false;
        }

        private static string CreateResultKey(RitualSnapshot snapshot)
        {
            return $"{snapshot.RitualSessionId}:{snapshot.SequenceId.Value}:{snapshot.WinnerPlayerId}";
        }
    }
}
