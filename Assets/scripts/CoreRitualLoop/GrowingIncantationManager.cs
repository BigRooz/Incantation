using UnityEngine;

/// <summary>
/// Future owner of the shared growing ritual phrase and ritual difficulty state.
/// </summary>
public class GrowingIncantationManager : MonoBehaviour
{
    [Header("Phrase State")]
    [Tooltip("Future shared ritual phrase.")]
    [SerializeField] private string currentPhrase = string.Empty;

    [Tooltip("Future difficulty value used to control phrase growth.")]
    [SerializeField, Min(0)] private int currentDifficulty;

    public string CurrentPhrase => currentPhrase;
    public int CurrentDifficulty => currentDifficulty;

    /*
     * Responsibility:
     * - Own only the current shared ritual phrase.
     * - Own only the current phrase difficulty.
     * - Add one word after a full table rotation.
     *
     * TODO:
     * - Receive ritual vocabulary from the future ritual word source.
     * - Build phrase growth rules without depending on turn order, recognition, UI, or book movement.
     * - Raise phrase changed events once the orchestrator is ready.
     */

    public void ResetPhrase()
    {
        // TODO: Future migration will initialize the phrase from ritual vocabulary.
        currentPhrase = string.Empty;
        currentDifficulty = 0;
    }

    public void AddWordAfterFullRotation()
    {
        // TODO: Future migration will append one ritual word after a full table rotation.
    }

    public void SetDifficulty(int newDifficulty)
    {
        // TODO: Future migration will define how difficulty maps to phrase length or word choices.
        currentDifficulty = Mathf.Max(0, newDifficulty);
    }
}
