using System;

namespace Incantation.Networking.Ritual
{
    /// <summary>
    /// Immutable value-only state for one authoritative ritual turn.
    /// Network timestamps use the networking layer's shared time domain.
    /// </summary>
    public readonly struct RitualTurnSnapshot
    {
        public RitualTurnSnapshot(
            RitualTurnSequenceId sequenceId,
            int activeSeatId,
            double startedAtNetworkTime,
            double endsAtNetworkTime)
        {
            SequenceId = sequenceId;
            ActiveSeatId = activeSeatId;
            StartedAtNetworkTime = startedAtNetworkTime;
            EndsAtNetworkTime = endsAtNetworkTime;
        }

        public RitualTurnSequenceId SequenceId { get; }
        public int ActiveSeatId { get; }
        public double StartedAtNetworkTime { get; }
        public double EndsAtNetworkTime { get; }
    }

    /// <summary>
    /// Immutable value-only state for the shared visible ritual phrase.
    /// </summary>
    public readonly struct RitualPhraseSnapshot
    {
        private readonly string[] words;

        public RitualPhraseSnapshot(
            RitualPhraseSequenceId sequenceId,
            RitualWordSequenceId expectedWordSequenceId,
            string[] words,
            uint unlockedWordCount,
            int expectedWordIndex)
        {
            SequenceId = sequenceId;
            ExpectedWordSequenceId = expectedWordSequenceId;
            this.words = words == null ? Array.Empty<string>() : (string[])words.Clone();
            UnlockedWordCount = unlockedWordCount;
            ExpectedWordIndex = expectedWordIndex;
        }

        public RitualPhraseSequenceId SequenceId { get; }
        public RitualWordSequenceId ExpectedWordSequenceId { get; }
        public uint UnlockedWordCount { get; }
        public int ExpectedWordIndex { get; }
        public int WordCount => words?.Length ?? 0;
        public string[] Words => words == null ? Array.Empty<string>() : (string[])words.Clone();
    }

    /// <summary>
    /// Immutable value-only state for the latest authoritative result.
    /// </summary>
    public readonly struct RitualOutcomeSnapshot
    {
        public RitualOutcomeSnapshot(
            RitualOutcomeSequenceId sequenceId,
            RitualTurnSequenceId turnSequenceId,
            RitualOutcome outcome,
            RitualFailureReason failureReason,
            int subjectSeatId)
        {
            SequenceId = sequenceId;
            TurnSequenceId = turnSequenceId;
            Outcome = outcome;
            FailureReason = failureReason;
            SubjectSeatId = subjectSeatId;
        }

        public RitualOutcomeSequenceId SequenceId { get; }
        public RitualTurnSequenceId TurnSequenceId { get; }
        public RitualOutcome Outcome { get; }
        public RitualFailureReason FailureReason { get; }
        public int SubjectSeatId { get; }
    }

    /// <summary>
    /// Immutable top-level contract for future authoritative synchronization.
    /// </summary>
    public readonly struct RitualSnapshot
    {
        public RitualSnapshot(
            string ritualSessionId,
            RitualSequenceId sequenceId,
            RitualPhase phase,
            RitualValidationMode validationMode,
            RitualTraversalDirection traversalDirection,
            uint completedRotationCount,
            string activePlayerId,
            RitualRosterSnapshot roster,
            RitualTurnSnapshot turn,
            RitualBookArrivalSnapshot bookArrival,
            RitualTimerSnapshot timer,
            RitualPhraseSnapshot phrase,
            RitualOutcomeSnapshot outcome,
            bool isGameOver,
            int winnerPlayerId)
        {
            RitualSessionId = ritualSessionId ?? string.Empty;
            SequenceId = sequenceId;
            Phase = phase;
            ValidationMode = validationMode;
            TraversalDirection = traversalDirection;
            CompletedRotationCount = completedRotationCount;
            ActivePlayerId = activePlayerId ?? string.Empty;
            Roster = roster;
            Turn = turn;
            BookArrival = bookArrival;
            Timer = timer;
            Phrase = phrase;
            Outcome = outcome;
            IsGameOver = isGameOver;
            WinnerPlayerId = winnerPlayerId;
        }

        public string RitualSessionId { get; }
        public RitualSequenceId SequenceId { get; }
        public RitualPhase Phase { get; }
        public RitualValidationMode ValidationMode { get; }
        public RitualTraversalDirection TraversalDirection { get; }
        public uint CompletedRotationCount { get; }
        public string ActivePlayerId { get; }
        public RitualRosterSnapshot Roster { get; }
        public RitualTurnSnapshot Turn { get; }
        public RitualBookArrivalSnapshot BookArrival { get; }
        public RitualTimerSnapshot Timer { get; }
        public RitualPhraseSnapshot Phrase { get; }
        public RitualOutcomeSnapshot Outcome { get; }
        public bool IsGameOver { get; }
        public int WinnerPlayerId { get; }
    }
}
