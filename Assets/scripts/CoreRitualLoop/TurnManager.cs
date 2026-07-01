using UnityEngine;

/// <summary>
/// Future owner of ritual turn order, active player index, and full table rotation detection.
/// </summary>
public class TurnManager : MonoBehaviour
{
    [Header("Turn State")]
    [Tooltip("Future active player index in the seated ritual order.")]
    [SerializeField] private int currentPlayerIndex;

    [Tooltip("Future number of seated ritual participants.")]
    [SerializeField, Min(0)] private int playerCount;

    [Tooltip("Future count of turns completed in the current table rotation.")]
    [SerializeField, Min(0)] private int turnsCompletedThisRotation;

    public int CurrentPlayerIndex => currentPlayerIndex;
    public int PlayerCount => playerCount;
    public int TurnsCompletedThisRotation => turnsCompletedThisRotation;

    /*
     * Responsibility:
     * - Track only whose turn it is.
     * - Advance to the next player in the ritual order.
     * - Report when a full table rotation has completed.
     *
     * TODO:
     * - Accept a future seated player count from the ritual setup flow.
     * - Skip eliminated players once elimination is migrated.
     * - Raise turn and rotation events for the orchestrator.
     */

    public void SetPlayerCount(int newPlayerCount)
    {
        // TODO: Future migration will validate seated players before assigning this value.
        playerCount = Mathf.Max(0, newPlayerCount);
    }

    public void ResetTurnOrder()
    {
        // TODO: Future migration will reset using the seated ritual order.
        currentPlayerIndex = 0;
        turnsCompletedThisRotation = 0;
    }

    public bool AdvanceToNextPlayer()
    {
        // TODO: Future migration will advance through live seated players.
        return false;
    }

    public bool HasCompletedFullRotation()
    {
        // TODO: Future migration will detect completion after every seated player has taken a turn.
        return false;
    }
}
