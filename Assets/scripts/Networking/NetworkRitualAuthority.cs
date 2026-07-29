using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Incantation.Networking.Ritual;
using UnityEngine;

namespace Incantation.Networking
{
    /// <summary>
    /// Replicates the server-owned ritual snapshot foundation without controlling gameplay.
    /// Existing ritual, Book, timer, phrase, voice, outcome, and elimination systems do not
    /// consume or write this state.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkRitualAuthority : NetworkBehaviour
    {
        public const int NoPlayerId = -1;
        public const int NoSeatId = -1;

        private const string DevelopmentSessionId = "DEV-RITUAL-0001";

        private readonly SyncVar<string> ritualSessionId = new(string.Empty);
        private readonly SyncVar<uint> ritualSequence = new(0);
        private readonly SyncVar<RitualPhase> ritualPhase = new(RitualPhase.Inactive);
        private readonly SyncVar<uint> turnSequence = new(0);
        private readonly SyncVar<int> activePlayerId = new(NoPlayerId);
        private readonly SyncVar<int> activeSeatId = new(NoSeatId);
        private readonly SyncVar<RitualTraversalDirection> traversalDirection =
            new(RitualTraversalDirection.Clockwise);
        private readonly SyncVar<RitualValidationMode> validationMode =
            new(RitualValidationMode.WordByWordRealtime);
        private readonly SyncVar<uint> phraseVersion = new(0);
        private readonly SyncList<string> phraseWords = new();
        private readonly SyncVar<uint> unlockedWordCount = new(0);
        private readonly SyncVar<int> expectedWordIndex = new(0);
        private readonly SyncVar<double> turnStartNetworkTime = new(0d);
        private readonly SyncVar<double> timerDeadlineNetworkTime = new(0d);
        private readonly SyncVar<RitualOutcome> latestRitualOutcome = new(RitualOutcome.None);
        private readonly SyncVar<RitualFailureReason> failureReason =
            new(RitualFailureReason.None);
        private readonly SyncVar<bool> isGameOver = new(false);
        private readonly SyncVar<int> winnerPlayerId = new(NoPlayerId);
        private readonly SyncVar<uint> completedRotationCount = new(0);
        private readonly SyncVar<uint> snapshotRevision = new(0);

        private bool snapshotNotificationPending;
        private bool ownsDiscoveryReference;

        public static NetworkRitualAuthority Instance { get; private set; }
        public RitualSnapshot Snapshot => CreateSnapshot();

        public event Action<RitualSnapshot> SnapshotChanged;

        private void OnEnable()
        {
            TryClaimDiscoveryReference();
        }

        private void OnDisable()
        {
            ReleaseDiscoveryReference();
        }

        private void LateUpdate()
        {
            if (!snapshotNotificationPending || !ownsDiscoveryReference)
                return;

            snapshotNotificationPending = false;
            RitualSnapshot snapshot = CreateSnapshot();
            SnapshotChanged?.Invoke(snapshot);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                $"[Ritual Snapshot] Session={snapshot.RitualSessionId}, Ritual={snapshot.SequenceId.Value}, Phase={snapshot.Phase}, Turn={snapshot.Turn.SequenceId.Value}, ActivePlayer={snapshot.ActivePlayerId}, ActiveSeat={snapshot.Turn.ActiveSeatId}, Direction={snapshot.TraversalDirection}, Validation={snapshot.ValidationMode}, Phrase={snapshot.Phrase.SequenceId.Value}, Unlocked={snapshot.Phrase.UnlockedWordCount}, Expected={snapshot.Phrase.ExpectedWordIndex}, Start={snapshot.Turn.StartedAtNetworkTime}, Deadline={snapshot.Turn.EndsAtNetworkTime}, Outcome={snapshot.Outcome.Outcome}, Failure={snapshot.Outcome.FailureReason}, GameOver={snapshot.IsGameOver}, Winner={snapshot.WinnerPlayerId}.",
                this);
#endif
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            if (!TryClaimDiscoveryReference())
                return;

            snapshotRevision.OnChange += HandleSnapshotRevisionChanged;
            snapshotNotificationPending = true;
        }

        public override void OnStopNetwork()
        {
            snapshotRevision.OnChange -= HandleSnapshotRevisionChanged;
            snapshotNotificationPending = false;
            ReleaseDiscoveryReference();
            base.OnStopNetwork();
        }

        private void OnDestroy()
        {
            ReleaseDiscoveryReference();
        }

        /// <summary>
        /// Development-only server mutation used to prove snapshot replication. This is not an
        /// RPC and performs no ritual behavior.
        /// </summary>
        public bool TryApplyDevelopmentTestSnapshot(RitualPhase requestedPhase)
        {
            if (!Application.isEditor && !Debug.isDebugBuild)
            {
                Debug.LogWarning(
                    $"{nameof(NetworkRitualAuthority)} rejected a development snapshot in a non-development build.",
                    this);
                return false;
            }

            if (!IsServerInitialized)
            {
                Debug.LogWarning(
                    $"{nameof(NetworkRitualAuthority)} rejected a development snapshot because only the server may mutate ritual state.",
                    this);
                return false;
            }

            RitualPhase currentPhase = ritualPhase.Value;
            if (!RitualPhaseTransitions.IsLegal(currentPhase, requestedPhase))
            {
                Debug.LogWarning(
                    $"{nameof(NetworkRitualAuthority)} rejected illegal development transition {currentPhase} -> {requestedPhase}.",
                    this);
                return false;
            }

            ritualSessionId.Value = DevelopmentSessionId;
            ritualSequence.Value = 1;
            ritualPhase.Value = requestedPhase;
            turnSequence.Value = 1;
            activePlayerId.Value = 0;
            activeSeatId.Value = 0;
            traversalDirection.Value = RitualTraversalDirection.Clockwise;
            validationMode.Value = RitualValidationMode.WordByWordRealtime;
            phraseVersion.Value = 1;
            phraseWords.Clear();
            phraseWords.Add("maledictus");
            phraseWords.Add("vinculum");
            unlockedWordCount.Value = 2;
            expectedWordIndex.Value = 0;
            turnStartNetworkTime.Value = 1000d;
            timerDeadlineNetworkTime.Value = 1090d;
            latestRitualOutcome.Value = RitualOutcome.None;
            failureReason.Value = RitualFailureReason.None;
            isGameOver.Value = false;
            winnerPlayerId.Value = NoPlayerId;
            completedRotationCount.Value = 0;
            snapshotRevision.Value++;
            return true;
        }

        [ContextMenu("Networking/Apply Deterministic Test Snapshot")]
        private void ApplyDeterministicTestSnapshotFromContextMenu()
        {
            TryApplyDevelopmentTestSnapshot(RitualPhase.Preparing);
        }

        private RitualSnapshot CreateSnapshot()
        {
            string[] words = new string[phraseWords.Count];
            for (int index = 0; index < phraseWords.Count; index++)
            {
                words[index] = phraseWords[index] ?? string.Empty;
            }

            RitualTurnSequenceId turnId = new(turnSequence.Value);
            RitualTurnSnapshot turn = new(
                turnId,
                activeSeatId.Value,
                turnStartNetworkTime.Value,
                timerDeadlineNetworkTime.Value);
            RitualPhraseSnapshot phrase = new(
                new RitualPhraseSequenceId(phraseVersion.Value),
                new RitualWordSequenceId((uint)Mathf.Max(0, expectedWordIndex.Value)),
                words,
                unlockedWordCount.Value,
                expectedWordIndex.Value);
            RitualOutcomeSnapshot outcome = new(
                new RitualOutcomeSequenceId(snapshotRevision.Value),
                turnId,
                latestRitualOutcome.Value,
                failureReason.Value,
                activeSeatId.Value);

            return new RitualSnapshot(
                ritualSessionId.Value,
                new RitualSequenceId(ritualSequence.Value),
                ritualPhase.Value,
                validationMode.Value,
                traversalDirection.Value,
                completedRotationCount.Value,
                activePlayerId.Value,
                turn,
                phrase,
                outcome,
                isGameOver.Value,
                winnerPlayerId.Value);
        }

        private void HandleSnapshotRevisionChanged(
            uint previousRevision,
            uint currentRevision,
            bool asServer)
        {
            snapshotNotificationPending = true;
        }

        private bool TryClaimDiscoveryReference()
        {
            if (ownsDiscoveryReference)
                return true;

            if (Instance != null && Instance != this)
            {
                Debug.LogError(
                    $"Duplicate {nameof(NetworkRitualAuthority)} detected. Exactly one active authority is supported per network ritual scene.",
                    this);
                enabled = false;
                return false;
            }

            Instance = this;
            ownsDiscoveryReference = true;
            return true;
        }

        private void ReleaseDiscoveryReference()
        {
            if (!ownsDiscoveryReference)
                return;

            if (Instance == this)
                Instance = null;

            ownsDiscoveryReference = false;
        }
    }
}
