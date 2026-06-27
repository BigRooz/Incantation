using System;
using System.Collections.Generic;
using UnityEngine;

public class VoicePhraseNormalizer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private IncantationWordLibrary wordLibrary;

    private readonly Dictionary<string, string> aliasLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public string Normalize(string phrase)
    {
        string normalizedPhrase = CleanPhrase(phrase);

        RebuildAliasLookup();

        if (aliasLookup.TryGetValue(normalizedPhrase, out string incantationWord))
            return incantationWord;

        return normalizedPhrase;
    }

    public string NormalizePhrase(string phrase)
    {
        return Normalize(phrase);
    }

    private void RebuildAliasLookup()
    {
        aliasLookup.Clear();
        IncantationWordLibrary resolvedWordLibrary = ResolveWordLibrary();

        if (resolvedWordLibrary == null)
            return;

        foreach (IncantationWord word in resolvedWordLibrary.GetWords())
        {
            if (word == null)
                continue;

            string normalizedWord = CleanPhrase(word.Word);

            if (string.IsNullOrEmpty(normalizedWord))
                continue;

            aliasLookup[normalizedWord] = normalizedWord;
        }

        foreach (IncantationWordLibrary.SpeechAliasMapping speechAliasMapping in resolvedWordLibrary.GetSpeechAliasMappings())
        {
            if (speechAliasMapping == null)
                continue;

            string normalizedAlias = CleanPhrase(speechAliasMapping.Alias);
            string normalizedWord = CleanPhrase(speechAliasMapping.Word);

            if (string.IsNullOrEmpty(normalizedAlias) || string.IsNullOrEmpty(normalizedWord))
                continue;

            aliasLookup[normalizedAlias] = normalizedWord;
            Debug.Log($"Library alias loaded: {normalizedAlias} -> {normalizedWord}");
        }
    }

    private IncantationWordLibrary ResolveWordLibrary()
    {
        if (wordLibrary == null)
            wordLibrary = GetComponent<IncantationWordLibrary>();

        if (wordLibrary == null)
            wordLibrary = FindFirstObjectByType<IncantationWordLibrary>();

        if (wordLibrary == null)
        {
            Debug.LogWarning("VoicePhraseNormalizer requires an IncantationWordLibrary reference.");
            return null;
        }

        return wordLibrary;
    }

    private string CleanPhrase(string phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase))
            return string.Empty;

        return phrase.Trim().ToLowerInvariant();
    }
}
