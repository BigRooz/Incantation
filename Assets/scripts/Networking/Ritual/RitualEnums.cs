namespace Incantation.Networking.Ritual
{
    /// <summary>
    /// Describes the authoritative lifecycle of a ritual without owning its behavior.
    /// </summary>
    public enum RitualPhase : byte
    {
        Inactive,
        Preparing,
        BookMoving,
        AwaitingRecitation,
        ResolvingTurn,
        CompletingRotation,
        Completed
    }

    /// <summary>
    /// Describes an authoritative ritual or turn result.
    /// </summary>
    public enum RitualOutcome : byte
    {
        None,
        TurnSucceeded,
        TurnFailed,
        RitualSucceeded,
        RitualFailed,
        Cancelled
    }

    /// <summary>
    /// Provides a stable reason for an unsuccessful authoritative result.
    /// </summary>
    public enum RitualFailureReason : byte
    {
        None,
        IncorrectWord,
        PhraseMismatch,
        TimedOut,
        ActivePlayerUnavailable,
        NoActivePlayers,
        Cancelled,
        AuthorityUnavailable
    }

    /// <summary>
    /// Selects how recognized speech candidates will be validated.
    /// </summary>
    public enum RitualValidationMode : byte
    {
        WordByWordRealtime,
        FullPhrase
    }

    /// <summary>
    /// Describes traversal through SeatManager's configured physical seat order.
    /// </summary>
    public enum RitualTraversalDirection : byte
    {
        Clockwise,
        CounterClockwise
    }
}
