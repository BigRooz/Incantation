using System;

namespace Incantation.Networking.Ritual
{
    /// <summary>
    /// Immutable movement-completion report sent from the physical Book execution boundary to
    /// the ritual authority. Contains stable, serialization-safe values only.
    /// </summary>
    public readonly struct RitualBookArrivalReport
    {
        public RitualBookArrivalReport(
            uint movementSequence,
            uint ritualSequence,
            uint turnSequence,
            int targetSeatId,
            double completionNetworkTime)
        {
            MovementSequence = movementSequence;
            RitualSequence = ritualSequence;
            TurnSequence = turnSequence;
            TargetSeatId = targetSeatId;
            CompletionNetworkTime = completionNetworkTime;
        }

        public uint MovementSequence { get; }
        public uint RitualSequence { get; }
        public uint TurnSequence { get; }
        public int TargetSeatId { get; }
        public double CompletionNetworkTime { get; }
    }

    /// <summary>
    /// Immutable read-only state for the latest authoritative Book arrival.
    /// </summary>
    public readonly struct RitualBookArrivalSnapshot
    {
        public RitualBookArrivalSnapshot(
            uint movementSequence,
            RitualSequenceId ritualSequenceId,
            RitualTurnSequenceId turnSequenceId,
            int targetSeatId,
            string playerId,
            double completionNetworkTime)
        {
            MovementSequence = movementSequence;
            RitualSequenceId = ritualSequenceId;
            TurnSequenceId = turnSequenceId;
            TargetSeatId = targetSeatId;
            PlayerId = playerId ?? string.Empty;
            CompletionNetworkTime = completionNetworkTime;
        }

        public uint MovementSequence { get; }
        public RitualSequenceId RitualSequenceId { get; }
        public RitualTurnSequenceId TurnSequenceId { get; }
        public int TargetSeatId { get; }
        public string PlayerId { get; }
        public double CompletionNetworkTime { get; }
        public bool HasArrived => MovementSequence > 0;
    }
}
