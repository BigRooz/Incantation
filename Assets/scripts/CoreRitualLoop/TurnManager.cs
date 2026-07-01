using UnityEngine;

/// <summary>
/// Owns active player turn order and full table rotation tracking for the core ritual loop.
/// Depends only on a configured active player count supplied by the ritual setup flow.
/// Does not move the book, manage timers, validate phrases, listen to microphones, update UI, or raise gameplay events.
/// </summary>
public class TurnManager : MonoBehaviour
{
    [Header("Turn State")]
    [Tooltip("Number of active ritual participants in the current turn order.")]
    [SerializeField, Min(1)] private int activePlayerCount = 1;

    [Tooltip("Current active player index in the ritual turn order.")]
    [SerializeField, Min(0)] private int currentPlayerIndex;

    [Tooltip("Number of full table rotations completed since the last turn reset.")]
    [SerializeField, Min(0)] private int completedRotationCount;

    [Tooltip("True only when the most recent turn advance completed a full table rotation.")]
    [SerializeField] private bool completedRotationOnLastAdvance;

    /// <summary>
    /// Configures the number of active players participating in turn order.
    /// Player count is always clamped to at least one, and the current player index is kept valid.
    /// </summary>
    /// <param name="playerCount">The active player count supplied by ritual setup.</param>
    public void ConfigurePlayerCount(int playerCount)
    {
        activePlayerCount = Mathf.Max(1, playerCount);
        currentPlayerIndex = Mathf.Clamp(currentPlayerIndex, 0, activePlayerCount - 1);
        completedRotationOnLastAdvance = false;
    }

    /// <summary>
    /// Resets turn order to the first player and clears completed rotation state.
    /// </summary>
    public void ResetTurns()
    {
        currentPlayerIndex = 0;
        completedRotationCount = 0;
        completedRotationOnLastAdvance = false;
    }

    /// <summary>
    /// Gets the current active player index.
    /// </summary>
    /// <returns>A valid player index from zero to active player count minus one.</returns>
    public int GetCurrentPlayerIndex()
    {
        return currentPlayerIndex;
    }

    /// <summary>
    /// Advances to the next active player, wrapping to zero after the final player.
    /// Each wrap increments the completed rotation count exactly once.
    /// </summary>
    public void AdvanceTurn()
    {
        currentPlayerIndex++;

        if (currentPlayerIndex < activePlayerCount)
        {
            completedRotationOnLastAdvance = false;
            return;
        }

        currentPlayerIndex = 0;
        completedRotationCount++;
        completedRotationOnLastAdvance = true;
    }

    /// <summary>
    /// Reports whether the most recent turn advance completed a full table rotation.
    /// </summary>
    /// <returns>True when the previous AdvanceTurn call wrapped from the final player to player zero.</returns>
    public bool HasCompletedRotation()
    {
        return completedRotationOnLastAdvance;
    }

    /// <summary>
    /// Gets the number of full table rotations completed since the last reset.
    /// </summary>
    /// <returns>The completed full table rotation count.</returns>
    public int GetCompletedRotationCount()
    {
        return completedRotationCount;
    }

    /// <summary>
    /// Gets the configured active player count.
    /// </summary>
    /// <returns>The active player count, always at least one.</returns>
    public int GetActivePlayerCount()
    {
        return activePlayerCount;
    }

    private void OnValidate()
    {
        activePlayerCount = Mathf.Max(1, activePlayerCount);
        currentPlayerIndex = Mathf.Clamp(currentPlayerIndex, 0, activePlayerCount - 1);
        completedRotationCount = Mathf.Max(0, completedRotationCount);
    }
}
