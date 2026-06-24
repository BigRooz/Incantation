using System;

public interface IVoiceRecognizer
{
    bool IsListening { get; }

    event Action<string> OnPhraseRecognized;

    void StartListening();

    void StopListening();
}
