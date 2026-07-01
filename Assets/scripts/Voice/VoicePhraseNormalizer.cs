using System;
using System.Collections.Generic;
using System.Text;
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

        return ApplyWordAliases(normalizedPhrase);
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
            Debug.LogWarning($"{nameof(VoicePhraseNormalizer)} on '{gameObject.name}' is missing required reference '{nameof(wordLibrary)}'. Assign an IncantationWordLibrary in the Inspector.", this);
            return null;
        }

        return wordLibrary;
    }

    private string CleanPhrase(string phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase))
            return string.Empty;

        StringBuilder builder = new StringBuilder();
        bool previousWasWhitespace = true;

        foreach (char character in phrase)
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

    private string ApplyWordAliases(string normalizedPhrase)
    {
        if (string.IsNullOrEmpty(normalizedPhrase))
            return string.Empty;

        string[] tokens = normalizedPhrase.Split(' ');
        List<string> normalizedTokens = new List<string>();

        for (int tokenIndex = 0; tokenIndex < tokens.Length;)
        {
            string replacement = null;
            int replacementTokenCount = 0;

            for (int tokenCount = tokens.Length - tokenIndex; tokenCount > 0; tokenCount--)
            {
                string candidate = BuildTokenPhrase(tokens, tokenIndex, tokenCount);

                if (!aliasLookup.TryGetValue(candidate, out replacement))
                    continue;

                replacementTokenCount = tokenCount;
                break;
            }

            if (replacementTokenCount > 0)
            {
                normalizedTokens.Add(replacement);
                tokenIndex += replacementTokenCount;
            }
            else
            {
                normalizedTokens.Add(tokens[tokenIndex]);
                tokenIndex++;
            }
        }

        return string.Join(" ", normalizedTokens);
    }

    private string BuildTokenPhrase(string[] tokens, int startIndex, int tokenCount)
    {
        StringBuilder builder = new StringBuilder();

        for (int i = 0; i < tokenCount; i++)
        {
            if (i > 0)
                builder.Append(' ');

            builder.Append(tokens[startIndex + i]);
        }

        return builder.ToString();
    }
}
