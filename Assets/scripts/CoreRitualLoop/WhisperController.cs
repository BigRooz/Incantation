using UnityEngine;

/// <summary>
/// Future wrapper for coordinating Whisper listening flow without implementing recognition directly.
/// </summary>
public class WhisperController : MonoBehaviour
{
    [Header("Listening State")]
    [Tooltip("Future read-only state for whether a ritual listening session is active.")]
    [SerializeField] private bool isListening;

    public bool IsListening => isListening;

    /*
     * Responsibility:
     * - Start a future listening session.
     * - Stop a future listening session.
     * - Cancel a future listening session.
     *
     * TODO:
     * - Wrap WhisperVoiceRecognizer only during a future migration task.
     * - Forward recognized phrase candidates to the ritual orchestrator.
     * - Keep Windows speech fallback orchestration separate from the primary Whisper path.
     */

    public void StartListening()
    {
        // TODO: Future migration will start the primary Whisper listening path.
    }

    public void StopListening()
    {
        // TODO: Future migration will stop listening and allow result handling.
    }

    public void CancelListening()
    {
        // TODO: Future migration will cancel listening without accepting a result.
    }
}
