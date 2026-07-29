namespace Incantation.Networking.Ritual
{
    /// <summary>
    /// Immutable semantic request for the physical Book execution boundary.
    /// Contains stable, serialization-safe identifiers only.
    /// </summary>
    public readonly struct RitualBookMovementCommand
    {
        public RitualBookMovementCommand(
            uint movementSequence,
            uint ritualSequence,
            uint turnSequence,
            int targetSeatId,
            string playerId)
        {
            MovementSequence = movementSequence;
            RitualSequence = ritualSequence;
            TurnSequence = turnSequence;
            TargetSeatId = targetSeatId;
            PlayerId = playerId ?? string.Empty;
        }

        public uint MovementSequence { get; }
        public uint RitualSequence { get; }
        public uint TurnSequence { get; }
        public int TargetSeatId { get; }
        public string PlayerId { get; }
    }
}
