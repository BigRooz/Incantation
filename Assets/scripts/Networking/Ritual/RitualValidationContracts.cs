using System;

namespace Incantation.Networking.Ritual
{
    public enum RitualValidationResult
    {
        None = 0,
        Accepted = 1,
        Rejected = 2
    }

    public enum RitualValidationFailureReason
    {
        None = 0,
        Empty = 1,
        WrongWord = 2,
        TooFewWords = 3,
        PhraseUnavailable = 4,
        InvalidState = 5
    }

    /// <summary>
    /// Immutable serialization-safe result of one server-owned phrase judgment.
    /// </summary>
    [Serializable]
    public readonly struct RitualValidationSnapshot
    {
        public RitualValidationSnapshot(
            RitualSequenceId ritualSequenceId,
            RitualTurnSequenceId turnSequenceId,
            uint submissionSequence,
            uint validationSequence,
            string playerId,
            RitualValidationMode validationMode,
            RitualValidationResult result,
            int acceptedWordCount,
            int validatedWordIndex,
            int firstRejectedWordIndex,
            string[] phraseWords,
            int expectedWordIndex,
            string expectedWord,
            string rejectedWord,
            string receivedText,
            RitualValidationFailureReason failureReason,
            double serverTimestamp)
        {
            RitualSequenceId = ritualSequenceId;
            TurnSequenceId = turnSequenceId;
            SubmissionSequence = submissionSequence;
            ValidationSequence = validationSequence;
            PlayerId = playerId ?? string.Empty;
            ValidationMode = validationMode;
            Result = result;
            AcceptedWordCount = acceptedWordCount;
            ValidatedWordIndex = validatedWordIndex;
            FirstRejectedWordIndex = firstRejectedWordIndex;
            this.phraseWords = phraseWords == null
                ? Array.Empty<string>()
                : (string[])phraseWords.Clone();
            ExpectedWordIndex = expectedWordIndex;
            ExpectedWord = expectedWord ?? string.Empty;
            RejectedWord = rejectedWord ?? string.Empty;
            ReceivedText = receivedText ?? string.Empty;
            FailureReason = failureReason;
            ServerTimestamp = serverTimestamp;
        }

        public RitualSequenceId RitualSequenceId { get; }
        public RitualTurnSequenceId TurnSequenceId { get; }
        public uint SubmissionSequence { get; }
        public uint ValidationSequence { get; }
        public string PlayerId { get; }
        public RitualValidationMode ValidationMode { get; }
        public RitualValidationResult Result { get; }
        public int AcceptedWordCount { get; }
        public int ValidatedWordIndex { get; }
        public int FirstRejectedWordIndex { get; }
        private readonly string[] phraseWords;
        public string[] PhraseWords => phraseWords == null
            ? Array.Empty<string>()
            : (string[])phraseWords.Clone();
        public int ExpectedWordIndex { get; }
        public string ExpectedWord { get; }
        public string RejectedWord { get; }
        public string ReceivedText { get; }
        public RitualValidationFailureReason FailureReason { get; }
        public double ServerTimestamp { get; }
        public bool HasValidation => ValidationSequence > 0;
        public bool IsAccepted => Result == RitualValidationResult.Accepted;
    }
}
