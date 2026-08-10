using System;
using System.Collections.Generic;
using System.Text;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Incantation.Networking.Ritual;
using UnityEngine;

namespace Incantation.Networking
{
    /// <summary>
    /// Owns server-authoritative ritual state through deterministic turn outcome and consequence
    /// selection. Physical Book execution, speech recognition, consequence presentation, phrase
    /// progression, and elimination execution remain outside this component.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkBookAuthority))]
    public sealed class NetworkRitualAuthority : NetworkBehaviour
    {
        public const int NoPlayerId = -1;
        public const int NoSeatId = -1;
        public const string NoActivePlayerId = "";

        private const string DevelopmentSessionId = "DEV-RITUAL-0001";

        private readonly SyncVar<string> ritualSessionId = new(string.Empty);
        private readonly SyncVar<uint> ritualSequence = new(0);
        private readonly SyncVar<RitualPhase> ritualPhase = new(RitualPhase.Inactive);
        private readonly SyncVar<uint> turnSequence = new(0);
        private readonly SyncVar<string> activePlayerId = new(NoActivePlayerId);
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
        private readonly SyncVar<uint> timerSequence = new(0);
        private readonly SyncVar<uint> timerRitualSequence = new(0);
        private readonly SyncVar<uint> timerTurnSequence = new(0);
        private readonly SyncVar<double> activeTimerDuration = new(0d);
        private readonly SyncVar<double> timerRemainingTime = new(0d);
        private readonly SyncVar<bool> isTimerRunning = new(false);
        private readonly SyncVar<bool> isTimerExpired = new(false);
        private readonly SyncVar<uint> timerRevision = new(0);
        private readonly SyncVar<uint> acceptedVoiceRitualSequence = new(0);
        private readonly SyncVar<uint> acceptedVoiceTurnSequence = new(0);
        private readonly SyncVar<uint> acceptedVoiceSubmissionSequence = new(0);
        private readonly SyncVar<string> acceptedVoicePlayerId = new(string.Empty);
        private readonly SyncVar<string> acceptedVoiceRecognizedText = new(string.Empty);
        private readonly SyncVar<double> acceptedVoiceServerTimestamp = new(0d);
        private readonly SyncVar<uint> voiceSubmissionRevision = new(0);
        private readonly SyncVar<uint> validationSequence = new(0);
        private readonly SyncVar<uint> validationRitualSequence = new(0);
        private readonly SyncVar<uint> validationTurnSequence = new(0);
        private readonly SyncVar<uint> validationSubmissionSequence = new(0);
        private readonly SyncVar<string> validationPlayerId = new(string.Empty);
        private readonly SyncVar<RitualValidationMode> validatedMode =
            new(RitualValidationMode.WordByWordRealtime);
        private readonly SyncVar<RitualValidationResult> validationResult =
            new(RitualValidationResult.None);
        private readonly SyncVar<int> validationAcceptedWordCount = new(0);
        private readonly SyncVar<int> validationWordIndex = new(-1);
        private readonly SyncVar<int> validationRejectedWordIndex = new(-1);
        private readonly SyncVar<string> validationExpectedWord = new(string.Empty);
        private readonly SyncVar<string> validationRejectedWord = new(string.Empty);
        private readonly SyncVar<string> validationReceivedText = new(string.Empty);
        private readonly SyncVar<RitualValidationFailureReason> validationFailureReason =
            new(RitualValidationFailureReason.None);
        private readonly SyncVar<double> validationServerTimestamp = new(0d);
        private readonly SyncVar<uint> validationRevision = new(0);
        private readonly SyncVar<uint> turnOutcomeSequence = new(0);
        private readonly SyncVar<uint> turnOutcomeRitualSequence = new(0);
        private readonly SyncVar<uint> turnOutcomeTurnSequence = new(0);
        private readonly SyncVar<string> turnOutcomePlayerId = new(string.Empty);
        private readonly SyncVar<TurnOutcomeType> turnOutcomeType =
            new(TurnOutcomeType.None);
        private readonly SyncVar<uint> turnOutcomeValidationSequence = new(0);
        private readonly SyncVar<uint> turnOutcomeTimerSequence = new(0);
        private readonly SyncVar<double> turnOutcomeServerTimestamp = new(0d);
        private readonly SyncVar<uint> turnOutcomeRevision = new(0);
        private readonly SyncVar<uint> consequenceSequence = new(0);
        private readonly SyncVar<uint> consequenceRitualSequence = new(0);
        private readonly SyncVar<uint> consequenceTurnSequence = new(0);
        private readonly SyncVar<uint> consequenceOutcomeSequence = new(0);
        private readonly SyncVar<string> consequencePlayerId = new(string.Empty);
        private readonly SyncVar<RitualConsequenceType> consequenceType =
            new(RitualConsequenceType.None);
        private readonly SyncVar<double> consequenceServerTimestamp = new(0d);
        private readonly SyncVar<uint> consequenceRevision = new(0);
        private readonly SyncVar<RitualOutcome> latestRitualOutcome = new(RitualOutcome.None);
        private readonly SyncVar<RitualFailureReason> failureReason =
            new(RitualFailureReason.None);
        private readonly SyncVar<bool> isGameOver = new(false);
        private readonly SyncVar<int> winnerPlayerId = new(NoPlayerId);
        private readonly SyncVar<uint> completedRotationCount = new(0);
        private readonly SyncList<NetworkRitualRosterEntry> ritualRoster = new();
        private readonly SyncVar<uint> rosterRevision = new(0);
        private readonly SyncVar<uint> requestedBookMovementSequence = new(0);
        private readonly SyncVar<uint> requestedBookMovementTurnSequence = new(0);
        private readonly SyncVar<int> requestedBookTargetSeatId = new(NoSeatId);
        private readonly SyncVar<uint> acceptedBookArrivalMovementSequence = new(0);
        private readonly SyncVar<uint> acceptedBookArrivalRitualSequence = new(0);
        private readonly SyncVar<uint> acceptedBookArrivalTurnSequence = new(0);
        private readonly SyncVar<int> acceptedBookArrivalTargetSeatId = new(NoSeatId);
        private readonly SyncVar<string> acceptedBookArrivalPlayerId = new(string.Empty);
        private readonly SyncVar<double> acceptedBookArrivalNetworkTime = new(0d);
        private readonly SyncVar<uint> bookArrivalRevision = new(0);
        private readonly SyncVar<uint> snapshotRevision = new(0);

        [Header("Ritual Roster Source")]
        [SerializeField] private SeatManager seatManager;

        private bool snapshotNotificationPending;
        private bool rosterNotificationPending;
        private bool bookArrivalNotificationPending;
        private bool timerNotificationPending;
        private bool timerExpiredNotificationPending;
        private bool validationNotificationPending;
        private bool turnOutcomeNotificationPending;
        private bool consequenceNotificationPending;
        private bool ownsDiscoveryReference;
        private NetworkBookAuthority networkBookAuthority;
        private RitualController ritualController;

        public static NetworkRitualAuthority Instance { get; private set; }
        public bool IsNetworkSessionActive => IsServerInitialized || IsClientInitialized;
        public RitualSnapshot Snapshot => CreateSnapshot();
        public RitualRosterSnapshot Roster => CreateRosterSnapshot();
        public RitualRosterEntrySnapshot[] CurrentRoster => Roster.Entries;
        public RitualRosterEntrySnapshot[] ActivePlayers => Roster.ActivePlayers;
        public RitualRosterEntrySnapshot[] AlivePlayers => Roster.AlivePlayers;
        public RitualRosterEntrySnapshot[] EliminatedPlayers => Roster.EliminatedPlayers;
        public string CurrentActivePlayerId => activePlayerId.Value;
        public int CurrentActiveSeatId => activeSeatId.Value;
        public uint RequestedBookMovementSequence => requestedBookMovementSequence.Value;
        public RitualBookArrivalSnapshot BookArrival => CreateBookArrivalSnapshot();
        public RitualTimerSnapshot CurrentTimerSnapshot => CreateTimerSnapshot();
        public RitualVoiceSubmissionSnapshot LatestVoiceSubmission =>
            CreateVoiceSubmissionSnapshot();
        public RitualValidationSnapshot CurrentValidation =>
            CreateValidationSnapshot();
        public RitualValidationSnapshot LatestValidation =>
            CreateValidationSnapshot();
        public TurnOutcomeSnapshot CurrentTurnOutcome =>
            CreateCurrentTurnOutcomeSnapshot();
        public TurnOutcomeSnapshot LatestTurnOutcome =>
            CreateTurnOutcomeSnapshot();
        public RitualConsequenceSnapshot CurrentConsequence =>
            CreateCurrentConsequenceSnapshot();
        public RitualConsequenceSnapshot LatestConsequence =>
            CreateConsequenceSnapshot();
        public bool IsTimerRunning => isTimerRunning.Value;
        public double TimerDeadline => timerDeadlineNetworkTime.Value;
        public double RemainingTime => CalculateRemainingTime(GetCurrentNetworkTime());
        public RitualRosterEntrySnapshot? CurrentActiveParticipant =>
            TryGetCurrentActiveParticipant(out RitualRosterEntrySnapshot participant)
                ? participant
                : null;

        public event Action<RitualSnapshot> SnapshotChanged;
        public event Action<RitualRosterSnapshot> RosterChanged;
        public event Action<RitualBookArrivalSnapshot> BookArrivalAccepted;
        public event Action<RitualTimerSnapshot> TimerSnapshotChanged;
        public event Action<RitualTimerSnapshot> TimerExpired;
        public event Action<RitualVoiceSubmissionSnapshot> VoiceSubmissionAccepted;
        public event Action<RitualValidationSnapshot> ValidationSnapshotChanged;
        public event Action<RitualValidationSnapshot> ValidationAccepted;
        public event Action<RitualValidationSnapshot> ValidationRejected;
        public event Action<TurnOutcomeSnapshot> TurnOutcomeCommitted;
        public event Action<TurnOutcomeSnapshot> TurnOutcomeSnapshotChanged;
        public event Action<RitualConsequenceSnapshot> ConsequenceCommitted;
        public event Action<RitualConsequenceSnapshot> ConsequenceSnapshotChanged;

        private void OnEnable()
        {
            TryClaimDiscoveryReference();
            ResolveBookAuthority();
        }

        private void OnDisable()
        {
            ReleaseDiscoveryReference();
        }

        private void LateUpdate()
        {
            if ((!snapshotNotificationPending &&
                 !rosterNotificationPending &&
                 !bookArrivalNotificationPending &&
                 !timerNotificationPending &&
                 !timerExpiredNotificationPending &&
                 !validationNotificationPending &&
                 !turnOutcomeNotificationPending &&
                 !consequenceNotificationPending) ||
                !ownsDiscoveryReference)
                return;

            if (rosterNotificationPending)
            {
                rosterNotificationPending = false;
                RitualRosterSnapshot roster = CreateRosterSnapshot();
                RosterChanged?.Invoke(roster);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log(
                    $"[Ritual Roster] Version={roster.Version}, Direction={roster.TraversalDirection}, Entries={FormatRoster(roster)}.",
                    this);
#endif
            }

            if (snapshotNotificationPending)
            {
                snapshotNotificationPending = false;
                RitualSnapshot snapshot = CreateSnapshot();
                SnapshotChanged?.Invoke(snapshot);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log(
                    $"[Ritual Snapshot] Session={snapshot.RitualSessionId}, Ritual={snapshot.SequenceId.Value}, Phase={snapshot.Phase}, Roster={snapshot.Roster.Count}, Turn={snapshot.Turn.SequenceId.Value}, ActivePlayer={snapshot.ActivePlayerId}, ActiveSeat={snapshot.Turn.ActiveSeatId}, Direction={snapshot.TraversalDirection}, ArrivalMovement={snapshot.BookArrival.MovementSequence}, ArrivalSeat={snapshot.BookArrival.TargetSeatId}, TimerSequence={snapshot.Timer.TimerSequence}, TimerRunning={snapshot.Timer.IsRunning}, TimerExpired={snapshot.Timer.IsExpired}, TimerRemaining={snapshot.Timer.RemainingTime}, VoiceSubmission={snapshot.VoiceSubmission.SubmissionSequence}, VoicePlayer={snapshot.VoiceSubmission.PlayerId}, Validation={snapshot.ValidationMode}, Phrase={snapshot.Phrase.SequenceId.Value}, Unlocked={snapshot.Phrase.UnlockedWordCount}, Expected={snapshot.Phrase.ExpectedWordIndex}, TurnOutcome={snapshot.TurnOutcome.OutcomeType}, TurnOutcomeSequence={snapshot.TurnOutcome.OutcomeSequenceId.Value}, Consequence={snapshot.Consequence.ConsequenceType}, ConsequenceSequence={snapshot.Consequence.ConsequenceSequenceId.Value}, Start={snapshot.Turn.StartedAtNetworkTime}, Deadline={snapshot.Turn.EndsAtNetworkTime}, Outcome={snapshot.Outcome.Outcome}, Failure={snapshot.Outcome.FailureReason}, GameOver={snapshot.IsGameOver}, Winner={snapshot.WinnerPlayerId}.",
                    this);
#endif
            }

            if (bookArrivalNotificationPending)
            {
                bookArrivalNotificationPending = false;
                BookArrivalAccepted?.Invoke(CreateBookArrivalSnapshot());
            }

            if (timerNotificationPending)
            {
                timerNotificationPending = false;
                TimerSnapshotChanged?.Invoke(CreateTimerSnapshot());
            }

            if (timerExpiredNotificationPending)
            {
                timerExpiredNotificationPending = false;
                TimerExpired?.Invoke(CreateTimerSnapshot());
            }

            if (validationNotificationPending)
            {
                validationNotificationPending = false;
                ValidationSnapshotChanged?.Invoke(CreateValidationSnapshot());
            }

            if (turnOutcomeNotificationPending)
            {
                turnOutcomeNotificationPending = false;
                TurnOutcomeSnapshotChanged?.Invoke(
                    CreateCurrentTurnOutcomeSnapshot());
            }

            if (consequenceNotificationPending)
            {
                consequenceNotificationPending = false;
                ConsequenceSnapshotChanged?.Invoke(
                    CreateCurrentConsequenceSnapshot());
            }
        }

        private void Update()
        {
            if (!IsServerInitialized || !isTimerRunning.Value)
                return;

            if (GetCurrentNetworkTime() >= timerDeadlineNetworkTime.Value)
                TryCommitTimerExpired();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            if (!TryClaimDiscoveryReference())
                return;

            rosterRevision.OnChange += HandleRosterRevisionChanged;
            bookArrivalRevision.OnChange += HandleBookArrivalRevisionChanged;
            timerRevision.OnChange += HandleTimerRevisionChanged;
            validationRevision.OnChange += HandleValidationRevisionChanged;
            turnOutcomeRevision.OnChange += HandleTurnOutcomeRevisionChanged;
            consequenceRevision.OnChange += HandleConsequenceRevisionChanged;
            snapshotRevision.OnChange += HandleSnapshotRevisionChanged;
            rosterNotificationPending = true;
            snapshotNotificationPending = true;
            validationNotificationPending = true;
            turnOutcomeNotificationPending = true;
            consequenceNotificationPending = true;
        }

        public override void OnStopNetwork()
        {
            rosterRevision.OnChange -= HandleRosterRevisionChanged;
            bookArrivalRevision.OnChange -= HandleBookArrivalRevisionChanged;
            timerRevision.OnChange -= HandleTimerRevisionChanged;
            validationRevision.OnChange -= HandleValidationRevisionChanged;
            turnOutcomeRevision.OnChange -= HandleTurnOutcomeRevisionChanged;
            consequenceRevision.OnChange -= HandleConsequenceRevisionChanged;
            snapshotRevision.OnChange -= HandleSnapshotRevisionChanged;
            rosterNotificationPending = false;
            snapshotNotificationPending = false;
            bookArrivalNotificationPending = false;
            timerNotificationPending = false;
            timerExpiredNotificationPending = false;
            validationNotificationPending = false;
            turnOutcomeNotificationPending = false;
            consequenceNotificationPending = false;
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
            timerSequence.Value = 0;
            timerRitualSequence.Value = 0;
            timerTurnSequence.Value = 0;
            activeTimerDuration.Value = 90d;
            timerRemainingTime.Value = 90d;
            isTimerRunning.Value = false;
            isTimerExpired.Value = false;
            acceptedVoiceRitualSequence.Value = 0;
            acceptedVoiceTurnSequence.Value = 0;
            acceptedVoiceSubmissionSequence.Value = 0;
            acceptedVoicePlayerId.Value = string.Empty;
            acceptedVoiceRecognizedText.Value = string.Empty;
            acceptedVoiceServerTimestamp.Value = 0d;
            validationSequence.Value = 0;
            validationRitualSequence.Value = 0;
            validationTurnSequence.Value = 0;
            validationSubmissionSequence.Value = 0;
            validationPlayerId.Value = string.Empty;
            validatedMode.Value = RitualValidationMode.WordByWordRealtime;
            validationResult.Value = RitualValidationResult.None;
            validationAcceptedWordCount.Value = 0;
            validationWordIndex.Value = -1;
            validationRejectedWordIndex.Value = -1;
            validationExpectedWord.Value = string.Empty;
            validationRejectedWord.Value = string.Empty;
            validationReceivedText.Value = string.Empty;
            validationFailureReason.Value = RitualValidationFailureReason.None;
            validationServerTimestamp.Value = 0d;
            turnOutcomeSequence.Value = 0;
            turnOutcomeRitualSequence.Value = 0;
            turnOutcomeTurnSequence.Value = 0;
            turnOutcomePlayerId.Value = string.Empty;
            turnOutcomeType.Value = TurnOutcomeType.None;
            turnOutcomeValidationSequence.Value = 0;
            turnOutcomeTimerSequence.Value = 0;
            turnOutcomeServerTimestamp.Value = 0d;
            consequenceSequence.Value = 0;
            consequenceRitualSequence.Value = 0;
            consequenceTurnSequence.Value = 0;
            consequenceOutcomeSequence.Value = 0;
            consequencePlayerId.Value = string.Empty;
            consequenceType.Value = RitualConsequenceType.None;
            consequenceServerTimestamp.Value = 0d;
            latestRitualOutcome.Value = RitualOutcome.None;
            failureReason.Value = RitualFailureReason.None;
            isGameOver.Value = false;
            winnerPlayerId.Value = NoPlayerId;
            completedRotationCount.Value = 0;
            snapshotRevision.Value++;
            return true;
        }

        /// <summary>
        /// Commits the next active and alive roster entry. This is the only method that selects
        /// or writes the authoritative active player and Seat.
        /// </summary>
        public bool TryCommitNextActiveParticipant()
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning(
                    "[RitualAuthority] Rejected active participant commit: only the server may choose participants.",
                    this);
                return false;
            }

            RitualRosterSnapshot roster = CreateRosterSnapshot();
            RitualRosterEntrySnapshot[] entries = roster.Entries;
            if (entries.Length == 0)
            {
                Debug.LogWarning(
                    "[RitualAuthority] Rejected active participant commit: the authoritative roster is empty.",
                    this);
                return false;
            }

            if (roster.Version == 0)
            {
                Debug.LogError(
                    "[RitualAuthority] Rejected active participant commit: the roster has no authoritative traversal revision.",
                    this);
                return false;
            }

            if (roster.TraversalDirection != traversalDirection.Value)
            {
                Debug.LogError(
                    $"[RitualAuthority] Rejected active participant commit: roster traversal {roster.TraversalDirection} does not match authority traversal {traversalDirection.Value}.",
                    this);
                return false;
            }

            if (!TryValidateRosterForTraversal(entries))
                return false;

            if (!TryFindCurrentParticipantIndex(entries, out int currentIndex))
                return false;

            int nextIndex = FindNextEligibleParticipantIndex(entries, currentIndex);
            if (nextIndex < 0)
            {
                Debug.LogWarning(
                    "[RitualAuthority] Rejected active participant commit: no active, alive participant exists.",
                    this);
                return false;
            }

            if (turnSequence.Value == uint.MaxValue)
            {
                Debug.LogError(
                    "[RitualAuthority] Rejected active participant commit: turn sequence is exhausted.",
                    this);
                return false;
            }

            RitualRosterEntrySnapshot nextParticipant = entries[nextIndex];
            uint nextTurnSequence = turnSequence.Value + 1;

            activePlayerId.Value = nextParticipant.PlayerId;
            activeSeatId.Value = nextParticipant.SeatId;
            turnSequence.Value = nextTurnSequence;
            snapshotRevision.Value++;

            Debug.Log(
                "[RitualAuthority]\n" +
                "Committed Active Participant\n" +
                $"PlayerId = {nextParticipant.PlayerId}\n" +
                $"SeatId = {nextParticipant.SeatId}\n" +
                $"TurnSequence = {nextTurnSequence}\n" +
                "TraversalReason = NextAliveParticipant",
                this);
            return true;
        }

        /// <summary>
        /// Issues the one semantic Book command permitted for the current authoritative turn.
        /// Physical movement and arrival remain owned by NetworkBookAuthority.
        /// </summary>
        public bool TryRequestBookMoveToCurrentParticipant()
        {
            if (!IsServerInitialized)
                return RejectBookMovement("Only the server may request Book movement.");

            RitualRosterSnapshot roster = CreateRosterSnapshot();
            if (roster.Version == 0 || roster.Count == 0)
                return RejectBookMovement("The authoritative ritual roster is unavailable.");

            if (!TryGetCurrentActiveParticipant(
                    out RitualRosterEntrySnapshot participant))
            {
                return RejectBookMovement(
                    "The authoritative active participant is unavailable or inconsistent.");
            }

            if (!participant.IsActive || !participant.IsAlive)
            {
                return RejectBookMovement(
                    $"Player {participant.PlayerId} is not an active, alive participant.");
            }

            if (seatManager == null ||
                seatManager.GetSeatById(participant.SeatId) == null)
            {
                return RejectBookMovement(
                    $"Target Seat ID {participant.SeatId} is not configured.");
            }

            if (turnSequence.Value == 0)
                return RejectBookMovement("The authoritative turn sequence is not initialized.");

            if (requestedBookMovementSequence.Value > 0 &&
                requestedBookMovementTurnSequence.Value == turnSequence.Value)
            {
                return RejectBookMovement(
                    $"Turn {turnSequence.Value} already has Book movement request {requestedBookMovementSequence.Value}.");
            }

            if (requestedBookMovementSequence.Value == uint.MaxValue)
                return RejectBookMovement("The Book movement sequence is exhausted.");

            NetworkBookAuthority resolvedBookAuthority = ResolveBookAuthority();
            if (resolvedBookAuthority == null)
                return RejectBookMovement("NetworkBookAuthority is unavailable.");

            uint nextMovementSequence = requestedBookMovementSequence.Value + 1;
            RitualBookMovementCommand command = new(
                nextMovementSequence,
                ritualSequence.Value,
                turnSequence.Value,
                participant.SeatId,
                participant.PlayerId);

            if (!resolvedBookAuthority.TryExecuteMovementCommand(command))
            {
                return RejectBookMovement(
                    $"NetworkBookAuthority rejected movement request {nextMovementSequence}.");
            }

            requestedBookMovementSequence.Value = nextMovementSequence;
            requestedBookMovementTurnSequence.Value = turnSequence.Value;
            requestedBookTargetSeatId.Value = participant.SeatId;
            snapshotRevision.Value++;

            Debug.Log(
                "[RitualAuthority]\n" +
                "Book Movement Requested\n" +
                $"MovementSequence = {nextMovementSequence}\n" +
                $"RitualSequence = {ritualSequence.Value}\n" +
                $"TurnSequence = {turnSequence.Value}\n" +
                $"TargetSeat = {participant.SeatId}\n" +
                $"PlayerId = {participant.PlayerId}",
                this);
            return true;
        }

        /// <summary>
        /// Temporary stable-ID bridge for the legacy ritual flow. The requested Seat is treated
        /// only as an expectation; authoritative traversal selects participants until it reaches
        /// that Seat, then the normal command path decides whether movement may be issued.
        /// </summary>
        public bool TryForwardLegacyBookMoveRequest(int requestedSeatId)
        {
            if (!IsServerInitialized)
                return RejectBookMovement("A client attempted to forward legacy Book movement.");

            if (seatManager == null || seatManager.GetSeatById(requestedSeatId) == null)
                return RejectBookMovement($"Legacy target Seat ID {requestedSeatId} is invalid.");

            if (ritualRoster.Count == 0 && !TryBuildRosterFromCurrentSeating())
                return RejectBookMovement("The legacy bridge could not establish the ritual roster.");

            RitualRosterSnapshot roster = CreateRosterSnapshot();
            if (!roster.TryGetPlayerBySeat(
                    requestedSeatId,
                    out RitualRosterEntrySnapshot requestedParticipant) ||
                !requestedParticipant.IsActive ||
                !requestedParticipant.IsAlive)
            {
                return RejectBookMovement(
                    $"Legacy target Seat ID {requestedSeatId} has no eligible roster participant.");
            }

            for (int attempt = 0;
                 attempt < roster.Count && CurrentActiveSeatId != requestedSeatId;
                 attempt++)
            {
                if (!TryCommitNextActiveParticipant())
                    return RejectBookMovement("The legacy bridge could not select a participant.");
            }

            if (CurrentActiveSeatId != requestedSeatId)
            {
                return RejectBookMovement(
                    $"Authoritative traversal did not select legacy target Seat ID {requestedSeatId}.");
            }

            return TryRequestBookMoveToCurrentParticipant();
        }

        /// <summary>
        /// Accepts one server-detected completion for the current authoritative movement.
        /// This publishes arrival state only; it does not start gameplay or advance the ritual.
        /// </summary>
        public bool TryCommitBookArrival(RitualBookArrivalReport report)
        {
            if (!IsServerInitialized)
                return RejectBookArrival("Only the server may commit Book arrival.");

            if (report.MovementSequence == 0)
                return RejectBookArrival("Movement sequence 0 is invalid.");

            if (report.MovementSequence == acceptedBookArrivalMovementSequence.Value)
            {
                return RejectBookArrival(
                    $"Movement {report.MovementSequence} arrival was already accepted.");
            }

            if (report.MovementSequence < requestedBookMovementSequence.Value)
            {
                return RejectBookArrival(
                    $"Movement {report.MovementSequence} is stale; current request is {requestedBookMovementSequence.Value}.");
            }

            if (report.MovementSequence != requestedBookMovementSequence.Value)
            {
                return RejectBookArrival(
                    $"Movement {report.MovementSequence} does not match current request {requestedBookMovementSequence.Value}.");
            }

            if (report.TurnSequence != requestedBookMovementTurnSequence.Value ||
                report.TurnSequence != turnSequence.Value)
            {
                return RejectBookArrival(
                    $"Turn {report.TurnSequence} does not match requested turn {requestedBookMovementTurnSequence.Value} and active turn {turnSequence.Value}.");
            }

            if (report.TargetSeatId != requestedBookTargetSeatId.Value ||
                report.TargetSeatId != activeSeatId.Value)
            {
                return RejectBookArrival(
                    $"Target Seat {report.TargetSeatId} does not match requested Seat {requestedBookTargetSeatId.Value} and active Seat {activeSeatId.Value}.");
            }

            if (report.RitualSequence != ritualSequence.Value)
            {
                return RejectBookArrival(
                    $"Ritual {report.RitualSequence} does not match active ritual {ritualSequence.Value}.");
            }

            if (ritualSequence.Value == 0 ||
                ritualPhase.Value == RitualPhase.Inactive)
            {
                return RejectBookArrival(
                    $"The authoritative ritual lifecycle is not active. " +
                    $"RitualSequence={ritualSequence.Value}, " +
                    $"Phase={ritualPhase.Value}.");
            }

            if (double.IsNaN(report.CompletionNetworkTime) ||
                double.IsInfinity(report.CompletionNetworkTime) ||
                report.CompletionNetworkTime < 0d)
            {
                return RejectBookArrival(
                    $"Completion time {report.CompletionNetworkTime} is invalid.");
            }

            if (!TryGetCurrentActiveParticipant(
                    out RitualRosterEntrySnapshot participant) ||
                !participant.IsActive ||
                !participant.IsAlive ||
                participant.SeatId != report.TargetSeatId)
            {
                return RejectBookArrival(
                    "The completion does not belong to the current active, alive participant.");
            }

            if (isTimerRunning.Value)
                return RejectBookArrival("A ritual timer is already running.");

            if (timerSequence.Value == uint.MaxValue)
                return RejectBookArrival("The ritual timer sequence is exhausted.");

            RitualController controller = ResolveRitualController();
            if (controller == null)
            {
                return RejectBookArrival(
                    "The configured ritual turn duration is unavailable because RitualController could not be resolved.");
            }

            double configuredDuration = controller.ConfiguredTurnDuration;
            if (double.IsNaN(configuredDuration) ||
                double.IsInfinity(configuredDuration) ||
                configuredDuration <= 0d)
            {
                return RejectBookArrival(
                    $"Configured timer duration {configuredDuration} is invalid.");
            }

            acceptedBookArrivalMovementSequence.Value = report.MovementSequence;
            acceptedBookArrivalRitualSequence.Value = report.RitualSequence;
            acceptedBookArrivalTurnSequence.Value = report.TurnSequence;
            acceptedBookArrivalTargetSeatId.Value = report.TargetSeatId;
            acceptedBookArrivalPlayerId.Value = participant.PlayerId;
            acceptedBookArrivalNetworkTime.Value = report.CompletionNetworkTime;
            bookArrivalRevision.Value++;
            snapshotRevision.Value++;

            Debug.Log(
                "[RitualAuthority]\n" +
                "Book Arrival Accepted\n" +
                $"MovementSequence = {report.MovementSequence}\n" +
                $"RitualSequence = {report.RitualSequence}\n" +
                $"TurnSequence = {report.TurnSequence}\n" +
                $"Seat = {report.TargetSeatId}\n" +
                $"Player = {participant.PlayerId}\n" +
                $"CompletionTime = {report.CompletionNetworkTime}",
                this);
            return TryStartTimerForArrival(report, participant, configuredDuration);
        }

        /// <summary>
        /// Requests a server-owned stop for the current turn timer. External callers cannot
        /// write timer state and clients have no mutation path.
        /// </summary>
        public bool TryStopTimerForCurrentTurn()
        {
            if (!IsServerInitialized)
                return RejectTimer("Only the server may stop ritual time.");

            if (!isTimerRunning.Value)
                return RejectTimer("The ritual timer is not running.");

            if (timerTurnSequence.Value != turnSequence.Value)
            {
                return RejectTimer(
                    $"Timer turn {timerTurnSequence.Value} does not match active turn {turnSequence.Value}.");
            }

            timerRemainingTime.Value = CalculateRemainingTime(GetCurrentNetworkTime());
            isTimerRunning.Value = false;
            isTimerExpired.Value = false;
            timerRevision.Value++;
            snapshotRevision.Value++;

            Debug.Log(
                "[RitualAuthority]\n" +
                "Timer Stopped\n" +
                $"TimerSequence = {timerSequence.Value}\n" +
                $"TurnSequence = {timerTurnSequence.Value}\n" +
                $"RemainingTime = {timerRemainingTime.Value}",
                this);
            return true;
        }

        /// <summary>
        /// Validates recognized speech against server-owned sender, roster, turn, arrival, and
        /// timer state. This accepts input only and performs no phrase judgment.
        /// </summary>
        public bool TryAcceptVoiceSubmission(
            NetworkConnection sender,
            RitualVoiceSubmission submission)
        {
            int connectionId = sender != null ? sender.ClientId : -1;
            if (!IsServerInitialized)
            {
                return RejectVoiceSubmission(
                    "Only the server may accept recognized speech.",
                    connectionId,
                    submission);
            }

            if (sender == null ||
                !sender.IsActive ||
                !sender.IsAuthenticated)
            {
                return RejectVoiceSubmission(
                    "The sending connection is missing, inactive, or unauthenticated.",
                    connectionId,
                    submission);
            }

            RitualRosterSnapshot roster = CreateRosterSnapshot();
            if (roster.Version == 0 || roster.Count == 0)
            {
                return RejectVoiceSubmission(
                    "The authoritative ritual roster is unavailable.",
                    connectionId,
                    submission);
            }

            if (!TryResolvePlayerForConnection(
                    sender,
                    out NetworkPlayer submittingNetworkPlayer,
                    out string connectionResolutionReason))
            {
                return RejectVoiceSubmission(
                    connectionResolutionReason,
                    connectionId,
                    submission);
            }

            if (!roster.TryGetSeatByPlayer(
                    submittingNetworkPlayer.PlayerId,
                    out int submittingSeatId) ||
                !roster.TryGetPlayerBySeat(
                    submittingSeatId,
                    out RitualRosterEntrySnapshot submittingParticipant))
            {
                return RejectVoiceSubmission(
                    "The sending connection does not resolve to a locked roster participant.",
                    connectionId,
                    submission);
            }

            if (!submittingParticipant.IsActive || !submittingParticipant.IsAlive)
            {
                return RejectVoiceSubmission(
                    "The submitting roster participant is inactive or not alive.",
                    connectionId,
                    submission);
            }

            if (!string.Equals(
                    submittingParticipant.PlayerId,
                    activePlayerId.Value,
                    StringComparison.Ordinal) ||
                submittingParticipant.SeatId != activeSeatId.Value)
            {
                return RejectVoiceSubmission(
                    "Only the current active participant may submit recognized speech.",
                    connectionId,
                    submission);
            }

            if (submission.RitualSequence != ritualSequence.Value)
            {
                return RejectVoiceSubmission(
                    $"Ritual {submission.RitualSequence} does not match active ritual {ritualSequence.Value}.",
                    connectionId,
                    submission);
            }

            if (submission.TurnSequence != turnSequence.Value)
            {
                return RejectVoiceSubmission(
                    $"Turn {submission.TurnSequence} does not match active turn {turnSequence.Value}.",
                    connectionId,
                    submission);
            }

            if (!BookArrival.HasArrived ||
                BookArrival.RitualSequenceId.Value != ritualSequence.Value ||
                BookArrival.TurnSequenceId.Value != turnSequence.Value ||
                BookArrival.TargetSeatId != activeSeatId.Value)
            {
                return RejectVoiceSubmission(
                    "Authoritative Book arrival is not accepted for the active turn.",
                    connectionId,
                    submission);
            }

            double serverTimestamp = GetCurrentNetworkTime();
            if (!isTimerRunning.Value ||
                isTimerExpired.Value ||
                timerRitualSequence.Value != ritualSequence.Value ||
                timerTurnSequence.Value != turnSequence.Value)
            {
                return RejectVoiceSubmission(
                    "The authoritative timer is not running for the active ritual turn.",
                    connectionId,
                    submission);
            }

            if (serverTimestamp >= timerDeadlineNetworkTime.Value)
            {
                return RejectVoiceSubmission(
                    $"The authoritative deadline {timerDeadlineNetworkTime.Value} has passed.",
                    connectionId,
                    submission);
            }

            if (submission.SubmissionSequence == 0)
            {
                return RejectVoiceSubmission(
                    "Submission sequence 0 is invalid.",
                    connectionId,
                    submission);
            }

            if (acceptedVoiceTurnSequence.Value == submission.TurnSequence &&
                submission.SubmissionSequence <=
                    acceptedVoiceSubmissionSequence.Value)
            {
                return RejectVoiceSubmission(
                    $"Submission sequence {submission.SubmissionSequence} is duplicate or older than accepted sequence {acceptedVoiceSubmissionSequence.Value}.",
                    connectionId,
                    submission);
            }

            if (double.IsNaN(submission.ClientCaptureTimestamp) ||
                double.IsInfinity(submission.ClientCaptureTimestamp) ||
                submission.ClientCaptureTimestamp < 0d)
            {
                return RejectVoiceSubmission(
                    "Client capture timestamp is malformed.",
                    connectionId,
                    submission);
            }

            if (!TryNormalizeRecognizedText(
                    submission.RecognizedText,
                    out string normalizedRecognizedText,
                    out string contentRejectionReason))
            {
                return RejectVoiceSubmission(
                    contentRejectionReason,
                    connectionId,
                    submission);
            }

            RitualVoiceSubmissionSnapshot acceptedSubmission = new(
                new RitualSequenceId(submission.RitualSequence),
                new RitualTurnSequenceId(submission.TurnSequence),
                submission.SubmissionSequence,
                submittingParticipant.PlayerId,
                normalizedRecognizedText,
                serverTimestamp);

            acceptedVoiceRitualSequence.Value = submission.RitualSequence;
            acceptedVoiceTurnSequence.Value = submission.TurnSequence;
            acceptedVoiceSubmissionSequence.Value = submission.SubmissionSequence;
            acceptedVoicePlayerId.Value = submittingParticipant.PlayerId;
            acceptedVoiceRecognizedText.Value = normalizedRecognizedText;
            acceptedVoiceServerTimestamp.Value = serverTimestamp;
            voiceSubmissionRevision.Value++;
            snapshotRevision.Value++;

            Debug.Log(
                "[RitualAuthority]\n" +
                "Voice Submission Accepted\n" +
                $"PlayerId = {submittingParticipant.PlayerId}\n" +
                $"ConnectionId = {connectionId}\n" +
                $"TurnSequence = {submission.TurnSequence}\n" +
                $"SubmissionSequence = {submission.SubmissionSequence}\n" +
                $"RecognizedText = {normalizedRecognizedText}\n" +
                $"ServerTimestamp = {serverTimestamp}",
                this);

            TryValidateAcceptedVoiceSubmission(acceptedSubmission);
            VoiceSubmissionAccepted?.Invoke(acceptedSubmission);
            PublishAcceptedVoiceSubmissionObserversRpc(
                submission.RitualSequence,
                submission.TurnSequence,
                submission.SubmissionSequence,
                submittingParticipant.PlayerId,
                normalizedRecognizedText,
                serverTimestamp);
            return true;
        }

        private bool TryValidateAcceptedVoiceSubmission(
            RitualVoiceSubmissionSnapshot submission)
        {
            if (!IsServerInitialized)
                return RejectValidation("Only the server may validate ritual speech.", submission);

            RitualVoiceSubmissionSnapshot acceptedSubmission =
                CreateVoiceSubmissionSnapshot();
            if (!acceptedSubmission.HasSubmission ||
                acceptedSubmission.RitualSequenceId.Value !=
                    submission.RitualSequenceId.Value ||
                acceptedSubmission.TurnSequenceId.Value !=
                    submission.TurnSequenceId.Value ||
                acceptedSubmission.SubmissionSequence !=
                    submission.SubmissionSequence ||
                !string.Equals(
                    acceptedSubmission.PlayerId,
                    submission.PlayerId,
                    StringComparison.Ordinal))
            {
                return RejectValidation(
                    "The accepted voice submission does not match authoritative state.",
                    submission);
            }

            if (validationTurnSequence.Value == submission.TurnSequenceId.Value &&
                submission.SubmissionSequence <=
                    validationSubmissionSequence.Value)
            {
                return RejectValidation(
                    "A validation already exists for this or a newer submission.",
                    submission);
            }

            if (submission.RitualSequenceId.Value != ritualSequence.Value ||
                submission.TurnSequenceId.Value != turnSequence.Value)
            {
                return RejectValidation(
                    "The accepted voice submission is stale for the current ritual turn.",
                    submission);
            }

            if (!TryGetCurrentActiveParticipant(
                    out RitualRosterEntrySnapshot participant) ||
                !string.Equals(
                    participant.PlayerId,
                    submission.PlayerId,
                    StringComparison.Ordinal))
            {
                return RejectValidation(
                    "The accepted voice submission is not owned by the active participant.",
                    submission);
            }

            RitualBookArrivalSnapshot arrival = CreateBookArrivalSnapshot();
            if (!arrival.HasArrived ||
                arrival.RitualSequenceId.Value != ritualSequence.Value ||
                arrival.TurnSequenceId.Value != turnSequence.Value ||
                arrival.TargetSeatId != activeSeatId.Value)
            {
                return RejectValidation(
                    "Authoritative Book arrival is unavailable for validation.",
                    submission);
            }

            double serverTimestamp = GetCurrentNetworkTime();
            if (!isTimerRunning.Value ||
                isTimerExpired.Value ||
                serverTimestamp >= timerDeadlineNetworkTime.Value)
            {
                return RejectValidation(
                    "The authoritative timer window is closed.",
                    submission);
            }

            RitualController controller = ResolveRitualController();
            IncantationManager manager =
                controller != null ? controller.CurrentIncantationManager : null;
            if (manager == null || manager.CurrentIncantation.Count == 0)
            {
                return RejectValidation(
                    "The server ritual phrase is unavailable.",
                    submission);
            }

            RitualValidationMode mode = controller.CurrentNetworkValidationMode;
            int validatedWordIndex = manager.CurrentWordIndex;
            PhraseValidationResult deterministicResult =
                mode == RitualValidationMode.FullPhrase
                    ? manager.EvaluateCurrentPhrase(
                        submission.RecognizedText,
                        controller.CurrentVoicePhraseNormalizer)
                    : manager.EvaluateCurrentWordRealtime(
                        submission.RecognizedText,
                        controller.CurrentVoicePhraseNormalizer);

            if (validationSequence.Value == uint.MaxValue)
            {
                return RejectValidation(
                    "The validation sequence is exhausted.",
                    submission);
            }

            CaptureAuthoritativePhrase(manager, mode);

            int rejectedWordIndex = deterministicResult.FirstFailedWordIndex;
            string expectedWord = string.Empty;
            string rejectedWord = string.Empty;
            if (mode == RitualValidationMode.WordByWordRealtime &&
                validatedWordIndex >= 0 &&
                validatedWordIndex < manager.CurrentIncantation.Count)
            {
                expectedWord =
                    manager.CurrentIncantation[validatedWordIndex]?.Text ??
                    string.Empty;
            }

            if (rejectedWordIndex >= 0)
            {
                foreach (PhraseValidationWordResult wordResult in
                    deterministicResult.WordTimeline)
                {
                    if (wordResult.WordIndex != rejectedWordIndex)
                        continue;

                    expectedWord = wordResult.ExpectedWord;
                    rejectedWord = wordResult.RecognizedWord;
                    break;
                }
            }

            uint nextValidationSequence = validationSequence.Value + 1;
            RitualValidationResult result = deterministicResult.IsSuccess
                ? RitualValidationResult.Accepted
                : RitualValidationResult.Rejected;
            RitualValidationFailureReason mappedFailureReason =
                MapValidationFailureReason(deterministicResult.FailureReason);

            validationSequence.Value = nextValidationSequence;
            validationRitualSequence.Value = submission.RitualSequenceId.Value;
            validationTurnSequence.Value = submission.TurnSequenceId.Value;
            validationSubmissionSequence.Value = submission.SubmissionSequence;
            validationPlayerId.Value = submission.PlayerId;
            validatedMode.Value = mode;
            validationResult.Value = result;
            validationAcceptedWordCount.Value =
                deterministicResult.MatchedWordCount;
            validationWordIndex.Value = validatedWordIndex;
            validationRejectedWordIndex.Value = rejectedWordIndex;
            validationExpectedWord.Value = expectedWord;
            validationRejectedWord.Value = rejectedWord;
            validationReceivedText.Value = submission.RecognizedText;
            validationFailureReason.Value = mappedFailureReason;
            validationServerTimestamp.Value = serverTimestamp;
            validationRevision.Value++;
            snapshotRevision.Value++;

            RitualValidationSnapshot validation = CreateValidationSnapshot();
            LogValidationCommitted(validation);
            PublishValidation(validation);
            PublishValidationObserversRpc(
                validation.RitualSequenceId.Value,
                validation.TurnSequenceId.Value,
                validation.SubmissionSequence,
                validation.ValidationSequence,
                validation.PlayerId,
                validation.ValidationMode,
                validation.Result,
                validation.AcceptedWordCount,
                validation.ValidatedWordIndex,
                validation.FirstRejectedWordIndex,
                validation.PhraseWords,
                validation.ExpectedWordIndex,
                validation.ExpectedWord,
                validation.RejectedWord,
                validation.ReceivedText,
                validation.FailureReason,
                validation.ServerTimestamp);

            if (IsPhraseCompletionValidation(validation))
            {
                TryCommitTurnOutcome(
                    TurnOutcomeType.Success,
                    validation.ValidationSequence,
                    0);
            }

            return true;
        }

        [ObserversRpc(ExcludeServer = true)]
        private void PublishValidationObserversRpc(
            uint validatedRitualSequence,
            uint validatedTurnSequence,
            uint validatedSubmissionSequence,
            uint committedValidationSequence,
            string derivedPlayerId,
            RitualValidationMode mode,
            RitualValidationResult result,
            int acceptedWordCount,
            int validatedWordIndex,
            int firstRejectedWordIndex,
            string[] authoritativePhraseWords,
            int authoritativeExpectedWordIndex,
            string expectedWord,
            string rejectedWord,
            string receivedText,
            RitualValidationFailureReason mappedFailureReason,
            double serverTimestamp)
        {
            PublishValidation(new RitualValidationSnapshot(
                new RitualSequenceId(validatedRitualSequence),
                new RitualTurnSequenceId(validatedTurnSequence),
                validatedSubmissionSequence,
                committedValidationSequence,
                derivedPlayerId,
                mode,
                result,
                acceptedWordCount,
                validatedWordIndex,
                firstRejectedWordIndex,
                authoritativePhraseWords,
                authoritativeExpectedWordIndex,
                expectedWord,
                rejectedWord,
                receivedText,
                mappedFailureReason,
                serverTimestamp));
        }

        private bool TryCommitTurnOutcome(
            TurnOutcomeType requestedOutcome,
            uint originatingValidationSequence,
            uint originatingTimerSequence)
        {
            if (!IsServerInitialized)
            {
                return RejectTurnOutcome(
                    "Only the server may commit a turn outcome.");
            }

            if (ritualSequence.Value == 0 || turnSequence.Value == 0)
            {
                return RejectTurnOutcome(
                    $"The current ritual or turn sequence is unavailable. " +
                    $"RitualSequence={ritualSequence.Value}, " +
                    $"TurnSequence={turnSequence.Value}, " +
                    $"Phase={ritualPhase.Value}, " +
                    $"RequestedOutcome={requestedOutcome}, " +
                    $"OriginatingTimer={originatingTimerSequence}.");
            }

            if (!TryGetCurrentActiveParticipant(
                    out RitualRosterEntrySnapshot participant) ||
                !participant.IsActive ||
                !participant.IsAlive ||
                !string.Equals(
                    participant.PlayerId,
                    activePlayerId.Value,
                    StringComparison.Ordinal))
            {
                return RejectTurnOutcome(
                    "The authoritative active participant is unavailable.");
            }

            TurnOutcomeSnapshot existingOutcome = CreateTurnOutcomeSnapshot();
            if (existingOutcome.HasOutcome &&
                existingOutcome.RitualSequenceId.Value ==
                    ritualSequence.Value &&
                existingOutcome.TurnSequenceId.Value == turnSequence.Value)
            {
                return RejectTurnOutcome(
                    $"Turn {turnSequence.Value} already ended with {existingOutcome.OutcomeType}.");
            }

            if (requestedOutcome == TurnOutcomeType.Success)
            {
                RitualValidationSnapshot validation =
                    CreateValidationSnapshot();
                if (!validation.HasValidation ||
                    !validation.IsAccepted ||
                    validation.RitualSequenceId.Value !=
                        ritualSequence.Value ||
                    validation.TurnSequenceId.Value !=
                        turnSequence.Value ||
                    validation.ValidationSequence !=
                        originatingValidationSequence ||
                    !string.Equals(
                        validation.PlayerId,
                        participant.PlayerId,
                        StringComparison.Ordinal) ||
                    !IsPhraseCompletionValidation(validation))
                {
                    return RejectTurnOutcome(
                        "The originating successful validation is invalid or incomplete.");
                }
            }
            else if (requestedOutcome == TurnOutcomeType.TimerExpired)
            {
                if (!isTimerExpired.Value ||
                    isTimerRunning.Value ||
                    timerRitualSequence.Value != ritualSequence.Value ||
                    timerTurnSequence.Value != turnSequence.Value ||
                    timerSequence.Value != originatingTimerSequence)
                {
                    return RejectTurnOutcome(
                        $"The originating timer expiration does not belong to the active turn. " +
                        $"RequestedTimer={originatingTimerSequence}, " +
                        $"CurrentTimer={timerSequence.Value}, " +
                        $"TimerRitual={timerRitualSequence.Value}, " +
                        $"ActiveRitual={ritualSequence.Value}, " +
                        $"TimerTurn={timerTurnSequence.Value}, " +
                        $"ActiveTurn={turnSequence.Value}, " +
                        $"Running={isTimerRunning.Value}, " +
                        $"Expired={isTimerExpired.Value}.");
                }
            }
            else
            {
                return RejectTurnOutcome(
                    $"Outcome type {requestedOutcome} is not enabled in this migration.");
            }

            if (turnOutcomeSequence.Value == uint.MaxValue)
            {
                return RejectTurnOutcome(
                    "The turn outcome sequence is exhausted.");
            }

            uint nextOutcomeSequence = turnOutcomeSequence.Value + 1;
            double serverTimestamp = GetCurrentNetworkTime();
            turnOutcomeSequence.Value = nextOutcomeSequence;
            turnOutcomeRitualSequence.Value = ritualSequence.Value;
            turnOutcomeTurnSequence.Value = turnSequence.Value;
            turnOutcomePlayerId.Value = participant.PlayerId;
            turnOutcomeType.Value = requestedOutcome;
            turnOutcomeValidationSequence.Value =
                originatingValidationSequence;
            turnOutcomeTimerSequence.Value = originatingTimerSequence;
            turnOutcomeServerTimestamp.Value = serverTimestamp;
            latestRitualOutcome.Value =
                requestedOutcome == TurnOutcomeType.Success
                    ? RitualOutcome.TurnSucceeded
                    : RitualOutcome.TurnFailed;
            failureReason.Value =
                requestedOutcome == TurnOutcomeType.TimerExpired
                    ? RitualFailureReason.TimedOut
                    : RitualFailureReason.None;
            turnOutcomeRevision.Value++;
            snapshotRevision.Value++;

            TurnOutcomeSnapshot outcome = CreateTurnOutcomeSnapshot();
            Debug.Log(
                "[RitualAuthority]\n" +
                "Turn Outcome Committed\n" +
                $"Outcome = {outcome.OutcomeType}\n" +
                $"Player = {outcome.PlayerId}\n" +
                $"TurnSequence = {outcome.TurnSequenceId.Value}\n" +
                $"OutcomeSequence = {outcome.OutcomeSequenceId.Value}\n" +
                $"ValidationSequence = {outcome.ValidationSequence}\n" +
                $"TimerSequence = {outcome.TimerSequence}\n" +
                $"ServerTimestamp = {outcome.ServerTimestamp}",
                this);

            TurnOutcomeCommitted?.Invoke(outcome);
            PublishTurnOutcomeObserversRpc(
                outcome.RitualSequenceId.Value,
                outcome.TurnSequenceId.Value,
                outcome.OutcomeSequenceId.Value,
                outcome.PlayerId,
                outcome.OutcomeType,
                outcome.ValidationSequence,
                outcome.TimerSequence,
                outcome.ServerTimestamp);
            return TryCommitConsequence(outcome);
        }

        private bool TryCommitConsequence(TurnOutcomeSnapshot outcome)
        {
            if (!IsServerInitialized)
                return RejectConsequence("Only the server may commit consequences.");

            TurnOutcomeSnapshot currentOutcome = CreateCurrentTurnOutcomeSnapshot();
            if (!currentOutcome.HasOutcome ||
                outcome.RitualSequenceId.Value != ritualSequence.Value ||
                outcome.TurnSequenceId.Value != turnSequence.Value ||
                outcome.OutcomeSequenceId.Value !=
                    currentOutcome.OutcomeSequenceId.Value ||
                !string.Equals(
                    outcome.PlayerId,
                    currentOutcome.PlayerId,
                    StringComparison.Ordinal))
            {
                return RejectConsequence(
                    "The originating outcome is missing, stale, or does not belong to the active turn.");
            }

            RitualConsequenceSnapshot existing = CreateConsequenceSnapshot();
            if (existing.HasConsequence &&
                existing.OutcomeSequenceId.Value ==
                    outcome.OutcomeSequenceId.Value)
            {
                return RejectConsequence(
                    $"Outcome {outcome.OutcomeSequenceId.Value} already has consequence {existing.ConsequenceType}.");
            }

            RitualConsequenceType selectedConsequence =
                outcome.OutcomeType switch
                {
                    TurnOutcomeType.Success =>
                        RitualConsequenceType.TurnSucceeded,
                    TurnOutcomeType.TimerExpired =>
                        RitualConsequenceType.TimerExpired,
                    _ => RitualConsequenceType.None
                };
            if (selectedConsequence == RitualConsequenceType.None)
            {
                return RejectConsequence(
                    $"Outcome type {outcome.OutcomeType} has no enabled consequence in this migration.");
            }

            if (consequenceSequence.Value == uint.MaxValue)
                return RejectConsequence("The consequence sequence is exhausted.");

            uint nextConsequenceSequence = consequenceSequence.Value + 1;
            double serverTimestamp = GetCurrentNetworkTime();
            consequenceSequence.Value = nextConsequenceSequence;
            consequenceRitualSequence.Value = outcome.RitualSequenceId.Value;
            consequenceTurnSequence.Value = outcome.TurnSequenceId.Value;
            consequenceOutcomeSequence.Value =
                outcome.OutcomeSequenceId.Value;
            consequencePlayerId.Value = outcome.PlayerId;
            consequenceType.Value = selectedConsequence;
            consequenceServerTimestamp.Value = serverTimestamp;
            consequenceRevision.Value++;
            snapshotRevision.Value++;

            RitualConsequenceSnapshot consequence =
                CreateConsequenceSnapshot();
            Debug.Log(
                "[RitualAuthority]\n" +
                "Consequence Committed\n" +
                $"Consequence = {consequence.ConsequenceType}\n" +
                $"Player = {consequence.PlayerId}\n" +
                $"TurnSequence = {consequence.TurnSequenceId.Value}\n" +
                $"OutcomeSequence = {consequence.OutcomeSequenceId.Value}\n" +
                $"ConsequenceSequence = {consequence.ConsequenceSequenceId.Value}\n" +
                $"ServerTimestamp = {consequence.ServerTimestamp}",
                this);

            ConsequenceCommitted?.Invoke(consequence);
            PublishConsequenceObserversRpc(
                consequence.RitualSequenceId.Value,
                consequence.TurnSequenceId.Value,
                consequence.OutcomeSequenceId.Value,
                consequence.ConsequenceSequenceId.Value,
                consequence.PlayerId,
                consequence.ConsequenceType,
                consequence.ServerTimestamp);
            return true;
        }

        [ObserversRpc(ExcludeServer = true)]
        private void PublishTurnOutcomeObserversRpc(
            uint outcomeRitualSequence,
            uint outcomeTurnSequence,
            uint outcomeSequence,
            string derivedPlayerId,
            TurnOutcomeType committedOutcome,
            uint originatingValidationSequence,
            uint originatingTimerSequence,
            double serverTimestamp)
        {
            TurnOutcomeCommitted?.Invoke(new TurnOutcomeSnapshot(
                new RitualSequenceId(outcomeRitualSequence),
                new RitualTurnSequenceId(outcomeTurnSequence),
                new RitualOutcomeSequenceId(outcomeSequence),
                derivedPlayerId,
                committedOutcome,
                originatingValidationSequence,
                originatingTimerSequence,
                serverTimestamp));
        }

        [ObserversRpc(ExcludeServer = true)]
        private void PublishConsequenceObserversRpc(
            uint committedRitualSequence,
            uint committedTurnSequence,
            uint originatingOutcomeSequence,
            uint committedConsequenceSequence,
            string derivedPlayerId,
            RitualConsequenceType committedConsequence,
            double serverTimestamp)
        {
            ConsequenceCommitted?.Invoke(new RitualConsequenceSnapshot(
                new RitualSequenceId(committedRitualSequence),
                new RitualTurnSequenceId(committedTurnSequence),
                new RitualOutcomeSequenceId(originatingOutcomeSequence),
                new RitualConsequenceSequenceId(
                    committedConsequenceSequence),
                derivedPlayerId,
                committedConsequence,
                serverTimestamp));
        }

        [ObserversRpc(ExcludeServer = true)]
        private void PublishAcceptedVoiceSubmissionObserversRpc(
            uint acceptedRitualSequence,
            uint acceptedTurnSequence,
            uint acceptedSubmissionSequence,
            string derivedPlayerId,
            string recognizedText,
            double serverTimestamp)
        {
            RitualVoiceSubmissionSnapshot acceptedSubmission = new(
                new RitualSequenceId(acceptedRitualSequence),
                new RitualTurnSequenceId(acceptedTurnSequence),
                acceptedSubmissionSequence,
                derivedPlayerId,
                recognizedText,
                serverTimestamp);
            VoiceSubmissionAccepted?.Invoke(acceptedSubmission);
        }

        /// <summary>
        /// Validates and locks one server-owned ritual roster from the approved NetworkPlayer
        /// assignments in SeatManager's configured physical traversal.
        /// </summary>
        public bool TryBuildRosterFromCurrentSeating()
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning(
                    $"{nameof(NetworkRitualAuthority)} rejected roster creation because only the server may own the ritual roster.",
                    this);
                return false;
            }

            if (ritualRoster.Count > 0)
            {
                Debug.LogWarning(
                    $"{nameof(NetworkRitualAuthority)} rejected roster creation because the authoritative roster is already locked.",
                    this);
                return false;
            }

            if (ritualPhase.Value != RitualPhase.Inactive &&
                ritualPhase.Value != RitualPhase.Preparing)
            {
                Debug.LogWarning(
                    $"{nameof(NetworkRitualAuthority)} rejected roster creation during phase {ritualPhase.Value}.",
                    this);
                return false;
            }

            if (seatManager == null)
            {
                Debug.LogError(
                    $"{nameof(NetworkRitualAuthority)} cannot create a roster without its assigned {nameof(SeatManager)}.",
                    this);
                return false;
            }

            if (!seatManager.HasConfiguredPhysicalSeatOrder())
            {
                Debug.LogError(
                    $"{nameof(NetworkRitualAuthority)} rejected the roster because {nameof(SeatManager)} does not contain one valid, unique physical seat order.",
                    this);
                return false;
            }

            if (!TryCollectApprovedPlayers(
                    out List<NetworkPlayer> approvedPlayers,
                    out HashSet<NetworkPlayer> approvedPlayerSet))
            {
                return false;
            }

            if (!TryValidateApprovedPlayers(approvedPlayers))
                return false;

            SeatTraversalDirection seatDirection =
                traversalDirection.Value == RitualTraversalDirection.Clockwise
                    ? SeatTraversalDirection.Clockwise
                    : SeatTraversalDirection.CounterClockwise;
            List<Seat> orderedSeats = seatManager.GetPhysicalSeats(seatDirection);
            List<NetworkRitualRosterEntry> candidateRoster =
                new(approvedPlayers.Count);

            foreach (Seat seat in orderedSeats)
            {
                int seatId = seatManager.GetSeatId(seat);
                NetworkPlayer player = NetworkPlayer.FindBySeatId(seatId);
                if (player == null || !approvedPlayerSet.Contains(player))
                    continue;

                candidateRoster.Add(
                    new NetworkRitualRosterEntry(
                        player.PlayerId,
                        seatId,
                        isActive: true,
                        isAlive: true));
            }

            if (candidateRoster.Count != approvedPlayers.Count)
            {
                Debug.LogError(
                    $"{nameof(NetworkRitualAuthority)} rejected an inconsistent roster: {approvedPlayers.Count} approved players produced {candidateRoster.Count} physical traversal entries.",
                    this);
                return false;
            }

            if (!TryBeginRitualLifecycleForRosterLock())
                return false;

            ritualRoster.Clear();
            foreach (NetworkRitualRosterEntry entry in candidateRoster)
            {
                ritualRoster.Add(entry);
            }

            rosterRevision.Value++;
            snapshotRevision.Value++;
            Debug.Log(
                "[RitualAuthority]\n" +
                "Ritual Roster Locked\n" +
                $"Players = {candidateRoster.Count}\n" +
                $"Traversal = {traversalDirection.Value}\n" +
                $"RitualSequence = {ritualSequence.Value}\n" +
                $"Phase = {ritualPhase.Value}",
                this);
            return true;
        }

        private bool TryBeginRitualLifecycleForRosterLock()
        {
            if (ritualPhase.Value == RitualPhase.Preparing &&
                ritualSequence.Value > 0)
            {
                return true;
            }

            if (ritualPhase.Value != RitualPhase.Inactive)
            {
                Debug.LogWarning(
                    "[RitualAuthority]\n" +
                    "Ritual Lifecycle Start Rejected\n" +
                    $"Reason = Phase {ritualPhase.Value} cannot begin a roster-backed ritual.\n" +
                    $"RitualSequence = {ritualSequence.Value}",
                    this);
                return false;
            }

            if (!RitualPhaseTransitions.IsLegal(
                    ritualPhase.Value,
                    RitualPhase.Preparing))
            {
                Debug.LogWarning(
                    "[RitualAuthority]\n" +
                    "Ritual Lifecycle Start Rejected\n" +
                    "Reason = Inactive to Preparing is not a legal ritual phase transition.",
                    this);
                return false;
            }

            if (ritualSequence.Value == uint.MaxValue)
            {
                Debug.LogWarning(
                    "[RitualAuthority]\n" +
                    "Ritual Lifecycle Start Rejected\n" +
                    "Reason = The ritual sequence is exhausted.",
                    this);
                return false;
            }

            ritualSequence.Value++;
            ritualPhase.Value = RitualPhase.Preparing;

            Debug.Log(
                "[RitualAuthority]\n" +
                "Ritual Lifecycle Started\n" +
                $"RitualSequence = {ritualSequence.Value}\n" +
                $"Phase = {ritualPhase.Value}",
                this);
            return true;
        }

        public bool TryGetPlayerBySeat(int seatId, out RitualRosterEntrySnapshot entry)
        {
            return Roster.TryGetPlayerBySeat(seatId, out entry);
        }

        public bool TryGetSeatByPlayer(string playerId, out int seatId)
        {
            return Roster.TryGetSeatByPlayer(playerId, out seatId);
        }

        [ContextMenu("Networking/Apply Deterministic Test Snapshot")]
        private void ApplyDeterministicTestSnapshotFromContextMenu()
        {
            TryApplyDevelopmentTestSnapshot(RitualPhase.Preparing);
        }

        [ContextMenu("Networking/Build Authoritative Ritual Roster")]
        private void BuildAuthoritativeRitualRosterFromContextMenu()
        {
            TryBuildRosterFromCurrentSeating();
        }

        [ContextMenu("Networking/Commit Next Active Participant")]
        private void CommitNextActiveParticipantFromContextMenu()
        {
            TryCommitNextActiveParticipant();
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
            RitualBookArrivalSnapshot bookArrival = CreateBookArrivalSnapshot();
            RitualTimerSnapshot timer = CreateTimerSnapshot();
            RitualVoiceSubmissionSnapshot voiceSubmission =
                CreateVoiceSubmissionSnapshot();
            RitualValidationSnapshot validation = CreateValidationSnapshot();
            TurnOutcomeSnapshot turnOutcome =
                CreateCurrentTurnOutcomeSnapshot();
            RitualConsequenceSnapshot consequence =
                CreateCurrentConsequenceSnapshot();
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
                CreateRosterSnapshot(),
                turn,
                bookArrival,
                timer,
                voiceSubmission,
                validation,
                turnOutcome,
                consequence,
                phrase,
                outcome,
                isGameOver.Value,
                winnerPlayerId.Value);
        }

        private RitualRosterSnapshot CreateRosterSnapshot()
        {
            RitualRosterEntrySnapshot[] entries =
                new RitualRosterEntrySnapshot[ritualRoster.Count];

            for (int index = 0; index < ritualRoster.Count; index++)
            {
                NetworkRitualRosterEntry entry = ritualRoster[index];
                entries[index] = new RitualRosterEntrySnapshot(
                    entry.PlayerId,
                    entry.SeatId,
                    entry.IsActive,
                    entry.IsAlive);
            }

            return new RitualRosterSnapshot(
                rosterRevision.Value,
                traversalDirection.Value,
                entries);
        }

        private RitualBookArrivalSnapshot CreateBookArrivalSnapshot()
        {
            return new RitualBookArrivalSnapshot(
                acceptedBookArrivalMovementSequence.Value,
                new RitualSequenceId(acceptedBookArrivalRitualSequence.Value),
                new RitualTurnSequenceId(acceptedBookArrivalTurnSequence.Value),
                acceptedBookArrivalTargetSeatId.Value,
                acceptedBookArrivalPlayerId.Value,
                acceptedBookArrivalNetworkTime.Value);
        }

        private RitualTimerSnapshot CreateTimerSnapshot()
        {
            return new RitualTimerSnapshot(
                timerSequence.Value,
                new RitualSequenceId(timerRitualSequence.Value),
                new RitualTurnSequenceId(timerTurnSequence.Value),
                isTimerRunning.Value,
                isTimerExpired.Value,
                activeTimerDuration.Value,
                turnStartNetworkTime.Value,
                timerDeadlineNetworkTime.Value,
                CalculateRemainingTime(GetCurrentNetworkTime()));
        }

        private RitualVoiceSubmissionSnapshot CreateVoiceSubmissionSnapshot()
        {
            return new RitualVoiceSubmissionSnapshot(
                new RitualSequenceId(acceptedVoiceRitualSequence.Value),
                new RitualTurnSequenceId(acceptedVoiceTurnSequence.Value),
                acceptedVoiceSubmissionSequence.Value,
                acceptedVoicePlayerId.Value,
                acceptedVoiceRecognizedText.Value,
                acceptedVoiceServerTimestamp.Value);
        }

        private RitualValidationSnapshot CreateValidationSnapshot()
        {
            string[] authoritativePhraseWords =
                new string[phraseWords.Count];
            for (int wordIndex = 0;
                wordIndex < phraseWords.Count;
                wordIndex++)
            {
                authoritativePhraseWords[wordIndex] =
                    phraseWords[wordIndex] ?? string.Empty;
            }

            return new RitualValidationSnapshot(
                new RitualSequenceId(validationRitualSequence.Value),
                new RitualTurnSequenceId(validationTurnSequence.Value),
                validationSubmissionSequence.Value,
                validationSequence.Value,
                validationPlayerId.Value,
                validatedMode.Value,
                validationResult.Value,
                validationAcceptedWordCount.Value,
                validationWordIndex.Value,
                validationRejectedWordIndex.Value,
                authoritativePhraseWords,
                expectedWordIndex.Value,
                validationExpectedWord.Value,
                validationRejectedWord.Value,
                validationReceivedText.Value,
                validationFailureReason.Value,
                validationServerTimestamp.Value);
        }

        private TurnOutcomeSnapshot CreateTurnOutcomeSnapshot()
        {
            return new TurnOutcomeSnapshot(
                new RitualSequenceId(turnOutcomeRitualSequence.Value),
                new RitualTurnSequenceId(turnOutcomeTurnSequence.Value),
                new RitualOutcomeSequenceId(turnOutcomeSequence.Value),
                turnOutcomePlayerId.Value,
                turnOutcomeType.Value,
                turnOutcomeValidationSequence.Value,
                turnOutcomeTimerSequence.Value,
                turnOutcomeServerTimestamp.Value);
        }

        private TurnOutcomeSnapshot CreateCurrentTurnOutcomeSnapshot()
        {
            TurnOutcomeSnapshot latest = CreateTurnOutcomeSnapshot();
            if (latest.RitualSequenceId.Value == ritualSequence.Value &&
                latest.TurnSequenceId.Value == turnSequence.Value)
            {
                return latest;
            }

            return new TurnOutcomeSnapshot(
                new RitualSequenceId(ritualSequence.Value),
                new RitualTurnSequenceId(turnSequence.Value),
                new RitualOutcomeSequenceId(0),
                activePlayerId.Value,
                TurnOutcomeType.None,
                0,
                0,
                0d);
        }

        private RitualConsequenceSnapshot CreateConsequenceSnapshot()
        {
            return new RitualConsequenceSnapshot(
                new RitualSequenceId(consequenceRitualSequence.Value),
                new RitualTurnSequenceId(consequenceTurnSequence.Value),
                new RitualOutcomeSequenceId(
                    consequenceOutcomeSequence.Value),
                new RitualConsequenceSequenceId(consequenceSequence.Value),
                consequencePlayerId.Value,
                consequenceType.Value,
                consequenceServerTimestamp.Value);
        }

        private RitualConsequenceSnapshot CreateCurrentConsequenceSnapshot()
        {
            RitualConsequenceSnapshot latest =
                CreateConsequenceSnapshot();
            if (latest.RitualSequenceId.Value == ritualSequence.Value &&
                latest.TurnSequenceId.Value == turnSequence.Value)
            {
                return latest;
            }

            return new RitualConsequenceSnapshot(
                new RitualSequenceId(ritualSequence.Value),
                new RitualTurnSequenceId(turnSequence.Value),
                new RitualOutcomeSequenceId(0),
                new RitualConsequenceSequenceId(0),
                activePlayerId.Value,
                RitualConsequenceType.None,
                0d);
        }

        private static bool IsPhraseCompletionValidation(
            RitualValidationSnapshot validation)
        {
            if (!validation.IsAccepted ||
                validation.PhraseWords.Length == 0)
            {
                return false;
            }

            if (validation.ValidationMode ==
                RitualValidationMode.FullPhrase)
            {
                return validation.AcceptedWordCount >=
                    validation.PhraseWords.Length;
            }

            return validation.ValidatedWordIndex >= 0 &&
                validation.ValidatedWordIndex + 1 >=
                    validation.PhraseWords.Length;
        }

        private RitualController ResolveRitualController()
        {
            if (ritualController == null)
            {
                ritualController = FindFirstObjectByType<RitualController>(
                    FindObjectsInactive.Include);
            }

            return ritualController;
        }

        private void CaptureAuthoritativePhrase(
            IncantationManager manager,
            RitualValidationMode mode)
        {
            IReadOnlyList<IncantationWord> currentWords =
                manager.CurrentIncantation;
            bool phraseChanged = phraseWords.Count != currentWords.Count;
            if (!phraseChanged)
            {
                for (int wordIndex = 0;
                    wordIndex < currentWords.Count;
                    wordIndex++)
                {
                    if (string.Equals(
                            phraseWords[wordIndex],
                            currentWords[wordIndex]?.Text,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    phraseChanged = true;
                    break;
                }
            }

            if (phraseChanged)
            {
                phraseWords.Clear();
                foreach (IncantationWord word in currentWords)
                {
                    phraseWords.Add(word?.Text ?? string.Empty);
                }

                phraseVersion.Value++;
            }

            validationMode.Value = mode;
            unlockedWordCount.Value = (uint)currentWords.Count;
            expectedWordIndex.Value = manager.CurrentWordIndex;
        }

        private static RitualValidationFailureReason MapValidationFailureReason(
            PhraseValidationFailureReason reason)
        {
            return reason switch
            {
                PhraseValidationFailureReason.Empty =>
                    RitualValidationFailureReason.Empty,
                PhraseValidationFailureReason.WrongWord =>
                    RitualValidationFailureReason.WrongWord,
                PhraseValidationFailureReason.TooFewWords =>
                    RitualValidationFailureReason.TooFewWords,
                _ => RitualValidationFailureReason.None
            };
        }

        private void PublishValidation(RitualValidationSnapshot validation)
        {
            if (validation.IsAccepted)
                ValidationAccepted?.Invoke(validation);
            else
                ValidationRejected?.Invoke(validation);
        }

        private void LogValidationCommitted(
            RitualValidationSnapshot validation)
        {
            string label = validation.IsAccepted
                ? "Validation Accepted"
                : "Validation Rejected";
            Debug.Log(
                "[RitualAuthority]\n" +
                $"{label}\n" +
                $"PlayerId = {validation.PlayerId}\n" +
                $"TurnSequence = {validation.TurnSequenceId.Value}\n" +
                $"SubmissionSequence = {validation.SubmissionSequence}\n" +
                $"ValidationSequence = {validation.ValidationSequence}\n" +
                $"Result = {validation.Result}\n" +
                $"AcceptedWords = {validation.AcceptedWordCount}\n" +
                $"FailureReason = {validation.FailureReason}\n" +
                $"Expected = {validation.ExpectedWord}\n" +
                $"Received = {validation.ReceivedText}",
                this);
        }

        private bool RejectValidation(
            string reason,
            RitualVoiceSubmissionSnapshot submission)
        {
            Debug.LogWarning(
                "[RitualAuthority]\n" +
                "Validation Rejected\n" +
                $"Reason = {reason}\n" +
                $"PlayerId = {submission.PlayerId}\n" +
                $"TurnSequence = {submission.TurnSequenceId.Value}\n" +
                $"SubmissionSequence = {submission.SubmissionSequence}",
                this);
            return false;
        }

        private bool TryResolvePlayerForConnection(
            NetworkConnection sender,
            out NetworkPlayer resolvedPlayer,
            out string rejectionReason)
        {
            resolvedPlayer = null;
            int matchCount = 0;

            foreach (NetworkPlayer player in NetworkPlayer.ActivePlayers)
            {
                if (player == null ||
                    player.Connection == null ||
                    !player.Connection.Equals(sender))
                {
                    continue;
                }

                matchCount++;
                resolvedPlayer = player;
            }

            if (matchCount == 1)
            {
                rejectionReason = string.Empty;
                return true;
            }

            rejectionReason = matchCount == 0
                ? "The sending connection has no server-owned NetworkPlayer."
                : $"The sending connection resolves to {matchCount} NetworkPlayer instances.";
            resolvedPlayer = null;
            return false;
        }

        private static bool TryNormalizeRecognizedText(
            string recognizedText,
            out string normalizedText,
            out string rejectionReason)
        {
            normalizedText = string.Empty;
            if (string.IsNullOrWhiteSpace(recognizedText))
            {
                rejectionReason = "Recognized text is empty.";
                return false;
            }

            if (recognizedText.Length >
                RitualVoiceSubmission.MaximumRecognizedTextLength)
            {
                rejectionReason =
                    $"Recognized text exceeds {RitualVoiceSubmission.MaximumRecognizedTextLength} characters.";
                return false;
            }

            StringBuilder builder = new(recognizedText.Length);
            bool previousWasWhitespace = true;
            foreach (char character in recognizedText)
            {
                if (char.IsControl(character) && !char.IsWhiteSpace(character))
                {
                    rejectionReason = "Recognized text contains control characters.";
                    return false;
                }

                if (char.IsWhiteSpace(character))
                {
                    if (!previousWasWhitespace)
                    {
                        builder.Append(' ');
                        previousWasWhitespace = true;
                    }

                    continue;
                }

                builder.Append(character);
                previousWasWhitespace = false;
            }

            normalizedText = builder.ToString().Trim();
            if (normalizedText.Length == 0)
            {
                rejectionReason = "Recognized text is empty after normalization.";
                return false;
            }

            rejectionReason = string.Empty;
            return true;
        }

        private bool TryStartTimerForArrival(
            RitualBookArrivalReport report,
            RitualRosterEntrySnapshot participant,
            double duration)
        {
            double startTime = GetCurrentNetworkTime();
            double deadline = startTime + duration;
            if (double.IsNaN(deadline) || double.IsInfinity(deadline))
                return RejectTimer($"Timer deadline {deadline} is invalid.");

            uint nextTimerSequence = timerSequence.Value + 1;
            timerSequence.Value = nextTimerSequence;
            timerRitualSequence.Value = report.RitualSequence;
            timerTurnSequence.Value = report.TurnSequence;
            activeTimerDuration.Value = duration;
            timerRemainingTime.Value = duration;
            turnStartNetworkTime.Value = startTime;
            timerDeadlineNetworkTime.Value = deadline;
            isTimerExpired.Value = false;
            isTimerRunning.Value = true;
            timerRevision.Value++;
            snapshotRevision.Value++;

            Debug.Log(
                "[RitualAuthority]\n" +
                "Timer Started\n" +
                $"TimerSequence = {nextTimerSequence}\n" +
                $"Duration = {duration}\n" +
                $"StartTime = {startTime}\n" +
                $"Deadline = {deadline}\n" +
                $"TurnSequence = {report.TurnSequence}\n" +
                $"Player = {participant.PlayerId}",
                this);
            return true;
        }

        private bool TryCommitTimerExpired()
        {
            if (!IsServerInitialized)
                return RejectTimer("Only the server may expire ritual time.");

            if (!isTimerRunning.Value)
            {
                string reason = isTimerExpired.Value
                    ? $"Timer {timerSequence.Value} already expired."
                    : "The ritual timer is not running.";
                return RejectTimer(reason);
            }

            if (timerTurnSequence.Value != turnSequence.Value)
            {
                return RejectTimer(
                    $"Timer turn {timerTurnSequence.Value} does not match active turn {turnSequence.Value}.");
            }

            if (timerRitualSequence.Value != ritualSequence.Value)
            {
                return RejectTimer(
                    $"Timer ritual {timerRitualSequence.Value} does not match active ritual {ritualSequence.Value}.");
            }

            double currentTime = GetCurrentNetworkTime();
            if (currentTime < timerDeadlineNetworkTime.Value)
            {
                return RejectTimer(
                    $"Timer {timerSequence.Value} cannot expire before deadline {timerDeadlineNetworkTime.Value}; current time is {currentTime}.");
            }

            uint expiredTimerSequence = timerSequence.Value;
            timerRemainingTime.Value = 0d;
            isTimerRunning.Value = false;
            isTimerExpired.Value = true;
            timerRevision.Value++;
            snapshotRevision.Value++;

            Debug.Log(
                "[RitualAuthority]\n" +
                "Timer Expired\n" +
                $"TimerSequence = {expiredTimerSequence}\n" +
                $"TurnSequence = {timerTurnSequence.Value}\n" +
                $"Deadline = {timerDeadlineNetworkTime.Value}",
                this);
            return TryCommitTurnOutcome(
                TurnOutcomeType.TimerExpired,
                0,
                expiredTimerSequence);
        }

        private double CalculateRemainingTime(double currentNetworkTime)
        {
            if (!isTimerRunning.Value)
                return Math.Max(0d, timerRemainingTime.Value);

            return Math.Max(0d, timerDeadlineNetworkTime.Value - currentNetworkTime);
        }

        private double GetCurrentNetworkTime()
        {
            if (!IsNetworkSessionActive || TimeManager == null)
                return 0d;

            return TimeManager.TicksToTime();
        }

        private NetworkBookAuthority ResolveBookAuthority()
        {
            if (networkBookAuthority == null)
                networkBookAuthority = GetComponent<NetworkBookAuthority>();

            return networkBookAuthority;
        }

        private bool RejectBookMovement(string reason)
        {
            Debug.LogWarning(
                "[RitualAuthority]\n" +
                "Book Movement Rejected\n" +
                $"Reason = {reason}",
                this);
            return false;
        }

        private bool RejectBookArrival(string reason)
        {
            Debug.LogWarning(
                "[RitualAuthority]\n" +
                "Book Arrival Rejected\n" +
                $"Reason = {reason}",
                this);
            return false;
        }

        private bool RejectTimer(string reason)
        {
            Debug.LogWarning(
                "[RitualAuthority]\n" +
                "Timer Rejected\n" +
                $"Reason = {reason}",
                this);
            return false;
        }

        private bool RejectVoiceSubmission(
            string reason,
            int connectionId,
            RitualVoiceSubmission submission)
        {
            Debug.LogWarning(
                "[RitualAuthority]\n" +
                "Voice Submission Rejected\n" +
                $"Reason = {reason}\n" +
                $"ConnectionId = {connectionId}\n" +
                $"TurnSequence = {submission.TurnSequence}\n" +
                $"SubmissionSequence = {submission.SubmissionSequence}",
                this);
            return false;
        }

        private bool RejectTurnOutcome(string reason)
        {
            Debug.LogWarning(
                "[RitualAuthority]\n" +
                "Turn Outcome Rejected\n" +
                $"Reason = {reason}\n" +
                $"RitualSequence = {ritualSequence.Value}\n" +
                $"TurnSequence = {turnSequence.Value}",
                this);
            return false;
        }

        private bool RejectConsequence(string reason)
        {
            Debug.LogWarning(
                "[RitualAuthority]\n" +
                "Consequence Rejected\n" +
                $"Reason = {reason}\n" +
                $"RitualSequence = {ritualSequence.Value}\n" +
                $"TurnSequence = {turnSequence.Value}\n" +
                $"OutcomeSequence = {turnOutcomeSequence.Value}",
                this);
            return false;
        }

        private bool TryCollectApprovedPlayers(
            out List<NetworkPlayer> approvedPlayers,
            out HashSet<NetworkPlayer> approvedPlayerSet)
        {
            approvedPlayers = new List<NetworkPlayer>();
            approvedPlayerSet = new HashSet<NetworkPlayer>();

            foreach (NetworkPlayer player in NetworkPlayer.ActivePlayers)
            {
                if (player == null)
                {
                    Debug.LogError(
                        $"{nameof(NetworkRitualAuthority)} rejected an inconsistent roster containing a missing network player.",
                        this);
                    return false;
                }

                if (!player.IsCircleMember)
                    continue;

                if (!approvedPlayerSet.Add(player))
                {
                    Debug.LogError(
                        $"{nameof(NetworkRitualAuthority)} rejected a duplicate network player in the approved ritual roster.",
                        this);
                    return false;
                }

                approvedPlayers.Add(player);
            }

            if (approvedPlayers.Count == 0)
            {
                Debug.LogWarning(
                    $"{nameof(NetworkRitualAuthority)} rejected an empty ritual roster.",
                    this);
                return false;
            }

            if (approvedPlayers.Count > NetworkPlayer.MaximumCircleMembers)
            {
                Debug.LogError(
                    $"{nameof(NetworkRitualAuthority)} rejected {approvedPlayers.Count} players because the ritual capacity is {NetworkPlayer.MaximumCircleMembers}.",
                    this);
                return false;
            }

            return true;
        }

        private bool TryValidateApprovedPlayers(IReadOnlyList<NetworkPlayer> approvedPlayers)
        {
            HashSet<string> playerIds = new(StringComparer.Ordinal);
            HashSet<int> seatIds = new();

            foreach (NetworkPlayer player in approvedPlayers)
            {
                if (string.IsNullOrEmpty(player.PlayerId))
                {
                    Debug.LogError(
                        $"{nameof(NetworkRitualAuthority)} rejected a ritual player with a missing stable player ID.",
                        this);
                    return false;
                }

                if (!playerIds.Add(player.PlayerId))
                {
                    Debug.LogError(
                        $"{nameof(NetworkRitualAuthority)} rejected duplicate player ID {player.PlayerId}.",
                        this);
                    return false;
                }

                if (!player.HasAssignedSeat || seatManager.GetSeatById(player.SeatId) == null)
                {
                    Debug.LogError(
                        $"{nameof(NetworkRitualAuthority)} rejected player {player.PlayerId} with missing or invalid Seat ID {player.SeatId}.",
                        this);
                    return false;
                }

                if (!seatIds.Add(player.SeatId))
                {
                    Debug.LogError(
                        $"{nameof(NetworkRitualAuthority)} rejected duplicate Seat ID {player.SeatId}.",
                        this);
                    return false;
                }
            }

            return true;
        }

        private bool TryGetCurrentActiveParticipant(
            out RitualRosterEntrySnapshot participant)
        {
            if (string.IsNullOrEmpty(activePlayerId.Value) ||
                activeSeatId.Value == NoSeatId)
            {
                participant = default;
                return false;
            }

            RitualRosterSnapshot roster = CreateRosterSnapshot();
            if (!roster.TryGetPlayerBySeat(activeSeatId.Value, out participant))
                return false;

            return string.Equals(
                participant.PlayerId,
                activePlayerId.Value,
                StringComparison.Ordinal);
        }

        private bool TryValidateRosterForTraversal(
            IReadOnlyList<RitualRosterEntrySnapshot> entries)
        {
            HashSet<string> playerIds = new(StringComparer.Ordinal);
            HashSet<int> seatIds = new();

            foreach (RitualRosterEntrySnapshot entry in entries)
            {
                if (string.IsNullOrEmpty(entry.PlayerId))
                {
                    Debug.LogError(
                        "[RitualAuthority] Rejected active participant commit: roster contains a missing player ID.",
                        this);
                    return false;
                }

                if (!playerIds.Add(entry.PlayerId))
                {
                    Debug.LogError(
                        $"[RitualAuthority] Rejected active participant commit: duplicate player ID {entry.PlayerId}.",
                        this);
                    return false;
                }

                if (entry.SeatId < 0 || !seatIds.Add(entry.SeatId))
                {
                    Debug.LogError(
                        $"[RitualAuthority] Rejected active participant commit: invalid or duplicate Seat ID {entry.SeatId}.",
                        this);
                    return false;
                }
            }

            return true;
        }

        private bool TryFindCurrentParticipantIndex(
            IReadOnlyList<RitualRosterEntrySnapshot> entries,
            out int currentIndex)
        {
            bool hasPlayer = !string.IsNullOrEmpty(activePlayerId.Value);
            bool hasSeat = activeSeatId.Value != NoSeatId;
            if (!hasPlayer && !hasSeat)
            {
                currentIndex = -1;
                return true;
            }

            if (hasPlayer != hasSeat)
            {
                Debug.LogError(
                    "[RitualAuthority] Rejected active participant commit: active player and Seat state are incomplete.",
                    this);
                currentIndex = -1;
                return false;
            }

            for (int index = 0; index < entries.Count; index++)
            {
                RitualRosterEntrySnapshot entry = entries[index];
                if (entry.SeatId == activeSeatId.Value &&
                    string.Equals(
                        entry.PlayerId,
                        activePlayerId.Value,
                        StringComparison.Ordinal))
                {
                    currentIndex = index;
                    return true;
                }
            }

            Debug.LogError(
                $"[RitualAuthority] Rejected active participant commit: current participant {activePlayerId.Value}@Seat{activeSeatId.Value} is absent from the authoritative roster.",
                this);
            currentIndex = -1;
            return false;
        }

        private static int FindNextEligibleParticipantIndex(
            IReadOnlyList<RitualRosterEntrySnapshot> entries,
            int currentIndex)
        {
            for (int offset = 1; offset <= entries.Count; offset++)
            {
                int candidateIndex = (currentIndex + offset) % entries.Count;
                RitualRosterEntrySnapshot candidate = entries[candidateIndex];
                if (candidate.IsActive && candidate.IsAlive)
                    return candidateIndex;
            }

            return -1;
        }

        private static string FormatRoster(RitualRosterSnapshot roster)
        {
            RitualRosterEntrySnapshot[] entries = roster.Entries;
            if (entries.Length == 0)
                return "none";

            string[] labels = new string[entries.Length];
            for (int index = 0; index < entries.Length; index++)
            {
                RitualRosterEntrySnapshot entry = entries[index];
                labels[index] =
                    $"{entry.PlayerId}@Seat{entry.SeatId}[Active={entry.IsActive},Alive={entry.IsAlive}]";
            }

            return string.Join(" -> ", labels);
        }

        private void HandleRosterRevisionChanged(
            uint previousRevision,
            uint currentRevision,
            bool asServer)
        {
            rosterNotificationPending = true;
        }

        private void HandleBookArrivalRevisionChanged(
            uint previousRevision,
            uint currentRevision,
            bool asServer)
        {
            bookArrivalNotificationPending = true;
        }

        private void HandleTimerRevisionChanged(
            uint previousRevision,
            uint currentRevision,
            bool asServer)
        {
            timerNotificationPending = true;
            if (isTimerExpired.Value)
                timerExpiredNotificationPending = true;
        }

        private void HandleValidationRevisionChanged(
            uint previousRevision,
            uint currentRevision,
            bool asServer)
        {
            validationNotificationPending = true;
        }

        private void HandleTurnOutcomeRevisionChanged(
            uint previousRevision,
            uint currentRevision,
            bool asServer)
        {
            turnOutcomeNotificationPending = true;
        }

        private void HandleConsequenceRevisionChanged(
            uint previousRevision,
            uint currentRevision,
            bool asServer)
        {
            consequenceNotificationPending = true;
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
