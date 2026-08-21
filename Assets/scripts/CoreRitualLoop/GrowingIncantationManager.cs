using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns the shared ritual phrase for the core ritual loop.
/// This is the only source of truth for the current visible incantation phrase.
/// It has no dependencies on players, turns, voice recognition, timers, book movement, UI, or gameplay orchestration.
/// </summary>
public class GrowingIncantationManager : MonoBehaviour
{
    [Header("Ritual Vocabulary")]
    [Tooltip("Canonical ritual words shuffled into a runtime order when the shared phrase resets for a new match.")]
    [SerializeField] private List<string> ritualVocabulary = new List<string>
    {
        "Umbra",
        "Vakor",
        "Mortis",
        "Noctis",
        "Korva",
        "Velum",
        "Zulak",
        "Dorim",
        "Rakan",
        "Nethor",
        "Inis",
        "Vexor",
        "Malum",
        "Luxor",
        "Tenebris"
    };

    [Header("Phrase State")]
    [Tooltip("Number of ritual words currently unlocked in the shared phrase.")]
    [SerializeField, Min(0)] private int currentUnlockedWordCount;

    [Tooltip("Current shared ritual phrase built from the unlocked vocabulary words.")]
    [SerializeField] private string currentRitualPhrase = string.Empty;

    [Tooltip("Current ritual words in phrase order.")]
    [SerializeField] private List<string> currentRitualWords = new List<string>();

    private readonly List<string> runtimeShuffledVocabulary = new List<string>();

    /// <summary>
    /// Resets the shared ritual phrase so it contains exactly one word.
    /// Empty or whitespace-only vocabulary entries are ignored.
    /// </summary>
    public void ResetPhrase()
    {
        currentRitualWords.Clear();
        runtimeShuffledVocabulary.Clear();
        currentUnlockedWordCount = 0;

        List<string> configuredVocabulary = GetConfiguredVocabulary();
        if (configuredVocabulary.Count == 0)
        {
            currentRitualPhrase = string.Empty;
            return;
        }

        AppendShuffledCycle(configuredVocabulary);
        currentUnlockedWordCount = 1;
        RebuildCurrentPhrase();
    }

    /// <summary>
    /// Gets the current shared ritual phrase as a single space-separated string.
    /// </summary>
    /// <returns>The current shared ritual phrase.</returns>
    public string GetCurrentPhrase()
    {
        return currentRitualPhrase;
    }

    /// <summary>
    /// Gets the currently unlocked ritual words in phrase order.
    /// </summary>
    /// <returns>A read-only list of the current ritual words.</returns>
    public IReadOnlyList<string> GetCurrentWords()
    {
        return currentRitualWords;
    }

    /// <summary>
    /// Gets the number of words currently unlocked in the shared ritual phrase.
    /// </summary>
    /// <returns>The current unlocked word count.</returns>
    public int GetCurrentWordCount()
    {
        return currentUnlockedWordCount;
    }

    /// <summary>
    /// Unlocks exactly one additional ritual word when ritual vocabulary is available.
    /// The configured vocabulary supplies the word order, but it does not cap phrase length.
    /// </summary>
    public void UnlockNextWord()
    {
        if (!CanUnlockNextWord())
        {
            return;
        }

        currentUnlockedWordCount++;
        RebuildCurrentPhrase();
    }

    /// <summary>
    /// Checks whether the ritual phrase can unlock one more vocabulary word.
    /// </summary>
    /// <returns>True when at least one valid vocabulary word is configured; otherwise false.</returns>
    public bool CanUnlockNextWord()
    {
        return GetConfiguredVocabulary().Count > 0;
    }

    private void RebuildCurrentPhrase()
    {
        List<string> configuredVocabulary = GetConfiguredVocabulary();

        currentRitualWords.Clear();

        if (configuredVocabulary.Count == 0)
        {
            currentUnlockedWordCount = 0;
            currentRitualPhrase = string.Empty;
            return;
        }

        EnsureRuntimeVocabularyCount(configuredVocabulary, currentUnlockedWordCount);

        for (int wordIndex = 0; wordIndex < currentUnlockedWordCount; wordIndex++)
        {
            currentRitualWords.Add(runtimeShuffledVocabulary[wordIndex]);
        }

        currentRitualPhrase = string.Join(" ", currentRitualWords);
    }

    private void EnsureRuntimeVocabularyCount(List<string> configuredVocabulary, int requiredWordCount)
    {
        while (runtimeShuffledVocabulary.Count < requiredWordCount)
        {
            string previousWord = runtimeShuffledVocabulary.Count > 0
                ? runtimeShuffledVocabulary[runtimeShuffledVocabulary.Count - 1]
                : string.Empty;
            AppendShuffledCycle(configuredVocabulary, previousWord);
        }
    }

    private void AppendShuffledCycle(List<string> configuredVocabulary, string previousWord = "")
    {
        List<string> shuffledCycle = new List<string>(configuredVocabulary);

        for (int wordIndex = shuffledCycle.Count - 1; wordIndex > 0; wordIndex--)
        {
            int swapIndex = Random.Range(0, wordIndex + 1);
            string swappedWord = shuffledCycle[wordIndex];
            shuffledCycle[wordIndex] = shuffledCycle[swapIndex];
            shuffledCycle[swapIndex] = swappedWord;
        }

        if (shuffledCycle.Count > 1 && shuffledCycle[0] == previousWord)
        {
            int swapIndex = Random.Range(1, shuffledCycle.Count);
            string firstWord = shuffledCycle[0];
            shuffledCycle[0] = shuffledCycle[swapIndex];
            shuffledCycle[swapIndex] = firstWord;
        }

        runtimeShuffledVocabulary.AddRange(shuffledCycle);
    }

    private List<string> GetConfiguredVocabulary()
    {
        List<string> configuredVocabulary = new List<string>();
        HashSet<string> configuredWords = new HashSet<string>();

        if (ritualVocabulary == null)
        {
            return configuredVocabulary;
        }

        foreach (string ritualWord in ritualVocabulary)
        {
            if (string.IsNullOrWhiteSpace(ritualWord))
            {
                continue;
            }

            string trimmedWord = ritualWord.Trim();

            if (configuredWords.Contains(trimmedWord))
            {
                continue;
            }

            configuredWords.Add(trimmedWord);
            configuredVocabulary.Add(trimmedWord);
        }

        return configuredVocabulary;
    }
}
