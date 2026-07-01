using UnityEngine;

/// <summary>
/// Future top-level orchestrator for the clean core ritual loop.
/// </summary>
public class CoreRitualLoop : MonoBehaviour
{
    [Header("Future Managers")]
    [Tooltip("Future owner of active player order, player advancement, and rotation detection.")]
    [SerializeField] private TurnManager turnManager;

    [Tooltip("Future owner of shared phrase state, difficulty, and rotation-based phrase growth.")]
    [SerializeField] private GrowingIncantationManager growingIncantationManager;

    [Tooltip("Future owner of phrase comparison rules for ritual speech.")]
    [SerializeField] private PhraseValidator phraseValidator;

    [Tooltip("Future wrapper for Whisper listening flow. This is not the recognizer implementation.")]
    [SerializeField] private WhisperController whisperController;

    /*
     * Responsibility:
     * - Coordinate future ritual phases once the existing loop is ready to be replaced.
     * - Keep orchestration separate from book movement, voice capture, seats, timers, and UI.
     *
     * TODO:
     * - Define ritual phase events.
     * - Coordinate turn start, listening, phrase validation, retry, timeout, and elimination flow.
     * - Connect to existing gameplay systems only in a future migration task.
     */
}
