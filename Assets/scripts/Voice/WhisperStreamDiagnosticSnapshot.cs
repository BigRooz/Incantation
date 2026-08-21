/// <summary>
/// Immutable, read-only complete-recitation state for the Development overlay.
/// The historical filename is retained to preserve the existing Unity asset identity.
/// </summary>
public readonly struct WhisperStreamDiagnosticSnapshot
{
    public WhisperStreamDiagnosticSnapshot(
        int version,
        bool ready,
        bool listening,
        bool voiceActive,
        float listeningGlow,
        string previousState,
        string transitionReason,
        string sessionIdentity,
        bool turnEligible,
        bool microphoneRecording,
        bool retryRequested,
        string retryBlock,
        string state,
        string rawTranscript,
        string bankedAttempt,
        string authority,
        string pipeline,
        string pipelineBlock,
        int failedIndex,
        long inferenceMilliseconds)
    {
        Version = version;
        Ready = ready;
        Listening = listening;
        VoiceActive = voiceActive;
        ListeningGlow = listeningGlow;
        PreviousState = previousState ?? string.Empty;
        TransitionReason = transitionReason ?? string.Empty;
        SessionIdentity = sessionIdentity ?? string.Empty;
        TurnEligible = turnEligible;
        MicrophoneRecording = microphoneRecording;
        RetryRequested = retryRequested;
        RetryBlock = retryBlock ?? string.Empty;
        State = state ?? "IDLE";
        RawTranscript = rawTranscript ?? string.Empty;
        BankedAttempt = bankedAttempt ?? string.Empty;
        Authority = string.IsNullOrEmpty(authority) ? "NOT SUBMITTED" : authority;
        Pipeline = pipeline ?? string.Empty;
        PipelineBlock = pipelineBlock ?? string.Empty;
        FailedIndex = failedIndex;
        InferenceMilliseconds = inferenceMilliseconds;
    }

    public int Version { get; }
    public bool Ready { get; }
    public bool Listening { get; }
    public bool VoiceActive { get; }
    public float ListeningGlow { get; }
    public string PreviousState { get; }
    public string TransitionReason { get; }
    public string SessionIdentity { get; }
    public bool TurnEligible { get; }
    public bool MicrophoneRecording { get; }
    public bool RetryRequested { get; }
    public string RetryBlock { get; }
    public string State { get; }
    public string RawTranscript { get; }
    public string BankedAttempt { get; }
    public string Authority { get; }
    public string Pipeline { get; }
    public string PipelineBlock { get; }
    public int FailedIndex { get; }
    public long InferenceMilliseconds { get; }
}
