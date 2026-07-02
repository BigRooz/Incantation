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

    public static PhraseValidationResult Validate(string expectedPhrase, string recognizedPhrase)
    {
        string normalizedExpectedPhrase = NormalizePhrase(expectedPhrase);
        string normalizedRecognizedPhrase = NormalizePhrase(recognizedPhrase);

        string[] expectedWords = SplitWords(normalizedExpectedPhrase);
        string[] recognizedWords = SplitWords(normalizedRecognizedPhrase);
        PhraseValidationWordResult[] wordTimeline = BuildWordTimeline(expectedWords, recognizedWords);

        if (recognizedWords.Length == 0)
        {
            return new PhraseValidationResult(false, 0, GetFirstFailedWordIndex(wordTimeline), PhraseValidationFailureReason.Empty, wordTimeline);
        }

        int wordsToCompare = Math.Min(expectedWords.Length, recognizedWords.Length);

        for (int wordIndex = 0; wordIndex < wordsToCompare; wordIndex++)
        {
            if (!string.Equals(expectedWords[wordIndex], recognizedWords[wordIndex], StringComparison.Ordinal))
            {
                return new PhraseValidationResult(false, wordIndex, wordIndex, PhraseValidationFailureReason.WrongWord, wordTimeline);
            }
        }

        if (recognizedWords.Length < expectedWords.Length)
        {
            return new PhraseValidationResult(false, recognizedWords.Length, recognizedWords.Length, PhraseValidationFailureReason.TooFewWords, wordTimeline);
        }

        return new PhraseValidationResult(true, expectedWords.Length, -1, PhraseValidationFailureReason.None, wordTimeline);
    }

    public PhraseValidationResult ValidatePhrase(string expectedPhrase, string recognizedPhrase)
    {
        return Validate(expectedPhrase, recognizedPhrase);
    }

    private static string[] SplitWords(string normalizedPhrase)
    {
        if (string.IsNullOrEmpty(normalizedPhrase))
        {
            return Array.Empty<string>();
        }

        return normalizedPhrase.Split(' ');
    }

    private static PhraseValidationWordResult[] BuildWordTimeline(string[] expectedWords, string[] recognizedWords)
    {
        if (expectedWords.Length == 0)
        {
            return Array.Empty<PhraseValidationWordResult>();
        }

        int timelineLength = GetWordTimelineLength(expectedWords, recognizedWords);
        PhraseValidationWordResult[] wordTimeline = new PhraseValidationWordResult[timelineLength];

        for (int wordIndex = 0; wordIndex < timelineLength; wordIndex++)
        {
            string recognizedWord = wordIndex < recognizedWords.Length ? recognizedWords[wordIndex] : string.Empty;
            PhraseValidationWordState state = GetWordState(expectedWords[wordIndex], recognizedWord);

            wordTimeline[wordIndex] = new PhraseValidationWordResult(wordIndex, expectedWords[wordIndex], recognizedWord, state);
        }

        return wordTimeline;
    }

    private static int GetWordTimelineLength(string[] expectedWords, string[] recognizedWords)
    {
        int wordsToCompare = Math.Min(expectedWords.Length, recognizedWords.Length);

        for (int wordIndex = 0; wordIndex < wordsToCompare; wordIndex++)
        {
            if (!string.Equals(expectedWords[wordIndex], recognizedWords[wordIndex], StringComparison.Ordinal))
            {
                return wordIndex + 1;
            }
        }

        if (recognizedWords.Length < expectedWords.Length)
        {
            return recognizedWords.Length + 1;
        }

        return expectedWords.Length;
    }

    private static int GetFirstFailedWordIndex(PhraseValidationWordResult[] wordTimeline)
    {
        for (int wordIndex = 0; wordIndex < wordTimeline.Length; wordIndex++)
        {
            if (wordTimeline[wordIndex].State != PhraseValidationWordState.Success)
            {
                return wordTimeline[wordIndex].WordIndex;
            }
        }

        return -1;
    }

    private static PhraseValidationWordState GetWordState(string expectedWord, string recognizedWord)
    {
        if (string.IsNullOrEmpty(recognizedWord))
        {
            return PhraseValidationWordState.Missing;
        }

        if (string.Equals(expectedWord, recognizedWord, StringComparison.Ordinal))
        {
            return PhraseValidationWordState.Success;
        }

        return PhraseValidationWordState.Failed;
    }
}
