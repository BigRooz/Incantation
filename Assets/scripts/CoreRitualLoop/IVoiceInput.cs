using System;

/// <summary>
/// Represents a generic voice input source for the Core Ritual Loop.
/// Captures recognized speech candidates from any provider without validating ritual correctness.
/// Future implementations may wrap Whisper, Windows keyword recognition, replay/testing input, or other provider adapters.
/// </summary>
public interface IVoiceInput
{
    /// <summary>
    /// Raised when the voice input source captures a spoken phrase candidate.
    /// </summary>
    event Action<string> OnPhraseRecognized;

    /// <summary>
    /// Starts capturing speech from the voice input source.
    /// </summary>
    void StartListening();

    /// <summary>
    /// Stops capturing speech and allows any pending recognition result to finish normally.
    /// </summary>
    void StopListening();

    /// <summary>
    /// Cancels the active listening session without waiting for additional recognition results.
    /// </summary>
    void CancelListening();
}
