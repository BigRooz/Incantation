using System;

public enum PhraseValidationWordState
{
    Success,
    Failed,
    Missing
}

public enum PhraseValidationFailureReason
{
    None,
    Empty,
    WrongWord,
    TooFewWords
}

public readonly struct PhraseValidationWordResult
{
    public PhraseValidationWordResult(int wordIndex, string expectedWord, string recognizedWord, PhraseValidationWordState state)
    {
        WordIndex = wordIndex;
        ExpectedWord = expectedWord;
        RecognizedWord = recognizedWord;
        State = state;
    }

    public int WordIndex { get; }
    public string ExpectedWord { get; }
    public string RecognizedWord { get; }
    public PhraseValidationWordState State { get; }
}

public readonly struct PhraseValidationResult
{
    public PhraseValidationResult(bool isSuccess, int matchedWordCount, int firstFailedWordIndex, PhraseValidationFailureReason failureReason)
        : this(isSuccess, matchedWordCount, firstFailedWordIndex, failureReason, Array.Empty<PhraseValidationWordResult>())
    {
    }

    public PhraseValidationResult(
        bool isSuccess,
        int matchedWordCount,
        int firstFailedWordIndex,
        PhraseValidationFailureReason failureReason,
        PhraseValidationWordResult[] wordTimeline)
    {
        IsSuccess = isSuccess;
        MatchedWordCount = matchedWordCount;
        FirstFailedWordIndex = firstFailedWordIndex;
        FailureReason = failureReason;
        WordTimeline = wordTimeline ?? Array.Empty<PhraseValidationWordResult>();
    }

    public bool IsSuccess { get; }
    public int MatchedWordCount { get; }
    public int FirstFailedWordIndex { get; }
    public PhraseValidationFailureReason FailureReason { get; }
    public PhraseValidationWordResult[] WordTimeline { get; }
}
