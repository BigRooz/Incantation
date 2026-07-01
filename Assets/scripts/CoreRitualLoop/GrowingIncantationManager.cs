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
    [Tooltip("Ordered ritual words used to grow the shared phrase. The first valid word becomes the reset phrase.")]
    [SerializeField] private List<string> ritualVocabulary = new List<string>
    {
        "Umbra",
        "Vakor",
        "Mortis",
        "Noctis"
    };

    [Header("Phrase State")]
    [Tooltip("Number of ritual words currently unlocked in the shared phrase.")]
    [SerializeField, Min(0)] private int currentUnlockedWordCount;

    [Tooltip("Current shared ritual phrase built from the unlocked vocabulary words.")]
    [SerializeField] private string currentRitualPhrase = string.Empty;

    [Tooltip("Current ritual words in phrase order.")]
    [SerializeField] private List<string> currentRitualWords = new List<string>();

    /// <summary>
    /// Resets the shared ritual phrase so it contains exactly one word.
    /// Empty or whitespace-only vocabulary entries are ignored.
    /// </summary>
    public void ResetPhrase()
    {
        currentRitualWords.Clear();
        currentUnlockedWordCount = 0;

        if (GetValidVocabularyCount() == 0)
        {
            currentRitualPhrase = string.Empty;
            return;
        }

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
    /// Unlocks exactly one additional ritual word when another word is available.
    /// Calling this after all vocabulary words are unlocked safely leaves the phrase unchanged.
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
    /// <returns>True when at least one locked vocabulary word remains; otherwise false.</returns>
    public bool CanUnlockNextWord()
    {
        return currentUnlockedWordCount < GetConfiguredVocabulary().Count;
    }

    private void RebuildCurrentPhrase()
    {
        List<string> configuredVocabulary = GetConfiguredVocabulary();

        currentRitualWords.Clear();

        int wordsToUnlock = Mathf.Min(currentUnlockedWordCount, configuredVocabulary.Count);

        for (int wordIndex = 0; wordIndex < wordsToUnlock; wordIndex++)
        {
            currentRitualWords.Add(configuredVocabulary[wordIndex]);
        }

        currentUnlockedWordCount = currentRitualWords.Count;
        currentRitualPhrase = string.Join(" ", currentRitualWords);
    }

    private int GetValidVocabularyCount()
    {
        return GetConfiguredVocabulary().Count;
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
