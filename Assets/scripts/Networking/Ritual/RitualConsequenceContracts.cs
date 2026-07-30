using System;

namespace Incantation.Networking.Ritual
{
    public enum RitualConsequenceType : byte
    {
        None = 0,
        ContinueTurn = 1,
        TurnSucceeded = 2,
        TurnFailed = 3,
        TimerExpired = 4,
        PlayerEliminated = 5,
        BookPrison = 6,
        Victory = 7,
        FutureCustom = 255
    }

    /// <summary>
    /// Immutable serialization-safe record of the gameplay consequence selected for one
    /// authoritative turn outcome.
    /// </summary>
    [Serializable]
    public readonly struct RitualConsequenceSnapshot
    {
        public RitualConsequenceSnapshot(
            RitualSequenceId ritualSequenceId,
            RitualTurnSequenceId turnSequenceId,
            RitualOutcomeSequenceId outcomeSequenceId,
            RitualConsequenceSequenceId consequenceSequenceId,
            string playerId,
            RitualConsequenceType consequenceType,
            double serverTimestamp)
        {
            RitualSequenceId = ritualSequenceId;
            TurnSequenceId = turnSequenceId;
            OutcomeSequenceId = outcomeSequenceId;
            ConsequenceSequenceId = consequenceSequenceId;
            PlayerId = playerId ?? string.Empty;
            ConsequenceType = consequenceType;
            ServerTimestamp = serverTimestamp;
        }

        public RitualSequenceId RitualSequenceId { get; }
        public RitualTurnSequenceId TurnSequenceId { get; }
        public RitualOutcomeSequenceId OutcomeSequenceId { get; }
        public RitualConsequenceSequenceId ConsequenceSequenceId { get; }
        public string PlayerId { get; }
        public RitualConsequenceType ConsequenceType { get; }
        public double ServerTimestamp { get; }
        public bool HasConsequence =>
            ConsequenceSequenceId.Value > 0 &&
            ConsequenceType != RitualConsequenceType.None;
    }
}
