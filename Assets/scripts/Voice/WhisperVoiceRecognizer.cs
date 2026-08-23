using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Incantation.Networking;
using UnityEngine;
using Whisper;
using Whisper.Utils;
using Debug = UnityEngine.Debug;

public enum WhisperCapturePurpose
{
    None,
    Ritual,
    Spell
}

/// <summary>
/// Sandbox-style production recognizer: preload once, record one complete
/// recitation, transcribe one complete AudioChunk, and publish one transcript.
/// </summary>
public sealed class WhisperVoiceRecognizer : MonoBehaviour,
    IVoiceRecognizer,
    IVoiceInput,
    IVoiceRecognizerProcessingStatus
{
    private const float MinimumRecordingLengthSeconds = 0.35f;

    [Header("Sandbox References")]
    [SerializeField] private WhisperManager whisper;
    [SerializeField] private MicrophoneRecord microphoneRecord;
    [Header("End Of Recitation")]
    [SerializeField, Min(0.3f)] private float trailingSilenceSeconds = 0.8f;
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool logRecognizedPhrases = true;

    private VoiceAmplitudeProvider amplitudeProvider;
    private VoiceAmplitudeProvider suspendedAmplitudeProvider;
    private bool restoreAmplitudeRecording;
    private bool previousMicrophoneUseVad;
    private bool hasTemporaryVadOverride;
    private bool preloadInProgress;
    private bool preloadFailed;
    private bool startRequestedWhenReady;
    private bool isListening;
    private bool isTranscribing;
    private bool speechObserved;
    private bool voiceActive;
    private bool discardStoppedRecording;
    private bool microphoneHandoffPending;
    private bool microphoneOwnershipBlocked;
    private WhisperCapturePurpose pendingCapturePurpose;
    private WhisperCapturePurpose sessionCapturePurpose;
    private float recordingStartedAt;
    private float lastVoiceActivityAt;
    private float listeningGlow;

    private int sessionId;
    private int invalidatedThroughSessionId;
    private uint pendingRitualSequence;
    private uint pendingTurnSequence;
    private string pendingPlayerId = string.Empty;
    private uint sessionRitualSequence;
    private uint sessionTurnSequence;
    private string sessionPlayerId = string.Empty;
    private int deliveredSessionId;
    private uint deliveredRitualSequence;
    private uint deliveredTurnSequence;
    private string deliveredPlayerId = string.Empty;

    private bool hasPendingTranscript;
    private int pendingTranscriptSessionId;
    private string pendingTranscript = string.Empty;
    private bool pendingTranscriptHasText;

    private int diagnosticVersion;
    private string diagnosticState = "PRELOADING";
    private string diagnosticRaw = string.Empty;
    private string diagnosticBanked = string.Empty;
    private string diagnosticAuthority = "NOT SUBMITTED";
    private string diagnosticPipeline = string.Empty;
    private string diagnosticPipelineBlock = string.Empty;
    private int diagnosticFailedIndex = -1;
    private long diagnosticInferenceMilliseconds;
    private bool preserveTokenlessDiagnostic;
    private string diagnosticPreviousState = string.Empty;
    private string diagnosticTransitionReason = "INITIAL_STATE";
    private bool diagnosticTurnEligible;
    private bool diagnosticRetryRequested;
    private string diagnosticRetryBlock = string.Empty;

    public bool IsListening => isListening;
    public bool IsProcessingRecognition => isTranscribing || hasPendingTranscript ||
        (startRequestedWhenReady && preloadInProgress) || microphoneOwnershipBlocked;
    public bool IsWhisperReady => whisper != null && whisper.IsLoaded;
    public float ListeningGlow => listeningGlow;

    public event Action<string> OnPhraseRecognized;
    public event Action<string> OnSpellPhraseRecognized;
    public event Action<bool> OnVoiceActivityChanged;
    public event Action<float> OnListeningGlowChanged;
    public event Action<string> OnRecognitionStateChanged;

    public void SetCapturePurpose(WhisperCapturePurpose purpose) =>
        pendingCapturePurpose = purpose;

    public WhisperStreamDiagnosticSnapshot DiagnosticSnapshot =>
        new WhisperStreamDiagnosticSnapshot(
            diagnosticVersion,
            IsWhisperReady,
            isListening && microphoneRecord != null && microphoneRecord.IsRecording,
            voiceActive,
            listeningGlow,
            diagnosticPreviousState,
            diagnosticTransitionReason,
            $"S{sessionId} R{sessionRitualSequence} T{sessionTurnSequence} P:{sessionPlayerId}",
            diagnosticTurnEligible,
            microphoneRecord != null && microphoneRecord.IsRecording,
            diagnosticRetryRequested,
            diagnosticRetryBlock,
            diagnosticState,
            diagnosticRaw,
            diagnosticBanked,
            diagnosticAuthority,
            diagnosticPipeline,
            diagnosticPipelineBlock,
            diagnosticFailedIndex,
            diagnosticInferenceMilliseconds);

    private void Awake() => ResolveReferences();

    private void OnEnable()
    {
        ResolveReferences();
        if (microphoneRecord != null)
            microphoneRecord.OnRecordStop += HandleRecordStop;
        BeginPreload();
    }

    private void OnDisable()
    {
        CancelListening("Recognizer disabled.");
        if (microphoneRecord != null)
            microphoneRecord.OnRecordStop -= HandleRecordStop;
    }

    private void OnValidate()
    {
        trailingSilenceSeconds = Mathf.Max(0.3f, trailingSilenceSeconds);
    }

    private void Update()
    {
        if (hasPendingTranscript)
        {
            ProcessPendingTranscript();
            return;
        }

        if (!isListening || microphoneRecord == null || !microphoneRecord.IsRecording)
            return;

        bool detected = microphoneRecord.useVad && microphoneRecord.IsVoiceDetected;
        SetVoiceActivity(detected);
        if (detected)
        {
            speechObserved = true;
            lastVoiceActivityAt = Time.realtimeSinceStartup;
            SetListeningGlow(1f);
            return;
        }

        if (!speechObserved)
        {
            SetListeningGlow(0f);
            return;
        }

        float silenceElapsed = Time.realtimeSinceStartup - lastVoiceActivityAt;
        float glow = 1f - silenceElapsed / Mathf.Max(0.3f, trailingSilenceSeconds);
        SetListeningGlow(glow);
        if (glow > 0f)
            return;

        StopCompleteRecording();
    }

    public void StartListening()
    {
        if (!ValidateReferences())
        {
            SetRetryBlockDiagnostic("REFERENCES_MISSING");
            return;
        }
        if (isListening)
        {
            SetRetryBlockDiagnostic("ALREADY_LISTENING");
            return;
        }
        if (isTranscribing)
        {
            SetRetryBlockDiagnostic("TRANSCRIPTION_ACTIVE");
            return;
        }
        if (hasPendingTranscript)
        {
            SetRetryBlockDiagnostic("TRANSCRIPT_PENDING");
            return;
        }

        if (!IsWhisperReady)
        {
            startRequestedWhenReady = true;
            SetRetryBlockDiagnostic(preloadFailed
                ? "PRELOAD_FAILED"
                : "WHISPER_NOT_READY");
            SetState(
                preloadFailed ? "READY_FAILED" : "PRELOADING",
                "START_DEFERRED_WHISPER_NOT_READY");
            BeginPreload();
            Log("Recording deferred because Whisper is not ready.");
            return;
        }

        startRequestedWhenReady = false;
        sessionId++;
        sessionRitualSequence = pendingRitualSequence;
        sessionTurnSequence = pendingTurnSequence;
        sessionPlayerId = pendingPlayerId;
        sessionCapturePurpose = pendingCapturePurpose;
        deliveredSessionId = 0;
        deliveredRitualSequence = 0;
        deliveredTurnSequence = 0;
        deliveredPlayerId = string.Empty;
        speechObserved = false;
        lastVoiceActivityAt = 0f;
        recordingStartedAt = Time.realtimeSinceStartup;
        discardStoppedRecording = false;
        SetVoiceActivity(false);
        SetListeningGlow(0f);
        ResetAttemptDiagnostics();
        microphoneHandoffPending = false;
        microphoneOwnershipBlocked = false;
        if (!AcquireMicrophoneOwnership())
        {
            microphoneOwnershipBlocked = true;
            diagnosticRetryBlock = "AMPLITUDE_PROVIDER_STILL_RECORDING";
            diagnosticVersion++;
            SetState("READY_FAILED", "MICROPHONE_OWNERSHIP_CONFLICT");
            return;
        }
        EnableVadObservation();
        microphoneRecord.StartRecord();
        isListening = microphoneRecord.IsRecording;
        diagnosticRetryBlock = isListening ? string.Empty : "MIC_START_FAILED";
        if (isListening)
            diagnosticRetryRequested = false;
        diagnosticVersion++;
        SetState(
            isListening ? "LISTENING" : "READY_FAILED",
            isListening ? "START_RECORD_SUCCEEDED" : "START_RECORD_FAILED");
        Log($"Complete recording started. Session={sessionId}, Ritual={sessionRitualSequence}, Turn={sessionTurnSequence}, Player={sessionPlayerId}.");
    }

    public void StopListening() => StopCompleteRecording();
    public void CancelListening() => CancelListening("Recognition session invalidated.");

    public void CancelListening(string reason)
    {
        CancelListening(reason, false);
    }

    public void CancelListeningForHandoff(string reason)
    {
        CancelListening(reason, true);
    }

    private void CancelListening(string reason, bool preserveMicrophoneOwnership)
    {
        invalidatedThroughSessionId = Mathf.Max(invalidatedThroughSessionId, sessionId);
        startRequestedWhenReady = false;
        discardStoppedRecording = true;
        microphoneOwnershipBlocked = false;
        microphoneHandoffPending = preserveMicrophoneOwnership;
        isListening = false;
        SetVoiceActivity(false);
        SetListeningGlow(0f);
        if (microphoneRecord != null && microphoneRecord.IsRecording)
        {
            microphoneRecord.StopRecord();
        }
        else if (!microphoneHandoffPending)
            ReleaseMicrophoneOwnership();
        SetState(
            IsWhisperReady ? "READY" : "PRELOADING",
            $"CANCEL_LISTENING: {reason}");
        Log($"Session invalidated. Session={sessionId}, Reason={reason}");
    }

    public void ConfigureAuthoritativeSession(uint ritualSequence, uint turnSequence, string playerId)
    {
        pendingRitualSequence = ritualSequence;
        pendingTurnSequence = turnSequence;
        pendingPlayerId = playerId ?? string.Empty;
    }

    public void SetAmplitudeProvider(VoiceAmplitudeProvider provider) => amplitudeProvider = provider;

    public bool TryGetDeliveredSessionContext(
        out int deliveredId, out uint ritualSequence, out uint turnSequence, out string playerId)
    {
        deliveredId = deliveredSessionId;
        ritualSequence = deliveredRitualSequence;
        turnSequence = deliveredTurnSequence;
        playerId = deliveredPlayerId;
        return deliveredId > 0;
    }

    public void SetBankedAttemptDiagnostic(string bankedAttempt)
    {
        diagnosticBanked = bankedAttempt ?? string.Empty;
        preserveTokenlessDiagnostic = false;
        diagnosticVersion++;
    }

    public void SetPipelineDiagnostic(string boundary)
    {
        diagnosticPipeline = boundary ?? string.Empty;
        diagnosticPipelineBlock = string.Empty;
        diagnosticVersion++;
    }

    public void SetPipelineBlockDiagnostic(string guardName)
    {
        diagnosticPipelineBlock = guardName ?? string.Empty;
        diagnosticVersion++;
    }

    public void SetNormalizationFailureDiagnostic(string reason)
    {
        diagnosticBanked = string.IsNullOrWhiteSpace(reason)
            ? "<FAILED>" : $"<FAILED: {reason}>";
        diagnosticAuthority = "NOT SUBMITTED";
        preserveTokenlessDiagnostic = true;
        diagnosticVersion++;
    }

    public void SetAttemptSubmittedDiagnostic()
    {
        diagnosticAuthority = "WAITING";
        SetState("AWAITING_AUTHORITY", "ATTEMPT_SUBMITTED");
    }

    public void SetSubmissionFailureDiagnostic()
    {
        diagnosticAuthority = "NOT SUBMITTED";
        diagnosticVersion++;
    }

    public void RecordAuthorityDiagnostic(bool accepted, int failedIndex)
    {
        diagnosticAuthority = accepted ? "ACCEPTED" : "REJECTED";
        diagnosticFailedIndex = accepted ? -1 : failedIndex;
        SetState("AWAITING_AUTHORITY", "AUTHORITY_VERDICT_RECEIVED");
    }

    public void ResetAuthorityDiagnostic()
    {
        diagnosticAuthority = "NOT SUBMITTED";
        diagnosticFailedIndex = -1;
        diagnosticRaw = string.Empty;
        diagnosticBanked = string.Empty;
        diagnosticPipeline = string.Empty;
        diagnosticPipelineBlock = string.Empty;
        preserveTokenlessDiagnostic = false;
        diagnosticVersion++;
    }

    private async void BeginPreload()
    {
        if (IsWhisperReady)
        {
            CompletePreload(true);
            return;
        }
        if (preloadInProgress || whisper == null)
            return;

        preloadInProgress = true;
        preloadFailed = false;
        SetState("PRELOADING", "PRELOAD_STARTED");
        try
        {
            if (!whisper.IsLoading)
                await whisper.InitModel();
            while (whisper.IsLoading)
                await Task.Yield();
            CompletePreload(whisper.IsLoaded);
        }
        catch (Exception exception)
        {
            Debug.LogError($"Whisper preload failed: {exception.Message}", this);
            CompletePreload(false);
        }
        finally
        {
            preloadInProgress = false;
        }
    }

    private void CompletePreload(bool succeeded)
    {
        preloadFailed = !succeeded;
        SetState(
            succeeded ? "READY" : "READY_FAILED",
            succeeded ? "PRELOAD_SUCCEEDED" : "PRELOAD_FAILED");
        if (!succeeded || !startRequestedWhenReady)
            return;
        startRequestedWhenReady = false;
        StartListening();
    }

    private void StopCompleteRecording()
    {
        if (!isListening || microphoneRecord == null ||
            !microphoneRecord.IsRecording || !speechObserved)
            return;
        if (Time.realtimeSinceStartup - recordingStartedAt < MinimumRecordingLengthSeconds)
            return;

        isListening = false;
        discardStoppedRecording = false;
        SetVoiceActivity(false);
        SetListeningGlow(0f);
        SetState("TRANSCRIBING", "TERMINAL_SILENCE_COMPLETE");
        microphoneRecord.StopRecord();
    }

    private void HandleRecordStop(AudioChunk completeRecording)
    {
        bool deferSpellMicrophoneRelease =
            sessionCapturePurpose == WhisperCapturePurpose.Spell &&
            !discardStoppedRecording;
        if (!microphoneHandoffPending && !deferSpellMicrophoneRelease)
            ReleaseMicrophoneOwnership();
        if (discardStoppedRecording)
        {
            discardStoppedRecording = false;
            return;
        }
        int stoppedSessionId = sessionId;
        if (stoppedSessionId <= invalidatedThroughSessionId)
            return;
        TranscribeCompleteRecording(completeRecording, stoppedSessionId);
    }

    private async void TranscribeCompleteRecording(AudioChunk completeRecording, int transcribedSessionId)
    {
        if (completeRecording.Data == null || completeRecording.Data.Length == 0)
        {
            QueueTranscript(transcribedSessionId, string.Empty, false, 0);
            return;
        }

        isTranscribing = true;
        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            WhisperResult result = await whisper.GetTextAsync(
                completeRecording.Data, completeRecording.Frequency, completeRecording.Channels);
            stopwatch.Stop();
            if (IsSessionStale(transcribedSessionId))
                return;
            string transcript = result != null ? result.Result.Trim() : string.Empty;
            QueueTranscript(transcribedSessionId, transcript,
                !string.IsNullOrWhiteSpace(transcript), stopwatch.ElapsedMilliseconds);
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            Debug.LogWarning($"Complete Whisper transcription failed: {exception.Message}", this);
            if (!IsSessionStale(transcribedSessionId))
                QueueTranscript(transcribedSessionId, string.Empty, false, stopwatch.ElapsedMilliseconds);
        }
        finally
        {
            isTranscribing = false;
        }
    }

    private void QueueTranscript(int transcribedSessionId, string transcript,
        bool hasText, long inferenceMilliseconds)
    {
        pendingTranscriptSessionId = transcribedSessionId;
        pendingTranscript = transcript ?? string.Empty;
        pendingTranscriptHasText = hasText;
        diagnosticInferenceMilliseconds = inferenceMilliseconds;
        diagnosticPipeline = "RESULT_READY";
        diagnosticVersion++;
        hasPendingTranscript = true;
    }

    private void ProcessPendingTranscript()
    {
        int completedSessionId = pendingTranscriptSessionId;
        string transcript = pendingTranscript;
        bool hasText = pendingTranscriptHasText;
        hasPendingTranscript = false;
        pendingTranscriptSessionId = 0;
        pendingTranscript = string.Empty;
        pendingTranscriptHasText = false;
        if (IsSessionStale(completedSessionId))
            return;

        if (sessionCapturePurpose == WhisperCapturePurpose.Spell &&
            !microphoneHandoffPending)
        {
            ReleaseMicrophoneOwnership();
        }


        diagnosticRaw = transcript;
        diagnosticPipeline = "MAIN_THREAD_DELIVERED";
        diagnosticVersion++;
        if (!hasText)
        {
            SetNormalizationFailureDiagnostic("NO_TEXTUAL_WORDS");
            SetRetryRequestedDiagnostic("TOKENLESS_RESULT");
            StartListening();
            return;
        }

        deliveredSessionId = completedSessionId;
        deliveredRitualSequence = sessionRitualSequence;
        deliveredTurnSequence = sessionTurnSequence;
        deliveredPlayerId = sessionPlayerId;
        if (logRecognizedPhrases)
            Debug.Log($"Whisper complete recitation: {transcript}", this);
        if (sessionCapturePurpose == WhisperCapturePurpose.Spell)
            OnSpellPhraseRecognized?.Invoke(transcript);
        else if (sessionCapturePurpose == WhisperCapturePurpose.Ritual)
            OnPhraseRecognized?.Invoke(transcript);
    }

    private bool IsSessionStale(int checkedSessionId) =>
        checkedSessionId <= invalidatedThroughSessionId || checkedSessionId != sessionId;

    private void EnableVadObservation()
    {
        previousMicrophoneUseVad = microphoneRecord.useVad;
        hasTemporaryVadOverride = !microphoneRecord.useVad;
        microphoneRecord.useVad = true;
    }

    private bool AcquireMicrophoneOwnership()
    {
        ResolveLocalAmplitudeProvider();
        if (amplitudeProvider != null && amplitudeProvider.IsRecording)
        {
            suspendedAmplitudeProvider = amplitudeProvider;
            restoreAmplitudeRecording = true;
            amplitudeProvider.StopRecording();
        }

        VoiceAmplitudeProvider[] providers = FindObjectsByType<VoiceAmplitudeProvider>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        foreach (VoiceAmplitudeProvider provider in providers)
        {
            if (!provider.IsRecording)
                continue;

            Debug.LogError(
                $"Microphone acquisition blocked because {nameof(VoiceAmplitudeProvider)} " +
                $"'{provider.gameObject.name}' (Instance {provider.GetInstanceID()}) is still recording.",
                provider);
            return false;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("ACTIVE_RECORDING_AMPLITUDE_PROVIDERS = 0", this);
#endif
        return true;
    }

    private void ResolveLocalAmplitudeProvider()
    {
        NetworkPlayer localPlayer = NetworkPlayer.LocalPlayer;
        NetworkCharacterPresentation presentation = localPlayer != null
            ? localPlayer.GetComponent<NetworkCharacterPresentation>()
            : null;
        GameObject character = presentation != null ? presentation.CharacterInstance : null;
        VoiceAmplitudeProvider resolvedProvider = character != null
            ? character.GetComponentInChildren<VoiceAmplitudeProvider>(true)
            : null;
        if (resolvedProvider != null)
            amplitudeProvider = resolvedProvider;
    }

    private void ReleaseMicrophoneOwnership()
    {
        if (hasTemporaryVadOverride && microphoneRecord != null)
            microphoneRecord.useVad = previousMicrophoneUseVad;
        hasTemporaryVadOverride = false;
        VoiceAmplitudeProvider provider = suspendedAmplitudeProvider;
        bool shouldRestore = restoreAmplitudeRecording;
        suspendedAmplitudeProvider = null;
        restoreAmplitudeRecording = false;
        if (shouldRestore && provider != null && provider.isActiveAndEnabled)
            provider.StartRecording();
    }

    private void SetVoiceActivity(bool active)
    {
        if (active && preserveTokenlessDiagnostic)
        {
            diagnosticRaw = string.Empty;
            diagnosticBanked = string.Empty;
            preserveTokenlessDiagnostic = false;
            diagnosticVersion++;
        }
        if (voiceActive == active)
            return;
        voiceActive = active;
        diagnosticVersion++;
        OnVoiceActivityChanged?.Invoke(active);
    }

    private void SetListeningGlow(float value)
    {
        float clampedValue = Mathf.Clamp01(value);
        if (Mathf.Approximately(listeningGlow, clampedValue))
            return;

        listeningGlow = clampedValue;
        diagnosticVersion++;
        OnListeningGlowChanged?.Invoke(listeningGlow);
    }

    public void SetTurnEligibilityDiagnostic(bool eligible)
    {
        if (diagnosticTurnEligible == eligible)
            return;

        diagnosticTurnEligible = eligible;
        diagnosticVersion++;
    }

    public void SetRetryRequestedDiagnostic(string reason)
    {
        diagnosticRetryRequested = true;
        diagnosticRetryBlock = string.Empty;
        diagnosticVersion++;
    }

    public void SetRetryBlockDiagnostic(string guard)
    {
        string safeGuard = guard ?? string.Empty;
        if (string.Equals(diagnosticRetryBlock, safeGuard, StringComparison.Ordinal))
            return;

        diagnosticRetryBlock = safeGuard;
        diagnosticVersion++;
    }

    private void SetState(string state, string reason)
    {
        if (string.Equals(diagnosticState, state, StringComparison.Ordinal))
            return;
        diagnosticPreviousState = diagnosticState;
        diagnosticState = state ?? "READY_FAILED";
        diagnosticTransitionReason = reason ?? string.Empty;
        diagnosticVersion++;
        OnRecognitionStateChanged?.Invoke(diagnosticState);
    }

    private void ResetAttemptDiagnostics()
    {
        if (!preserveTokenlessDiagnostic)
        {
            diagnosticRaw = string.Empty;
            diagnosticBanked = string.Empty;
        }
        diagnosticAuthority = "NOT SUBMITTED";
        diagnosticPipeline = string.Empty;
        diagnosticPipelineBlock = string.Empty;
        diagnosticFailedIndex = -1;
        diagnosticInferenceMilliseconds = 0;
        diagnosticVersion++;
    }

    private void ResolveReferences()
    {
        if (whisper == null)
            whisper = GetComponent<WhisperManager>();
        if (microphoneRecord == null)
            microphoneRecord = GetComponent<MicrophoneRecord>();
    }

    private bool ValidateReferences()
    {
        ResolveReferences();
        if (whisper != null && microphoneRecord != null)
            return true;
        Debug.LogError("WhisperVoiceRecognizer requires WhisperManager and MicrophoneRecord references.", this);
        return false;
    }

    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void Log(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[WhisperPhrase] {message}", this);
    }
}
