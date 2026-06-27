using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_WSA
using UnityEngine.Windows.Speech;
#endif

public class WindowsKeywordVoiceRecognizer : MonoBehaviour, IVoiceRecognizer
{
    [Header("References")]
    [SerializeField] private IncantationWordLibrary wordLibrary;

    [Header("Debug")]
    [SerializeField] private bool logRecognizedPhrases = true;

    private bool isListening;
    private bool hasLoggedUnavailableWarning;
    private bool hasLoggedMissingLibraryWarning;
    private bool hasLoggedEmptyKeywordWarning;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_WSA
    private KeywordRecognizer keywordRecognizer;
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
        DisposeKeywordRecognizer();
#endif
    }

    public void StartListening()
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_WSA
        if (!TryInitializeKeywordRecognizer())
            return;

        if (isListening || keywordRecognizer.IsRunning)
        {
            isListening = true;
            return;
        }

        try
        {
            keywordRecognizer.Start();
            isListening = true;
        }
        catch (Exception exception)
        {
            isListening = false;
            Debug.LogWarning($"Windows keyword recognition could not start: {exception.Message}", this);
        }
#else
        isListening = false;
        LogKeywordRecognitionUnavailable();
#endif
    }

    public void StopListening()
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_WSA
        if (keywordRecognizer == null)
        {
            isListening = false;
            return;
        }

        if (!isListening && !keywordRecognizer.IsRunning)
            return;

        try
        {
            keywordRecognizer.Stop();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Windows keyword recognition could not stop cleanly: {exception.Message}", this);
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
    private bool TryInitializeKeywordRecognizer()
    {
        if (keywordRecognizer != null)
            return true;

        string[] keywords = BuildKeywords();

        if (keywords.Length == 0)
            return false;

        try
        {
            keywordRecognizer = new KeywordRecognizer(keywords);
            keywordRecognizer.OnPhraseRecognized += HandleKeywordRecognized;
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Windows keyword recognition is unavailable: {exception.Message}", this);
            return false;
        }
    }

    private string[] BuildKeywords()
    {
        IncantationWordLibrary resolvedWordLibrary = ResolveWordLibrary();

        if (resolvedWordLibrary == null)
            return Array.Empty<string>();

        HashSet<string> keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (IncantationWord word in resolvedWordLibrary.GetWords())
        {
            if (word == null)
                continue;

            AddKeyword(keywords, word.Word);
        }

        foreach (IncantationWordLibrary.SpeechAliasMapping speechAliasMapping in resolvedWordLibrary.GetSpeechAliasMappings())
        {
            if (speechAliasMapping == null)
                continue;

            AddKeyword(keywords, speechAliasMapping.Alias);
        }

        if (keywords.Count == 0)
            LogEmptyKeywordList();

        string[] keywordArray = new string[keywords.Count];
        keywords.CopyTo(keywordArray);
        return keywordArray;
    }

    private void AddKeyword(HashSet<string> keywords, string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return;

        keywords.Add(keyword.Trim());
    }

    private IncantationWordLibrary ResolveWordLibrary()
    {
        if (wordLibrary == null)
            wordLibrary = GetComponent<IncantationWordLibrary>();

        if (wordLibrary == null)
            wordLibrary = FindFirstObjectByType<IncantationWordLibrary>();

        if (wordLibrary == null)
            LogMissingWordLibrary();

        return wordLibrary;
    }

    private void HandleKeywordRecognized(PhraseRecognizedEventArgs args)
    {
        if (logRecognizedPhrases)
            Debug.Log($"Keyword recognized: {args.text}", this);

        OnPhraseRecognized?.Invoke(args.text);
    }

    private void DisposeKeywordRecognizer()
    {
        if (keywordRecognizer == null)
            return;

        StopListening();
        keywordRecognizer.OnPhraseRecognized -= HandleKeywordRecognized;
        keywordRecognizer.Dispose();
        keywordRecognizer = null;
    }
#else
    private void LogKeywordRecognitionUnavailable()
    {
        if (hasLoggedUnavailableWarning)
            return;

        Debug.LogWarning("Windows keyword recognition is only available on Windows. Use MockVoiceRecognizer on this platform.", this);
        hasLoggedUnavailableWarning = true;
    }
#endif

    private void LogMissingWordLibrary()
    {
        if (hasLoggedMissingLibraryWarning)
            return;

        Debug.LogWarning("WindowsKeywordVoiceRecognizer requires an IncantationWordLibrary reference.", this);
        hasLoggedMissingLibraryWarning = true;
    }

    private void LogEmptyKeywordList()
    {
        if (hasLoggedEmptyKeywordWarning)
            return;

        Debug.LogWarning("WindowsKeywordVoiceRecognizer has no incantation words or speech aliases to recognize.", this);
        hasLoggedEmptyKeywordWarning = true;
    }
}
