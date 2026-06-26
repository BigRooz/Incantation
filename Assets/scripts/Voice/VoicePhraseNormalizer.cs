using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class VoicePhraseAlias
{
    [Tooltip("The phrase returned by speech recognition.")]
    public string recognizedPhrase;

    [Tooltip("The incantation word this recognized phrase should count as.")]
    public string incantationWord;
}

public class VoicePhraseNormalizer : MonoBehaviour
{
    [Header("Speech Recognition Aliases")]
    [Tooltip("Phrases returned by speech recognition and the incantation words they should become.")]
    [SerializeField] private List<VoicePhraseAlias> aliases = new List<VoicePhraseAlias>
    {
        new VoicePhraseAlias { recognizedPhrase = "deliveries", incantationWord = "tenebris" },
        new VoicePhraseAlias { recognizedPhrase = "malediction", incantationWord = "maledictum" },
        new VoicePhraseAlias { recognizedPhrase = "or do", incantationWord = "ordo" },
        new VoicePhraseAlias { recognizedPhrase = "vehicle um", incantationWord = "vinculum" },
        new VoicePhraseAlias { recognizedPhrase = "umbrella", incantationWord = "umbra" }
    };

    public string NormalizePhrase(string phrase)
    {
        string normalizedPhrase = Normalize(phrase);

        foreach (VoicePhraseAlias alias in aliases)
        {
            if (alias == null || Normalize(alias.recognizedPhrase) != normalizedPhrase)
                continue;

            return Normalize(alias.incantationWord);
        }

        return normalizedPhrase;
    }

    private string Normalize(string phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase))
            return string.Empty;

        return phrase.Trim().ToLowerInvariant();
    }
}
