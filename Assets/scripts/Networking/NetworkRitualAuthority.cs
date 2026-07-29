using System;
using System.Collections.Generic;
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
        private readonly SyncVar<uint> snapshotRevision = new(0);

        [Header("Ritual Roster Source")]
        [SerializeField] private SeatManager seatManager;

        private bool snapshotNotificationPending;
        private bool rosterNotificationPending;
        private bool ownsDiscoveryReference;
        private NetworkBookAuthority networkBookAuthority;

        public static NetworkRitualAuthority Instance { get; private set; }
        public RitualSnapshot Snapshot => CreateSnapshot();
        public RitualRosterSnapshot Roster => CreateRosterSnapshot();
        public RitualRosterEntrySnapshot[] CurrentRoster => Roster.Entries;
        public RitualRosterEntrySnapshot[] ActivePlayers => Roster.ActivePlayers;
        public RitualRosterEntrySnapshot[] AlivePlayers => Roster.AlivePlayers;
        public RitualRosterEntrySnapshot[] EliminatedPlayers => Roster.EliminatedPlayers;
        public string CurrentActivePlayerId => activePlayerId.Value;
        public int CurrentActiveSeatId => activeSeatId.Value;
        public uint RequestedBookMovementSequence => requestedBookMovementSequence.Value;
        public RitualRosterEntrySnapshot? CurrentActiveParticipant =>
            TryGetCurrentActiveParticipant(out RitualRosterEntrySnapshot participant)
                ? participant
                : null;

        public event Action<RitualSnapshot> SnapshotChanged;
        public event Action<RitualRosterSnapshot> RosterChanged;

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
            if ((!snapshotNotificationPending && !rosterNotificationPending) ||
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
                    $"[Ritual Snapshot] Session={snapshot.RitualSessionId}, Ritual={snapshot.SequenceId.Value}, Phase={snapshot.Phase}, Roster={snapshot.Roster.Count}, Turn={snapshot.Turn.SequenceId.Value}, ActivePlayer={snapshot.ActivePlayerId}, ActiveSeat={snapshot.Turn.ActiveSeatId}, Direction={snapshot.TraversalDirection}, Validation={snapshot.ValidationMode}, Phrase={snapshot.Phrase.SequenceId.Value}, Unlocked={snapshot.Phrase.UnlockedWordCount}, Expected={snapshot.Phrase.ExpectedWordIndex}, Start={snapshot.Turn.StartedAtNetworkTime}, Deadline={snapshot.Turn.EndsAtNetworkTime}, Outcome={snapshot.Outcome.Outcome}, Failure={snapshot.Outcome.FailureReason}, GameOver={snapshot.IsGameOver}, Winner={snapshot.WinnerPlayerId}.",
                    this);
#endif
            }
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            if (!TryClaimDiscoveryReference())
                return;

            rosterRevision.OnChange += HandleRosterRevisionChanged;
            snapshotRevision.OnChange += HandleSnapshotRevisionChanged;
            rosterNotificationPending = true;
            snapshotNotificationPending = true;
        }

        public override void OnStopNetwork()
        {
            rosterRevision.OnChange -= HandleRosterRevisionChanged;
            snapshotRevision.OnChange -= HandleSnapshotRevisionChanged;
            rosterNotificationPending = false;
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

            ritualRoster.Clear();
            foreach (NetworkRitualRosterEntry entry in candidateRoster)
            {
                ritualRoster.Add(entry);
            }

            rosterRevision.Value++;
            snapshotRevision.Value++;
            Debug.Log(
                $"{nameof(NetworkRitualAuthority)} locked {candidateRoster.Count} players in {traversalDirection.Value} physical SeatManager order.",
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
