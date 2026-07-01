using UnityEngine;

/// <summary>
/// Orchestrates the logic-only core ritual loop by coordinating turn order, shared phrase state,
/// and phrase validation through their focused production managers.
/// Depends on TurnManager, GrowingIncantationManager, and PhraseValidator.
/// Does not know about Whisper, book movement, UI, timers, networking, scenes, or gameplay visuals.
/// TODO: Integrate with ritual phase events after the logic loop is validated in isolation.
/// </summary>
public class CoreRitualLoop : MonoBehaviour
{
    [Header("Core Logic Managers")]
    [Tooltip("Owns active player order and full table rotation detection.")]
    [SerializeField] private TurnManager turnManager;

    [Tooltip("Owns shared ritual phrase state and rotation-based phrase growth.")]
    [SerializeField] private GrowingIncantationManager growingIncantationManager;

    [Tooltip("Owns full visible phrase comparison rules for ritual speech.")]
    [SerializeField] private PhraseValidator phraseValidator;

    /// <summary>
    /// Configures the number of active ritual participants.
    /// Turn order ownership remains inside TurnManager.
    /// </summary>
    /// <param name="playerCount">The active player count supplied by ritual setup.</param>
    public void ConfigurePlayerCount(int playerCount)
    {
        if (turnManager == null)
        {
            Debug.LogError($"{nameof(CoreRitualLoop)} requires a {nameof(TurnManager)} reference.", this);
            return;
        }

        turnManager.ConfigurePlayerCount(playerCount);
    }

    /// <summary>
    /// Resets ritual logic to the first player and the initial one-word phrase.
    /// </summary>
    public void ResetGame()
    {
        if (!HasRequiredStateManagers())
        {
            return;
        }

        turnManager.ResetTurns();
        growingIncantationManager.ResetPhrase();
    }

    /// <summary>
    /// Gets the current active player index from TurnManager.
    /// </summary>
    /// <returns>The current active player index, or zero when TurnManager is missing.</returns>
    public int GetCurrentPlayerIndex()
    {
        if (turnManager == null)
        {
            Debug.LogError($"{nameof(CoreRitualLoop)} requires a {nameof(TurnManager)} reference.", this);
            return 0;
        }

        return turnManager.GetCurrentPlayerIndex();
    }

    /// <summary>
    /// Gets the current shared ritual phrase from GrowingIncantationManager.
    /// </summary>
    /// <returns>The current shared ritual phrase, or an empty phrase when the phrase manager is missing.</returns>
    public string GetCurrentPhrase()
    {
        if (growingIncantationManager == null)
        {
            Debug.LogError($"{nameof(CoreRitualLoop)} requires a {nameof(GrowingIncantationManager)} reference.", this);
            return string.Empty;
        }

        return growingIncantationManager.GetCurrentPhrase();
    }

    /// <summary>
    /// Validates spoken text against the current full visible ritual phrase.
    /// Phrase comparison ownership remains inside PhraseValidator.
    /// </summary>
    /// <param name="recognizedPhrase">The phrase candidate recognized by a future voice system.</param>
    /// <returns>True when the recognized phrase satisfies the current ritual phrase.</returns>
    public bool ValidateCurrentPhrase(string recognizedPhrase)
    {
        if (!HasRequiredValidationManagers())
        {
            return false;
        }

        return phraseValidator.ValidatePhrase(growingIncantationManager.GetCurrentPhrase(), recognizedPhrase);
    }

    /// <summary>
    /// Advances a successful ritual turn and unlocks exactly one word after a completed table rotation.
    /// Turn progression ownership remains inside TurnManager.
    /// Phrase growth ownership remains inside GrowingIncantationManager.
    /// </summary>
    public void AdvanceSuccessfulTurn()
    {
        if (!HasRequiredStateManagers())
        {
            return;
        }

        turnManager.AdvanceTurn();

        if (turnManager.HasCompletedRotation())
        {
            growingIncantationManager.UnlockNextWord();
        }
    }

    /// <summary>
    /// Placeholder for future failure, retry, timeout, and elimination rules.
    /// This intentionally does nothing in the logic-only implementation.
    /// </summary>
    public void AdvanceFailedTurn()
    {
    }

    private bool HasRequiredStateManagers()
    {
        bool hasRequiredManagers = true;

        if (turnManager == null)
        {
            Debug.LogError($"{nameof(CoreRitualLoop)} requires a {nameof(TurnManager)} reference.", this);
            hasRequiredManagers = false;
        }

        if (growingIncantationManager == null)
        {
            Debug.LogError($"{nameof(CoreRitualLoop)} requires a {nameof(GrowingIncantationManager)} reference.", this);
            hasRequiredManagers = false;
        }

        return hasRequiredManagers;
    }

    private bool HasRequiredValidationManagers()
    {
        bool hasRequiredManagers = HasRequiredStateManagers();

        if (phraseValidator == null)
        {
            Debug.LogError($"{nameof(CoreRitualLoop)} requires a {nameof(PhraseValidator)} reference.", this);
            hasRequiredManagers = false;
        }

        return hasRequiredManagers;
    }
}
