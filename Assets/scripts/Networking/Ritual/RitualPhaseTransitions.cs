namespace Incantation.Networking.Ritual
{
    /// <summary>
    /// Defines legal authoritative phase changes without performing any transition.
    /// </summary>
    public static class RitualPhaseTransitions
    {
        public static bool IsLegal(RitualPhase currentPhase, RitualPhase requestedPhase)
        {
            switch (currentPhase)
            {
                case RitualPhase.Inactive:
                    return requestedPhase == RitualPhase.Preparing;

                case RitualPhase.Preparing:
                    return requestedPhase == RitualPhase.BookMoving
                        || requestedPhase == RitualPhase.Completed;

                case RitualPhase.BookMoving:
                    return requestedPhase == RitualPhase.AwaitingRecitation
                        || requestedPhase == RitualPhase.Completed;

                case RitualPhase.AwaitingRecitation:
                    return requestedPhase == RitualPhase.ResolvingTurn
                        || requestedPhase == RitualPhase.Completed;

                case RitualPhase.ResolvingTurn:
                    return requestedPhase == RitualPhase.BookMoving
                        || requestedPhase == RitualPhase.CompletingRotation
                        || requestedPhase == RitualPhase.Completed;

                case RitualPhase.CompletingRotation:
                    return requestedPhase == RitualPhase.BookMoving
                        || requestedPhase == RitualPhase.Completed;

                case RitualPhase.Completed:
                    return requestedPhase == RitualPhase.Inactive;

                default:
                    return false;
            }
        }
    }
}
