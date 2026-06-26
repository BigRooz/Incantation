using System;
using UnityEngine;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_WSA
using UnityEngine.Windows.Speech;
#endif

public class UnityDictationVoiceRecognizer : MonoBehaviour, IVoiceRecognizer
{
    [Header("Prototype")]
    [SerializeField] private bool logRecognizedPhrases = true;

    private bool isListening;
    private bool hasLoggedUnavailableWarning;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_WSA
    private DictationRecognizer dictationRecognizer;
#endif

    public bool IsListening => isListening;

    public event Action<string> OnPhraseRecognized;

    private void OnDisable()
    {
        StopListening();
    }

    private void OnDestroy()
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_WSA
        if (dictationRecognizer == null)
            return;

        StopListening();
        UnsubscribeFromDictationRecognizer();
        dictationRecognizer.Dispose();
        dictationRecognizer = null;
#endif
    }

    public void StartListening()
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_WSA
        if (!TryInitializeDictationRecognizer())
            return;

        if (isListening || dictationRecognizer.Status == SpeechSystemStatus.Running)
        {
            isListening = true;
            return;
        }

        try
        {
            dictationRecognizer.Start();
            isListening = true;
        }
        catch (Exception exception)
        {
            isListening = false;
            Debug.LogWarning($"Unity dictation could not start: {exception.Message}", this);
        }
#else
        LogDictationUnavailable();
#endif
    }

    public void StopListening()
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_WSA
        if (dictationRecognizer == null)
        {
            isListening = false;
            return;
        }

        if (!isListening && dictationRecognizer.Status != SpeechSystemStatus.Running)
            return;

        try
        {
            dictationRecognizer.Stop();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Unity dictation could not stop cleanly: {exception.Message}", this);
        }
        finally
        {
            isListening = false;
        }
#else
        isListening = false;
#endif
    }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_WSA
    private bool TryInitializeDictationRecognizer()
    {
        if (dictationRecognizer != null)
            return true;

        try
        {
            dictationRecognizer = new DictationRecognizer();
            dictationRecognizer.DictationResult += HandleDictationResult;
            dictationRecognizer.DictationError += HandleDictationError;
            dictationRecognizer.DictationComplete += HandleDictationComplete;
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Unity dictation is unavailable: {exception.Message}", this);
            return false;
        }
    }

    private void UnsubscribeFromDictationRecognizer()
    {
        dictationRecognizer.DictationResult -= HandleDictationResult;
        dictationRecognizer.DictationError -= HandleDictationError;
        dictationRecognizer.DictationComplete -= HandleDictationComplete;
    }

    private void HandleDictationResult(string text, ConfidenceLevel confidence)
    {
        if (logRecognizedPhrases)
            Debug.Log($"Voice recognized: {text}", this);

        OnPhraseRecognized?.Invoke(text);
    }

    private void HandleDictationError(string error, int hresult)
    {
        isListening = false;
        Debug.LogWarning($"Unity dictation error ({hresult}): {error}", this);
    }

    private void HandleDictationComplete(DictationCompletionCause completionCause)
    {
        isListening = false;

        if (completionCause != DictationCompletionCause.Complete)
            Debug.LogWarning($"Unity dictation stopped: {completionCause}", this);
    }
#else
    private void LogDictationUnavailable()
    {
        if (hasLoggedUnavailableWarning)
            return;

        Debug.LogWarning("Unity dictation is only available on Windows. Use MockVoiceRecognizer on this platform.", this);
        hasLoggedUnavailableWarning = true;
    }
#endif
}
