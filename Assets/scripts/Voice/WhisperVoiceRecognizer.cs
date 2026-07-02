using System;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;
using Whisper;
using Whisper.Utils;
using Debug = UnityEngine.Debug;

/// <summary>
/// Captures one complete microphone recording and transcribes it once through Whisper.
/// Depends on WhisperManager and MicrophoneRecord on this object or serialized in the Inspector.
/// Raises a single final transcript per completed listening session and leaves ritual validation to gameplay systems.
/// </summary>
public class WhisperVoiceRecognizer : MonoBehaviour, IVoiceRecognizer, IVoiceInput, IVoiceRecognizerProcessingStatus
{
    private const float MinimumAllowedRecordingLengthSeconds = 0.35f;
    private const float DefaultSpeechEndSilenceSeconds = 0.45f;
    private const float MinimumAllowedSpeechEndSilenceSeconds = 0.2f;
    private const int MinimumExpectedPhraseWordCount = 1;
    private const int EmptyTranscriptRetryWordCount = 4;
    private const float MeaningfulDetectedSpeechRecordingSeconds = 1.0f;
    private const float EmptyTranscriptRetryPaddingSeconds = 0.35f;

    [Header("Whisper")]
    [SerializeField] private WhisperManager whisper;
    [SerializeField] private MicrophoneRecord microphoneRecord;

    [Header("Recognition")]
    [SerializeField] private bool ignoreEmptyTranscripts = true;
    [SerializeField] private float minimumRecordingLengthSeconds = MinimumAllowedRecordingLengthSeconds;

    [Header("Speech End Detection")]
    [SerializeField] private bool enableSpeechEndAutoStop = true;
    [SerializeField, Min(MinimumAllowedSpeechEndSilenceSeconds)] private float speechEndSilenceSeconds = DefaultSpeechEndSilenceSeconds;
    [SerializeField] private bool useDynamicSpeechEndSilence = true;
    [SerializeField, Min(MinimumAllowedSpeechEndSilenceSeconds)] private float oneWordSpeechEndSilenceSeconds = 0.45f;
    [SerializeField, Min(MinimumAllowedSpeechEndSilenceSeconds)] private float twoWordSpeechEndSilenceSeconds = 0.55f;
    [SerializeField, Min(MinimumAllowedSpeechEndSilenceSeconds)] private float threeWordSpeechEndSilenceSeconds = 0.7f;
    [SerializeField, Min(MinimumAllowedSpeechEndSilenceSeconds)] private float fourOrMoreWordSpeechEndSilenceSeconds = 0.85f;
    [SerializeField, Min(MinimumExpectedPhraseWordCount)] private int expectedPhraseWordCount = MinimumExpectedPhraseWordCount;
    [SerializeField] private bool autoEnableMicrophoneVad = true;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool logRecognizedPhrases = true;

    private bool isListening;
    private bool isStarting;
    private bool isTranscribing;
    private bool isProcessingRecognition;
    private bool discardNextRecording;
    private bool cancelPendingStart;
    private bool hasSpeechStarted;
    private bool hasTemporaryMicrophoneVadOverride;
    private bool previousMicrophoneUseVad;
    private bool sessionSpeechDetected;
    private float lastSpeechDetectedAt;
    private float sessionActiveSpeechDuration;
    private float lastSpeechDetectionUpdateAt;
    private int listeningSessionId;
    private long lastRecognitionLatencyMs;

    public bool IsListening => isListening;
    public bool IsProcessingRecognition => isProcessingRecognition || isTranscribing;

    public event Action<string> OnPhraseRecognized;

    public void SetExpectedPhraseWordCount(int wordCount)
    {
        expectedPhraseWordCount = Mathf.Max(MinimumExpectedPhraseWordCount, wordCount);
    }

    private void Awake()
    {
        ApplyRecognitionTimingMinimums();
        ResolveReferences();
    }

    private void OnEnable()
    {
        ApplyRecognitionTimingMinimums();
        ResolveReferences();

        if (microphoneRecord != null)
            microphoneRecord.OnRecordStop += HandleRecordStop;
    }

    private void OnDisable()
    {
        StopListeningAndDiscardRecording();

        if (microphoneRecord != null)
            microphoneRecord.OnRecordStop -= HandleRecordStop;
    }

    private void Update()
    {
        UpdateSpeechEndDetection();
    }

    public async void StartListening()
    {
        if (!ValidateReferences())
            return;

        if (isListening || microphoneRecord.IsRecording)
        {
            Log("Start ignored because Whisper voice recognition is already listening.");
            isListening = true;
            return;
        }

        if (isStarting)
        {
            Log("Start ignored because Whisper voice recognition is already starting.");
            return;
        }

        if (isTranscribing)
        {
            Log("Start ignored because Whisper is still transcribing the previous recording.");
            return;
        }

        isStarting = true;
        discardNextRecording = false;
        cancelPendingStart = false;

        try
        {
            await EnsureWhisperIsReady();

            if (cancelPendingStart)
            {
                Log("Whisper voice recognition start canceled before recording began.");
                return;
            }

            if (!isActiveAndEnabled)
            {
                Log("Whisper voice recognition start canceled because the component is no longer active.");
                return;
            }

            if (whisper == null || !whisper.IsLoaded)
            {
                Debug.LogWarning("WhisperVoiceRecognizer could not start because the Whisper model is not loaded.", this);
                return;
            }

            StartRecordedRecognition();
            microphoneRecord.StartRecord();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"WhisperVoiceRecognizer failed to start: {exception.Message}", this);
            isListening = false;
            EndSpeechEndDetection();
        }
        finally
        {
            isStarting = false;
        }
    }

    public void StopListening()
    {
        StopListeningAndTranscribeRecording();
    }

    public void CancelListening()
    {
        StopListeningAndDiscardRecording();
    }

    private void StopListeningAndTranscribeRecording()
    {
        if (microphoneRecord == null)
        {
            isListening = false;
            cancelPendingStart = true;
            return;
        }

        if (isStarting && !microphoneRecord.IsRecording)
        {
            isListening = false;
            cancelPendingStart = true;
            Log("Whisper voice recognition stop requested while startup is pending.");
            return;
        }

        if (!isListening && !microphoneRecord.IsRecording)
            return;

        isListening = false;
        EndSpeechEndDetection();

        if (!microphoneRecord.IsRecording)
        {
            Log("Whisper voice recognition stopped without an active microphone recording.");
            return;
        }

        isProcessingRecognition = true;
        discardNextRecording = false;
        Log($"Whisper voice recognition stopping. Session {listeningSessionId} will be transcribed.");
        microphoneRecord.StopRecord();
    }

    private void StopListeningAndDiscardRecording()
    {
        if (microphoneRecord == null)
        {
            isListening = false;
            cancelPendingStart = true;
            return;
        }

        if (isStarting && !microphoneRecord.IsRecording)
        {
            isListening = false;
            discardNextRecording = true;
            cancelPendingStart = true;
            Log("Whisper voice recognition cancel requested while startup is pending.");
            return;
        }

        if (!isListening && !microphoneRecord.IsRecording)
            return;

        isListening = false;
        discardNextRecording = true;
        EndSpeechEndDetection();

        if (!microphoneRecord.IsRecording)
            return;

        Log($"Whisper voice recognition stopping. Session {listeningSessionId} recording will be discarded.");
        microphoneRecord.StopRecord();
    }

    private async void HandleRecordStop(AudioChunk recordedAudio)
    {
        int sessionId = listeningSessionId;
        isListening = false;
        EndSpeechEndDetection();

        if (discardNextRecording)
        {
            discardNextRecording = false;
            isProcessingRecognition = false;
            Log($"Whisper voice recognition discarded recording for session {sessionId}.");
            return;
        }

        isProcessingRecognition = true;

        try
        {
            await TranscribeRecording(recordedAudio, sessionId);
        }
        finally
        {
            isProcessingRecognition = false;
        }
    }

    private void StartRecordedRecognition()
    {
        listeningSessionId++;
        isListening = true;
        BeginSpeechEndDetection();
        Log($"Whisper voice recognition recording started. Session {listeningSessionId}.");
    }

    private void BeginSpeechEndDetection()
    {
        hasSpeechStarted = false;
        sessionSpeechDetected = false;
        lastSpeechDetectedAt = 0f;
        sessionActiveSpeechDuration = 0f;
        lastSpeechDetectionUpdateAt = Time.realtimeSinceStartup;

        if (!enableSpeechEndAutoStop || microphoneRecord == null)
            return;

        if (!autoEnableMicrophoneVad || microphoneRecord.useVad)
            return;

        previousMicrophoneUseVad = microphoneRecord.useVad;
        hasTemporaryMicrophoneVadOverride = true;
        microphoneRecord.useVad = true;
    }

    private void EndSpeechEndDetection()
    {
        hasSpeechStarted = false;
        lastSpeechDetectedAt = 0f;
        lastSpeechDetectionUpdateAt = 0f;

        if (!hasTemporaryMicrophoneVadOverride || microphoneRecord == null)
            return;

        microphoneRecord.useVad = previousMicrophoneUseVad;
        hasTemporaryMicrophoneVadOverride = false;
    }

    private void UpdateSpeechEndDetection()
    {
        if (!enableSpeechEndAutoStop || !isListening || isStarting || isTranscribing || isProcessingRecognition)
            return;

        if (microphoneRecord == null || !microphoneRecord.IsRecording)
            return;

        if (!microphoneRecord.useVad)
            return;

        float now = Time.realtimeSinceStartup;
        float deltaSeconds = Mathf.Max(0f, now - lastSpeechDetectionUpdateAt);
        lastSpeechDetectionUpdateAt = now;

        if (microphoneRecord.IsVoiceDetected)
        {
            if (!hasSpeechStarted)
            {
                Log($"Whisper voice recognition detected speech. Session {listeningSessionId}. Expected words: {expectedPhraseWordCount}. VAD threshold: {microphoneRecord.vadThd:0.00}.");
            }

            if (hasSpeechStarted)
                sessionActiveSpeechDuration += deltaSeconds;

            hasSpeechStarted = true;
            sessionSpeechDetected = true;
            lastSpeechDetectedAt = now;
            return;
        }

        if (!hasSpeechStarted)
            return;

        float silenceSeconds = now - lastSpeechDetectedAt;

        float activeSpeechEndSilenceSeconds = GetActiveSpeechEndSilenceSeconds();

        if (silenceSeconds < activeSpeechEndSilenceSeconds)
            return;

        Log($"Whisper voice recognition detected speech end after {silenceSeconds:0.00}s of silence using {activeSpeechEndSilenceSeconds:0.00}s threshold for {expectedPhraseWordCount} expected word(s). Active speech: {sessionActiveSpeechDuration:0.00}s. Session {listeningSessionId} will be transcribed.");
        StopListeningAndTranscribeRecording();
    }

    private async Task TranscribeRecording(AudioChunk recordedAudio, int sessionId)
    {
        if (!ValidateReferences())
            return;

        if (recordedAudio.Data == null || recordedAudio.Data.Length == 0)
        {
            Log($"Whisper voice recognition emitted no transcript for session {sessionId}. Reason: empty audio. Expected words: {expectedPhraseWordCount}. Speech detected: {sessionSpeechDetected}. Active speech: {sessionActiveSpeechDuration:0.00}s.");
            return;
        }

        if (recordedAudio.Length < minimumRecordingLengthSeconds)
        {
            Log($"Whisper voice recognition emitted no transcript for session {sessionId}. Reason: short audio ({recordedAudio.Length:0.00}s < {minimumRecordingLengthSeconds:0.00}s). Expected words: {expectedPhraseWordCount}. Speech detected: {sessionSpeechDetected}. Active speech: {sessionActiveSpeechDuration:0.00}s.");
            return;
        }

        isTranscribing = true;
        Stopwatch stopwatch = Stopwatch.StartNew();

        try
        {
            bool speechWasDetected = sessionSpeechDetected || recordedAudio.IsVoiceDetected;
            float activeSpeechDuration = sessionActiveSpeechDuration;
            float activeSpeechEndSilenceSeconds = GetActiveSpeechEndSilenceSeconds();

            Log($"Whisper voice recognition transcribing final recording for session {sessionId}: {recordedAudio.Length:0.00}s, {recordedAudio.Frequency}Hz, {recordedAudio.Channels} channel(s). Expected words: {expectedPhraseWordCount}. Speech detected: {speechWasDetected}. Active speech: {activeSpeechDuration:0.00}s. Silence threshold: {activeSpeechEndSilenceSeconds:0.00}s. VAD threshold: {GetVadThresholdLogValue()}.");

            WhisperResult result = await whisper.GetTextAsync(recordedAudio.Data, recordedAudio.Frequency, recordedAudio.Channels);
            stopwatch.Stop();
            lastRecognitionLatencyMs = stopwatch.ElapsedMilliseconds;

            string recognizedText = result != null ? result.Result.Trim() : string.Empty;

            if (IsEmptyTranscript(recognizedText) && ShouldRetryEmptyTranscript(recordedAudio, speechWasDetected))
            {
                Log($"Whisper voice recognition returned an empty transcript for session {sessionId} despite detected speech. Retrying once with {EmptyTranscriptRetryPaddingSeconds:0.00}s padding for {expectedPhraseWordCount} expected word(s). Latency: {lastRecognitionLatencyMs} ms.");

                stopwatch.Restart();
                float[] paddedAudio = CreatePaddedAudio(recordedAudio.Data, recordedAudio.Frequency, recordedAudio.Channels, EmptyTranscriptRetryPaddingSeconds);
                result = await whisper.GetTextAsync(paddedAudio, recordedAudio.Frequency, recordedAudio.Channels);
                stopwatch.Stop();
                lastRecognitionLatencyMs += stopwatch.ElapsedMilliseconds;
                recognizedText = result != null ? result.Result.Trim() : string.Empty;
            }

            if (IsEmptyTranscript(recognizedText))
            {
                Log($"Whisper voice recognition found no speech in session {sessionId}. Expected words: {expectedPhraseWordCount}. Speech detected: {speechWasDetected}. Recording duration: {recordedAudio.Length:0.00}s. Active speech: {activeSpeechDuration:0.00}s. Silence threshold: {activeSpeechEndSilenceSeconds:0.00}s. Latency: {lastRecognitionLatencyMs} ms.");

                if (!ShouldEmitEmptyTranscript(recordedAudio, speechWasDetected) && (ignoreEmptyTranscripts || IsPunctuationOnlyTranscript(recognizedText)))
                {
                    Log($"Whisper voice recognition emitted no transcript for session {sessionId}. Reason: empty Whisper result ignored.");
                    return;
                }
            }
            else if (logRecognizedPhrases)
            {
                Debug.Log($"Whisper final phrase recognized in {lastRecognitionLatencyMs} ms for {expectedPhraseWordCount} expected word(s): \"{recognizedText}\"", this);
            }

            OnPhraseRecognized?.Invoke(recognizedText);
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            Debug.LogWarning($"WhisperVoiceRecognizer transcription failed: {exception.Message}", this);
        }
        finally
        {
            isTranscribing = false;
        }
    }

    private async Task EnsureWhisperIsReady()
    {
        if (whisper == null || whisper.IsLoaded)
            return;

        Log(whisper.IsLoading ? "Waiting for Whisper model to finish loading." : "Initializing Whisper model.");

        if (!whisper.IsLoading)
            await whisper.InitModel();

        while (whisper.IsLoading)
            await Task.Yield();
    }

    private void ApplyRecognitionTimingMinimums()
    {
        minimumRecordingLengthSeconds = Mathf.Max(minimumRecordingLengthSeconds, MinimumAllowedRecordingLengthSeconds);
        speechEndSilenceSeconds = Mathf.Max(speechEndSilenceSeconds, MinimumAllowedSpeechEndSilenceSeconds);
        oneWordSpeechEndSilenceSeconds = Mathf.Max(oneWordSpeechEndSilenceSeconds, MinimumAllowedSpeechEndSilenceSeconds);
        twoWordSpeechEndSilenceSeconds = Mathf.Max(twoWordSpeechEndSilenceSeconds, MinimumAllowedSpeechEndSilenceSeconds);
        threeWordSpeechEndSilenceSeconds = Mathf.Max(threeWordSpeechEndSilenceSeconds, MinimumAllowedSpeechEndSilenceSeconds);
        fourOrMoreWordSpeechEndSilenceSeconds = Mathf.Max(fourOrMoreWordSpeechEndSilenceSeconds, MinimumAllowedSpeechEndSilenceSeconds);
        expectedPhraseWordCount = Mathf.Max(expectedPhraseWordCount, MinimumExpectedPhraseWordCount);
    }

    private float GetActiveSpeechEndSilenceSeconds()
    {
        if (!useDynamicSpeechEndSilence)
            return speechEndSilenceSeconds;

        if (expectedPhraseWordCount <= 1)
            return oneWordSpeechEndSilenceSeconds;

        if (expectedPhraseWordCount == 2)
            return twoWordSpeechEndSilenceSeconds;

        if (expectedPhraseWordCount == 3)
            return threeWordSpeechEndSilenceSeconds;

        return fourOrMoreWordSpeechEndSilenceSeconds;
    }

    private bool IsEmptyTranscript(string transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript))
            return true;

        return IsPunctuationOnlyTranscript(transcript);
    }

    private bool IsPunctuationOnlyTranscript(string transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript))
            return false;

        foreach (char character in transcript)
        {
            if (char.IsLetterOrDigit(character))
                return false;
        }

        return true;
    }

    private bool ShouldRetryEmptyTranscript(AudioChunk recordedAudio, bool speechWasDetected)
    {
        return expectedPhraseWordCount >= EmptyTranscriptRetryWordCount &&
            speechWasDetected &&
            recordedAudio.Length >= MeaningfulDetectedSpeechRecordingSeconds;
    }

    private bool ShouldEmitEmptyTranscript(AudioChunk recordedAudio, bool speechWasDetected)
    {
        return expectedPhraseWordCount >= EmptyTranscriptRetryWordCount &&
            speechWasDetected &&
            recordedAudio.Length >= MeaningfulDetectedSpeechRecordingSeconds;
    }

    private string GetVadThresholdLogValue()
    {
        if (microphoneRecord == null || !microphoneRecord.useVad)
            return "disabled";

        return microphoneRecord.vadThd.ToString("0.00");
    }

    private static float[] CreatePaddedAudio(float[] audioData, int frequency, int channels, float paddingSeconds)
    {
        int paddingSamples = Mathf.Max(0, Mathf.RoundToInt(frequency * Mathf.Max(1, channels) * paddingSeconds));

        if (paddingSamples <= 0)
            return audioData;

        float[] paddedAudio = new float[audioData.Length + paddingSamples * 2];
        Array.Copy(audioData, 0, paddedAudio, paddingSamples, audioData.Length);
        return paddedAudio;
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

        bool hasReferences = true;

        if (whisper == null)
        {
            Debug.LogWarning("WhisperVoiceRecognizer requires a WhisperManager reference.", this);
            hasReferences = false;
        }

        if (microphoneRecord == null)
        {
            Debug.LogWarning("WhisperVoiceRecognizer requires a MicrophoneRecord reference.", this);
            hasReferences = false;
        }

        return hasReferences;
    }

    private void Log(string message)
    {
        if (!enableDebugLogs)
            return;

        Debug.Log(message, this);
    }
}
