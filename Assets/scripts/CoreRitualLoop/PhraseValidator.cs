using System;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Compares the expected ritual phrase against recognized speech text.
/// </summary>
public class PhraseValidator : MonoBehaviour
{
    public enum PhraseJudgmentWordState
    {
        Success,
        Failed,
        Missing
    }

    public enum PhraseJudgmentFailureReason
    {
        None,
        Empty,
        WrongWord,
        TooFewWords
    }

    public readonly struct PhraseJudgmentWordResult
    {
        public PhraseJudgmentWordResult(int wordIndex, string expectedWord, string recognizedWord, PhraseJudgmentWordState state)
        {
            WordIndex = wordIndex;
            ExpectedWord = expectedWord;
            RecognizedWord = recognizedWord;
            State = state;
        }

        public int WordIndex { get; }
        public string ExpectedWord { get; }
        public string RecognizedWord { get; }
        public PhraseJudgmentWordState State { get; }
    }

    public readonly struct PhraseJudgmentResult
    {
        public PhraseJudgmentResult(bool success, int matchedWordCount, int firstWrongWordIndex, PhraseJudgmentFailureReason failureReason)
            : this(success, matchedWordCount, firstWrongWordIndex, failureReason, Array.Empty<PhraseJudgmentWordResult>())
        {
        }

        public PhraseJudgmentResult(
            bool success,
            int matchedWordCount,
            int firstWrongWordIndex,
            PhraseJudgmentFailureReason failureReason,
            PhraseJudgmentWordResult[] wordTimeline)
        {
            Success = success;
            MatchedWordCount = matchedWordCount;
            FirstWrongWordIndex = firstWrongWordIndex;
            FailureReason = failureReason;
            WordTimeline = wordTimeline ?? Array.Empty<PhraseJudgmentWordResult>();
        }

        public bool Success { get; }
        public int MatchedWordCount { get; }
        public int FirstWrongWordIndex { get; }
        public PhraseJudgmentFailureReason FailureReason { get; }
        public PhraseJudgmentWordResult[] WordTimeline { get; }
    }

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

    public static PhraseJudgmentResult JudgePhrase(string expectedPhrase, string recognizedPhrase)
    {
        string normalizedExpectedPhrase = NormalizePhrase(expectedPhrase);
        string normalizedRecognizedPhrase = NormalizePhrase(recognizedPhrase);

        string[] expectedWords = SplitWords(normalizedExpectedPhrase);
        string[] recognizedWords = SplitWords(normalizedRecognizedPhrase);
        PhraseJudgmentWordResult[] wordTimeline = BuildWordTimeline(expectedWords, recognizedWords);

        if (recognizedWords.Length == 0)
        {
            return new PhraseJudgmentResult(false, 0, -1, PhraseJudgmentFailureReason.Empty, wordTimeline);
        }

        int wordsToCompare = Math.Min(expectedWords.Length, recognizedWords.Length);

        for (int wordIndex = 0; wordIndex < wordsToCompare; wordIndex++)
        {
            if (!string.Equals(expectedWords[wordIndex], recognizedWords[wordIndex], StringComparison.Ordinal))
            {
                return new PhraseJudgmentResult(false, wordIndex, wordIndex, PhraseJudgmentFailureReason.WrongWord, wordTimeline);
            }
        }

        if (recognizedWords.Length < expectedWords.Length)
        {
            return new PhraseJudgmentResult(false, recognizedWords.Length, -1, PhraseJudgmentFailureReason.TooFewWords, wordTimeline);
        }

        return new PhraseJudgmentResult(true, expectedWords.Length, -1, PhraseJudgmentFailureReason.None, wordTimeline);
    }

    public bool ValidatePhrase(string expectedPhrase, string recognizedPhrase)
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

    private static PhraseJudgmentWordResult[] BuildWordTimeline(string[] expectedWords, string[] recognizedWords)
    {
        if (expectedWords.Length == 0)
        {
            return Array.Empty<PhraseJudgmentWordResult>();
        }

        int timelineLength = GetWordTimelineLength(expectedWords, recognizedWords);
        PhraseJudgmentWordResult[] wordTimeline = new PhraseJudgmentWordResult[timelineLength];

        for (int wordIndex = 0; wordIndex < timelineLength; wordIndex++)
        {
            string recognizedWord = wordIndex < recognizedWords.Length ? recognizedWords[wordIndex] : string.Empty;
            PhraseJudgmentWordState state = GetWordState(expectedWords[wordIndex], recognizedWord);

            wordTimeline[wordIndex] = new PhraseJudgmentWordResult(wordIndex, expectedWords[wordIndex], recognizedWord, state);
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

    private static PhraseJudgmentWordState GetWordState(string expectedWord, string recognizedWord)
    {
        if (string.IsNullOrEmpty(recognizedWord))
        {
            return PhraseJudgmentWordState.Missing;
        }

        if (string.Equals(expectedWord, recognizedWord, StringComparison.Ordinal))
        {
            return PhraseJudgmentWordState.Success;
        }

        return PhraseJudgmentWordState.Failed;
    }
}
