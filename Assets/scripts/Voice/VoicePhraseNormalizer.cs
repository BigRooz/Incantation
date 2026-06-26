using System.Collections.Generic;
using UnityEngine;

public class VoicePhraseNormalizer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private IncantationWordLibrary wordLibrary;

    private readonly Dictionary<string, string> aliasLookup = new Dictionary<string, string>();

    public string NormalizePhrase(string phrase)
    {
        string normalizedPhrase = Normalize(phrase);

        RebuildAliasLookup();

        if (aliasLookup.TryGetValue(normalizedPhrase, out string incantationWord))
            return incantationWord;

        return normalizedPhrase;
    }

    private void RebuildAliasLookup()
    {
        aliasLookup.Clear();

        foreach (IncantationWord word in GetLibraryWords())
        {
            if (word == null)
                continue;

            string normalizedWord = Normalize(word.Word);

            if (string.IsNullOrEmpty(normalizedWord))
                continue;

            foreach (string speechAlias in word.SpeechAliases)
            {
                string normalizedAlias = Normalize(speechAlias);

                if (string.IsNullOrEmpty(normalizedAlias))
                    continue;

                aliasLookup[normalizedAlias] = normalizedWord;
            }
        }
    }

    private IReadOnlyList<IncantationWord> GetLibraryWords()
    {
        if (wordLibrary == null)
            wordLibrary = GetComponent<IncantationWordLibrary>();

        if (wordLibrary == null)
            wordLibrary = FindFirstObjectByType<IncantationWordLibrary>();

        if (wordLibrary == null)
        {
            Debug.LogWarning("VoicePhraseNormalizer requires an IncantationWordLibrary reference.");
            return new List<IncantationWord>();
        }

        return wordLibrary.Words;
    }

    private string Normalize(string phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase))
            return string.Empty;

        return phrase.Trim().ToLowerInvariant();
    }
}
