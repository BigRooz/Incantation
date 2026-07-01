using UnityEngine;

/// <summary>
/// Provides the narrow adapter surface between legacy ritual gameplay and CoreRitualLoop.
/// Depends only on CoreRitualLoop and forwards calls without owning gameplay, validation,
/// phrase, timer, networking, or voice-recognition rules.
/// TODO: Wire legacy callers to this bridge during the RitualController migration.
/// </summary>
public class CoreRitualLoopBridge : MonoBehaviour
{
    [Tooltip("Core ritual engine that owns ritual loop coordination.")]
    [SerializeField] private CoreRitualLoop coreRitualLoop;

    /// <summary>
    /// Forwards active player count configuration to CoreRitualLoop.
    /// </summary>
    /// <param name="playerCount">The number of active ritual participants.</param>
    public void ConfigurePlayerCount(int playerCount)
    {
        if (!HasCoreRitualLoop())
        {
            return;
        }

        coreRitualLoop.ConfigurePlayerCount(playerCount);
    }

    /// <summary>
    /// Forwards ritual state reset to CoreRitualLoop.
    /// </summary>
    public void ResetGame()
    {
        if (!HasCoreRitualLoop())
        {
            return;
        }

        coreRitualLoop.ResetGame();
    }

    /// <summary>
    /// Forwards current player index lookup to CoreRitualLoop.
    /// </summary>
    /// <returns>The current active player index reported by CoreRitualLoop.</returns>
    public int GetCurrentPlayerIndex()
    {
        if (!HasCoreRitualLoop())
        {
            return 0;
        }

        return coreRitualLoop.GetCurrentPlayerIndex();
    }

    /// <summary>
    /// Forwards current phrase lookup to CoreRitualLoop.
    /// </summary>
    /// <returns>The current shared ritual phrase reported by CoreRitualLoop.</returns>
    public string GetCurrentPhrase()
    {
        if (!HasCoreRitualLoop())
        {
            return string.Empty;
        }

        return coreRitualLoop.GetCurrentPhrase();
    }

    /// <summary>
    /// Forwards recognized phrase validation to CoreRitualLoop.
    /// </summary>
    /// <param name="recognizedPhrase">The recognized phrase candidate.</param>
    /// <returns>True when CoreRitualLoop accepts the phrase candidate.</returns>
    public bool ValidatePhrase(string recognizedPhrase)
    {
        if (!HasCoreRitualLoop())
        {
            return false;
        }

        return coreRitualLoop.ValidateCurrentPhrase(recognizedPhrase);
    }

    /// <summary>
    /// Forwards successful turn advancement to CoreRitualLoop.
    /// </summary>
    public void AdvanceSuccessfulTurn()
    {
        if (!HasCoreRitualLoop())
        {
            return;
        }

        coreRitualLoop.AdvanceSuccessfulTurn();
    }

    /// <summary>
    /// Forwards failed turn advancement to CoreRitualLoop.
    /// </summary>
    public void AdvanceFailedTurn()
    {
        if (!HasCoreRitualLoop())
        {
            return;
        }

        coreRitualLoop.AdvanceFailedTurn();
    }

    private bool HasCoreRitualLoop()
    {
        if (coreRitualLoop != null)
        {
            return true;
        }

        Debug.LogError($"{nameof(CoreRitualLoopBridge)} requires a {nameof(CoreRitualLoop)} reference.", this);
        return false;
    }
}
