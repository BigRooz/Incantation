using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;

public class IncantationManager : MonoBehaviour
{
    private const int DefaultIncantationLength = 5;

    [Header("Settings")]
    [SerializeField] private int incantationLength = DefaultIncantationLength;
    [SerializeField] private IncantationWordLibrary wordLibrary;

    [Header("Events")]
    [SerializeField] private UnityEvent onIncantationGenerated = new UnityEvent();
    [SerializeField] private UnityEvent onCorrectWord = new UnityEvent();
    [SerializeField] private UnityEvent onIncorrectWord = new UnityEvent();
    [SerializeField] private UnityEvent onIncantationCompleted = new UnityEvent();

    private readonly List<IncantationWord> currentIncantation = new List<IncantationWord>();
    private PhraseValidationResult activePhraseValidationResult;
    private int activePhraseReplayIndex;
    private bool hasActivePhraseReplay;

    public string CurrentWord
    {
        get
        {
            if (IsCompleted || currentIncantation.Count == 0)
                return string.Empty;

            return currentIncantation[CurrentWordIndex].Text;
        }
    }

    public int CurrentWordIndex { get; private set; }

    public IReadOnlyList<IncantationWord> CurrentIncantation => currentIncantation;

    public IReadOnlyList<IncantationWord> CompletedWords
    {
        get
        {
            List<IncantationWord> completedWords = new List<IncantationWord>();

            foreach (IncantationWord word in currentIncantation)
            {
                if (word.IsCompleted)
                    completedWords.Add(word);
            }

            return completedWords;
        }
    }

    public bool IsCompleted => currentIncantation.Count > 0 && CurrentWordIndex >= currentIncantation.Count;
    public bool HasActivePhraseReplay => hasActivePhraseReplay;
    public bool IsPhraseReplayComplete => !hasActivePhraseReplay;
    public PhraseValidationResult LastPhraseValidationResult { get; private set; }

    public UnityEvent OnIncantationGenerated => onIncantationGenerated;
    public UnityEvent OnCorrectWord => onCorrectWord;
    public UnityEvent OnIncorrectWord => onIncorrectWord;
    public UnityEvent OnIncantationCompleted => onIncantationCompleted;
    public event Action OnPhraseReplayReset;
    public event Action<PhraseValidationWordResult> OnPhraseReplayAcceptedWord;
    public event Action<PhraseValidationWordResult> OnPhraseReplayRejectedWord;
    public event Action OnPhraseReplayFinished;

    public void GenerateIncantation()
    {
        ResetIncantation();

        List<IncantationWord> availableWords = GetUniquePossibleWords();
        int targetLength = Mathf.Min(Mathf.Max(0, incantationLength), availableWords.Count);

        if (targetLength < incantationLength)
            Debug.LogWarning("IncantationManager word library has fewer unique possible words than the requested incantation length.");

        for (int i = 0; i < targetLength; i++)
        {
            int randomIndex = UnityEngine.Random.Range(0, availableWords.Count);
            IncantationWord selectedWord = availableWords[randomIndex];

            currentIncantation.Add(new IncantationWord(selectedWord.Word, selectedWord.SpeechAliases));
            availableWords.RemoveAt(randomIndex);
        }

        onIncantationGenerated.Invoke();
    }

    public bool TryCompleteCurrentWord(string spokenWord)
    {
        if (IsCompleted || currentIncantation.Count == 0)
        {
            onIncorrectWord.Invoke();
            return false;
        }

        string normalizedSpokenWord = NormalizeWord(spokenWord);
        string normalizedCurrentWord = NormalizeWord(CurrentWord);

        if (normalizedSpokenWord != normalizedCurrentWord)
        {
            onIncorrectWord.Invoke();
            return false;
        }

        currentIncantation[CurrentWordIndex].MarkCompleted();
        CurrentWordIndex++;
        onCorrectWord.Invoke();

        if (IsCompleted)
            onIncantationCompleted.Invoke();

        return true;
    }

    public bool TryValidateCurrentWordRealtime(string spokenWord, VoicePhraseNormalizer phraseNormalizer)
    {
        if (IsCompleted || currentIncantation.Count == 0)
        {
            onIncorrectWord.Invoke();
            return false;
        }

        PhraseValidationResult result = EvaluateCurrentWordRealtime(
            spokenWord,
            phraseNormalizer);
        ApplyAuthoritativeWordValidation(result);
        return result.IsSuccess;
    }

    public bool TryCompleteCurrentPhrase(string spokenPhrase, VoicePhraseNormalizer phraseNormalizer)
    {
        if (IsCompleted || currentIncantation.Count == 0)
        {
            onIncorrectWord.Invoke();
            return false;
        }

        StartPhraseJudgmentReplay(spokenPhrase, phraseNormalizer);

        return IsCompleted && LastPhraseValidationResult.IsSuccess;
    }

    public PhraseValidationResult StartPhraseJudgmentReplay(string spokenPhrase, VoicePhraseNormalizer phraseNormalizer)
    {
        activePhraseValidationResult = EvaluateCurrentPhrase(
            spokenPhrase,
            phraseNormalizer);
        LastPhraseValidationResult = activePhraseValidationResult;
        activePhraseReplayIndex = 0;
        hasActivePhraseReplay = activePhraseValidationResult.WordTimeline.Length > 0;
        OnPhraseReplayReset?.Invoke();

        if (!hasActivePhraseReplay)
            OnPhraseReplayFinished?.Invoke();
        else
            ReplayActivePhraseJudgment();

        return activePhraseValidationResult;
    }

    public PhraseValidationResult EvaluateCurrentWordRealtime(
        string spokenWord,
        VoicePhraseNormalizer phraseNormalizer)
    {
        if (IsCompleted || currentIncantation.Count == 0)
        {
            return PhraseValidator.ValidateWord(
                string.Empty,
                spokenWord,
                CurrentWordIndex);
        }

        return PhraseValidator.ValidateWord(
            NormalizeWord(CurrentWord),
            NormalizePhrase(spokenWord, phraseNormalizer),
            CurrentWordIndex);
    }

    public PhraseValidationResult EvaluateCurrentPhrase(
        string spokenPhrase,
        VoicePhraseNormalizer phraseNormalizer)
    {
        string normalizedSpokenPhrase = NormalizePhrase(
            spokenPhrase,
            phraseNormalizer);
        string normalizedExpectedPhrase = NormalizePhrase(
            GetCurrentIncantationText(),
            null);
        return PhraseValidator.Validate(
            normalizedExpectedPhrase,
            normalizedSpokenPhrase);
    }

    public void ApplyAuthoritativeWordValidation(PhraseValidationResult result)
    {
        activePhraseValidationResult = result;
        LastPhraseValidationResult = result;
        activePhraseReplayIndex = 0;
        hasActivePhraseReplay = true;
        OnPhraseReplayReset?.Invoke();

        if (result.WordTimeline.Length == 0)
        {
            hasActivePhraseReplay = false;
            OnPhraseReplayFinished?.Invoke();
            return;
        }

        PhraseValidationWordResult wordResult = result.WordTimeline[0];
        hasActivePhraseReplay = false;

        if (result.IsSuccess && !IsCompleted)
        {
            currentIncantation[CurrentWordIndex].MarkCompleted();
            CurrentWordIndex++;
            onCorrectWord.Invoke();
            OnPhraseReplayAcceptedWord?.Invoke(wordResult);

            if (IsCompleted)
                onIncantationCompleted.Invoke();
        }
        else
        {
            onIncorrectWord.Invoke();
            OnPhraseReplayRejectedWord?.Invoke(wordResult);
            ResetCurrentPhraseProgress();
        }

        OnPhraseReplayFinished?.Invoke();
    }

    public void ApplyAuthoritativePhraseValidation(PhraseValidationResult result)
    {
        activePhraseValidationResult = result;
        LastPhraseValidationResult = result;
        activePhraseReplayIndex = 0;
        hasActivePhraseReplay = result.WordTimeline.Length > 0;
        OnPhraseReplayReset?.Invoke();

        if (!hasActivePhraseReplay)
            OnPhraseReplayFinished?.Invoke();
        else
            ReplayActivePhraseJudgment();
    }

    public PhraseValidationResult BuildAuthoritativeValidationResult(
        bool isAccepted,
        int acceptedWordCount,
        int firstRejectedWordIndex,
        string rejectedWord,
        PhraseValidationFailureReason failureReason)
    {
        int clampedAcceptedWordCount = Mathf.Clamp(
            acceptedWordCount,
            0,
            currentIncantation.Count);
        int timelineLength = clampedAcceptedWordCount;
        if (!isAccepted &&
            firstRejectedWordIndex >= 0 &&
            firstRejectedWordIndex < currentIncantation.Count)
        {
            timelineLength = Mathf.Max(
                timelineLength,
                firstRejectedWordIndex + 1);
        }

        PhraseValidationWordResult[] timeline =
            new PhraseValidationWordResult[timelineLength];
        for (int wordIndex = 0; wordIndex < timelineLength; wordIndex++)
        {
            string expectedWord = currentIncantation[wordIndex].Text;
            bool acceptedWord = wordIndex < clampedAcceptedWordCount;
            string receivedWord = acceptedWord
                ? expectedWord
                : rejectedWord ?? string.Empty;
            PhraseValidationWordState state = acceptedWord
                ? PhraseValidationWordState.Success
                : string.IsNullOrEmpty(receivedWord)
                    ? PhraseValidationWordState.Missing
                    : PhraseValidationWordState.Failed;
            timeline[wordIndex] = new PhraseValidationWordResult(
                wordIndex,
                expectedWord,
                receivedWord,
                state);
        }

        return new PhraseValidationResult(
            isAccepted,
            clampedAcceptedWordCount,
            firstRejectedWordIndex,
            failureReason,
            timeline);
    }

    public PhraseValidationResult BuildAuthoritativeWordValidationResult(
        bool isAccepted,
        int wordIndex,
        string expectedWord,
        string rejectedWord,
        PhraseValidationFailureReason failureReason)
    {
        PhraseValidationWordResult wordResult =
            new PhraseValidationWordResult(
                wordIndex,
                expectedWord ?? string.Empty,
                rejectedWord ?? string.Empty,
                isAccepted
                    ? PhraseValidationWordState.Success
                    : PhraseValidationWordState.Failed);
        return new PhraseValidationResult(
            isAccepted,
            isAccepted ? 1 : 0,
            isAccepted ? -1 : wordIndex,
            failureReason,
            new[] { wordResult });
    }

    public void ApplyAuthoritativePhraseState(
        string[] words,
        int expectedWordIndex)
    {
        string[] safeWords = words ?? Array.Empty<string>();
        bool phraseChanged = currentIncantation.Count != safeWords.Length;
        if (!phraseChanged)
        {
            for (int wordIndex = 0;
                wordIndex < safeWords.Length;
                wordIndex++)
            {
                if (string.Equals(
                        currentIncantation[wordIndex]?.Text,
                        safeWords[wordIndex],
                        StringComparison.Ordinal))
                {
                    continue;
                }

                phraseChanged = true;
                break;
            }
        }

        if (phraseChanged)
        {
            currentIncantation.Clear();
            foreach (string word in safeWords)
            {
                currentIncantation.Add(new IncantationWord(word));
            }

            onIncantationGenerated.Invoke();
        }

        CurrentWordIndex = Mathf.Clamp(
            expectedWordIndex,
            0,
            currentIncantation.Count);
        for (int wordIndex = 0;
            wordIndex < currentIncantation.Count;
            wordIndex++)
        {
            if (wordIndex < CurrentWordIndex)
                currentIncantation[wordIndex].MarkCompleted();
            else
                currentIncantation[wordIndex].MarkIncomplete();
        }

        ResetPhraseReplayFeedback();
    }

    public void ResetCurrentPhraseProgress()
    {
        CurrentWordIndex = 0;

        foreach (IncantationWord word in currentIncantation)
        {
            if (word != null)
                word.MarkIncomplete();
        }
    }

    public void ResetPhraseReplayFeedback()
    {
        activePhraseValidationResult = default(PhraseValidationResult);
        activePhraseReplayIndex = 0;
        hasActivePhraseReplay = false;
        OnPhraseReplayReset?.Invoke();
    }

    public bool TryAdvancePhraseJudgmentReplay(out PhraseValidationWordResult replayedWord)
    {
        replayedWord = default(PhraseValidationWordResult);

        if (!hasActivePhraseReplay)
            return false;

        if (activePhraseReplayIndex >= activePhraseValidationResult.WordTimeline.Length)
        {
            hasActivePhraseReplay = false;
            OnPhraseReplayFinished?.Invoke();
            return false;
        }

        replayedWord = activePhraseValidationResult.WordTimeline[activePhraseReplayIndex];
        activePhraseReplayIndex++;

        if (replayedWord.State == PhraseValidationWordState.Success)
        {
            if (IsCompleted)
            {
                hasActivePhraseReplay = false;
                return true;
            }

            currentIncantation[CurrentWordIndex].MarkCompleted();
            CurrentWordIndex++;
            onCorrectWord.Invoke();
            OnPhraseReplayAcceptedWord?.Invoke(replayedWord);

            if (IsCompleted && activePhraseValidationResult.IsSuccess)
            {
                hasActivePhraseReplay = false;
                onIncantationCompleted.Invoke();
                OnPhraseReplayFinished?.Invoke();
            }

            return true;
        }

        hasActivePhraseReplay = false;
        onIncorrectWord.Invoke();
        OnPhraseReplayRejectedWord?.Invoke(replayedWord);
        OnPhraseReplayFinished?.Invoke();
        return true;
    }

    public void ResetIncantation()
    {
        currentIncantation.Clear();
        CurrentWordIndex = 0;
        activePhraseValidationResult = default(PhraseValidationResult);
        LastPhraseValidationResult = default(PhraseValidationResult);
        activePhraseReplayIndex = 0;
        hasActivePhraseReplay = false;
        OnPhraseReplayReset?.Invoke();
    }

    private void ReplayActivePhraseJudgment()
    {
        PhraseValidationWordResult replayedWord;

        while (TryAdvancePhraseJudgmentReplay(out replayedWord))
        {
        }
    }

    private List<IncantationWord> GetUniquePossibleWords()
    {
        List<IncantationWord> uniqueWords = new List<IncantationWord>();

        foreach (IncantationWord possibleWord in GetLibraryWords())
        {
            if (possibleWord == null)
                continue;

            string normalizedWord = NormalizeWord(possibleWord.Word);

            if (string.IsNullOrEmpty(normalizedWord))
                continue;

            if (ContainsNormalizedWord(uniqueWords, normalizedWord))
                continue;

            uniqueWords.Add(possibleWord);
        }

        return uniqueWords;
    }

    private IReadOnlyList<IncantationWord> GetLibraryWords()
    {
        if (wordLibrary == null)
            wordLibrary = GetComponent<IncantationWordLibrary>();

        if (wordLibrary == null)
            wordLibrary = FindFirstObjectByType<IncantationWordLibrary>();

        if (wordLibrary == null)
        {
            Debug.LogWarning($"{nameof(IncantationManager)} on '{gameObject.name}' is missing required reference '{nameof(wordLibrary)}'. Assign an IncantationWordLibrary in the Inspector.", this);
            return new List<IncantationWord>();
        }

        return wordLibrary.Words;
    }

    private bool ContainsNormalizedWord(List<IncantationWord> words, string normalizedWord)
    {
        foreach (IncantationWord word in words)
        {
            if (NormalizeWord(word.Word) == normalizedWord)
                return true;
        }

        return false;
    }

    public string GetCurrentIncantationText()
    {
        if (currentIncantation.Count == 0)
            return string.Empty;

        StringBuilder builder = new StringBuilder();

        for (int i = 0; i < currentIncantation.Count; i++)
        {
            if (i > 0)
                builder.Append(' ');

            builder.Append(currentIncantation[i].Text);
        }

        return builder.ToString();
    }

    private string NormalizePhrase(string phrase, VoicePhraseNormalizer phraseNormalizer)
    {
        if (phraseNormalizer != null)
            return phraseNormalizer.NormalizePhrase(phrase);

        return NormalizeWord(phrase);
    }

    private string NormalizeWord(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
            return string.Empty;

        StringBuilder builder = new StringBuilder();
        bool previousWasWhitespace = true;

        foreach (char character in word)
        {
            if (char.IsPunctuation(character))
                continue;

            if (char.IsWhiteSpace(character))
            {
                if (!previousWasWhitespace)
                {
                    builder.Append(' ');
                    previousWasWhitespace = true;
                }

                continue;
            }

            builder.Append(char.ToLowerInvariant(character));
            previousWasWhitespace = false;
        }

        return builder.ToString().Trim();
    }
}
