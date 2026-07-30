using System;

namespace Incantation.Networking.Ritual
{
    public enum TurnOutcomeType : byte
    {
        None = 0,
        Success = 1,
        ValidationFailed = 2,
        TimerExpired = 3,
        FutureDisconnect = 4,
        FutureAborted = 5,
        FutureAdminAbort = 6
    }

    /// <summary>
    /// Immutable serialization-safe record of how one authoritative ritual turn ended.
    /// </summary>
    [Serializable]
    public readonly struct TurnOutcomeSnapshot
    {
        public TurnOutcomeSnapshot(
            RitualSequenceId ritualSequenceId,
            RitualTurnSequenceId turnSequenceId,
            RitualOutcomeSequenceId outcomeSequenceId,
            string playerId,
            TurnOutcomeType outcomeType,
            uint validationSequence,
            uint timerSequence,
            double serverTimestamp)
        {
            RitualSequenceId = ritualSequenceId;
            TurnSequenceId = turnSequenceId;
            OutcomeSequenceId = outcomeSequenceId;
            PlayerId = playerId ?? string.Empty;
            OutcomeType = outcomeType;
            ValidationSequence = validationSequence;
            TimerSequence = timerSequence;
            ServerTimestamp = serverTimestamp;
        }

        public RitualSequenceId RitualSequenceId { get; }
        public RitualTurnSequenceId TurnSequenceId { get; }
        public RitualOutcomeSequenceId OutcomeSequenceId { get; }
        public string PlayerId { get; }
        public TurnOutcomeType OutcomeType { get; }
        public uint ValidationSequence { get; }
        public uint TimerSequence { get; }
        public double ServerTimestamp { get; }
        public bool HasOutcome =>
            OutcomeSequenceId.Value > 0 &&
            OutcomeType != TurnOutcomeType.None;
    }
}
