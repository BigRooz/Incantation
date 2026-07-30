using System;

namespace Incantation.Networking.Ritual
{
    /// <summary>
    /// Serialization-safe recognized-speech payload. Player identity is intentionally absent;
    /// the server derives it from the sending connection.
    /// </summary>
    [Serializable]
    public struct RitualVoiceSubmission
    {
        public const int MaximumRecognizedTextLength = 256;

        public RitualVoiceSubmission(
            uint ritualSequence,
            uint turnSequence,
            uint submissionSequence,
            string recognizedText,
            double clientCaptureTimestamp)
        {
            RitualSequence = ritualSequence;
            TurnSequence = turnSequence;
            SubmissionSequence = submissionSequence;
            RecognizedText = recognizedText ?? string.Empty;
            ClientCaptureTimestamp = clientCaptureTimestamp;
        }

        public uint RitualSequence;
        public uint TurnSequence;
        public uint SubmissionSequence;
        public string RecognizedText;
        public double ClientCaptureTimestamp;
    }

    /// <summary>
    /// Immutable read-only state for the latest server-accepted voice submission.
    /// </summary>
    public readonly struct RitualVoiceSubmissionSnapshot
    {
        public RitualVoiceSubmissionSnapshot(
            RitualSequenceId ritualSequenceId,
            RitualTurnSequenceId turnSequenceId,
            uint submissionSequence,
            string playerId,
            string recognizedText,
            double serverAcceptanceTimestamp)
        {
            RitualSequenceId = ritualSequenceId;
            TurnSequenceId = turnSequenceId;
            SubmissionSequence = submissionSequence;
            PlayerId = playerId ?? string.Empty;
            RecognizedText = recognizedText ?? string.Empty;
            ServerAcceptanceTimestamp = serverAcceptanceTimestamp;
        }

        public RitualSequenceId RitualSequenceId { get; }
        public RitualTurnSequenceId TurnSequenceId { get; }
        public uint SubmissionSequence { get; }
        public string PlayerId { get; }
        public string RecognizedText { get; }
        public double ServerAcceptanceTimestamp { get; }
        public bool HasSubmission => SubmissionSequence > 0;
    }
}
