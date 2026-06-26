using System.Collections.Generic;
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

    public UnityEvent OnIncantationGenerated => onIncantationGenerated;
    public UnityEvent OnCorrectWord => onCorrectWord;
    public UnityEvent OnIncorrectWord => onIncorrectWord;
    public UnityEvent OnIncantationCompleted => onIncantationCompleted;

    public void GenerateIncantation()
    {
        ResetIncantation();

        List<IncantationWord> availableWords = GetUniquePossibleWords();
        int targetLength = Mathf.Min(Mathf.Max(0, incantationLength), availableWords.Count);

        if (targetLength < incantationLength)
            Debug.LogWarning("IncantationManager word library has fewer unique possible words than the requested incantation length.");

        for (int i = 0; i < targetLength; i++)
        {
            int randomIndex = Random.Range(0, availableWords.Count);
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

    public void ResetIncantation()
    {
        currentIncantation.Clear();
        CurrentWordIndex = 0;
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
            Debug.LogWarning("IncantationManager requires an IncantationWordLibrary reference.");
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

    private string NormalizeWord(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
            return string.Empty;

        return word.Trim().ToLowerInvariant();
    }
}
