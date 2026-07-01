using System;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Compares the expected ritual phrase against recognized speech text.
/// </summary>
public class PhraseValidator : MonoBehaviour
{
    private static readonly Regex PunctuationRegex = new Regex(@"[.,?!]", RegexOptions.Compiled);
    private static readonly Regex WhitespaceRegex = new Regex(@"\s+", RegexOptions.Compiled);

    public static string NormalizePhrase(string phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase))
        {
            return string.Empty;
        }

        string withoutPunctuation = PunctuationRegex.Replace(phrase, string.Empty);
        string collapsedWhitespace = WhitespaceRegex.Replace(withoutPunctuation, " ");

        return collapsedWhitespace.Trim().ToLowerInvariant();
    }

    public static bool Validate(string expectedPhrase, string recognizedPhrase)
    {
        string normalizedExpectedPhrase = NormalizePhrase(expectedPhrase);
        string normalizedRecognizedPhrase = NormalizePhrase(recognizedPhrase);

        return string.Equals(normalizedExpectedPhrase, normalizedRecognizedPhrase, StringComparison.Ordinal);
    }

    public bool ValidatePhrase(string expectedPhrase, string recognizedPhrase)
    {
        return Validate(expectedPhrase, recognizedPhrase);
    }
}
