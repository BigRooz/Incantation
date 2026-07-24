/// <summary>
/// Defines the small local-only input contexts needed to keep gameplay shortcuts
/// separate from focused Living Book interaction.
/// </summary>
public enum LocalInputContext
{
    Gameplay,
    BookInteraction
}

/// <summary>
/// Owns the current input context for this local Unity player instance.
/// It has no network state and must never be synchronized between players.
/// </summary>
public static class LocalInputContextGate
{
    public static LocalInputContext Current { get; private set; } = LocalInputContext.Gameplay;
    public static bool AllowsGameplayInput => Current == LocalInputContext.Gameplay;
    public static bool IsBookInteractionActive => Current == LocalInputContext.BookInteraction;
    public static event System.Action<LocalInputContext> ContextChanged;

    public static void SetContext(LocalInputContext context)
    {
        if (Current == context)
            return;

        Current = context;
        ContextChanged?.Invoke(Current);
    }

    public static void RestoreGameplay()
    {
        Current = LocalInputContext.Gameplay;
    }

    [UnityEngine.RuntimeInitializeOnLoadMethod(
        UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        Current = LocalInputContext.Gameplay;
        ContextChanged = null;
    }
}
