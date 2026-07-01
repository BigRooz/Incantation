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
    [Header("Whisper")]
    [SerializeField] private WhisperManager whisper;
    [SerializeField] private MicrophoneRecord microphoneRecord;

    [Header("Recognition")]
    [SerializeField] private bool ignoreEmptyTranscripts = true;
    [SerializeField] private float minimumRecordingLengthSeconds = 0.1f;

    [Header("Speech End Detection")]
    [SerializeField] private bool enableSpeechEndAutoStop = true;
    [SerializeField] private float speechEndSilenceSeconds = 0.8f;
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
    private float lastSpeechDetectedAt;
    private int listeningSessionId;
    private long lastRecognitionLatencyMs;

    public bool IsListening => isListening;
    public bool IsProcessingRecognition => isProcessingRecognition || isTranscribing;

    public event Action<string> OnPhraseRecognized;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
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
        lastSpeechDetectedAt = 0f;

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

        if (microphoneRecord.IsVoiceDetected)
        {
            if (!hasSpeechStarted)
                Log($"Whisper voice recognition detected speech. Session {listeningSessionId}.");

            hasSpeechStarted = true;
            lastSpeechDetectedAt = Time.realtimeSinceStartup;
            return;
        }

        if (!hasSpeechStarted)
            return;

        float silenceSeconds = Time.realtimeSinceStartup - lastSpeechDetectedAt;

        if (silenceSeconds < speechEndSilenceSeconds)
            return;

        Log($"Whisper voice recognition detected speech end after {silenceSeconds:0.00}s of silence. Session {listeningSessionId} will be transcribed.");
        StopListeningAndTranscribeRecording();
    }

    private async Task TranscribeRecording(AudioChunk recordedAudio, int sessionId)
    {
        if (!ValidateReferences())
            return;

        if (recordedAudio.Data == null || recordedAudio.Data.Length == 0)
        {
            Log($"Whisper voice recognition ignored empty audio for session {sessionId}.");
            return;
        }

        if (recordedAudio.Length < minimumRecordingLengthSeconds)
        {
            Log($"Whisper voice recognition ignored short audio for session {sessionId}: {recordedAudio.Length:0.00}s.");
            return;
        }

        isTranscribing = true;
        Stopwatch stopwatch = Stopwatch.StartNew();

        try
        {
            Log($"Whisper voice recognition transcribing final recording for session {sessionId}: {recordedAudio.Length:0.00}s, {recordedAudio.Frequency}Hz, {recordedAudio.Channels} channel(s).");

            WhisperResult result = await whisper.GetTextAsync(recordedAudio.Data, recordedAudio.Frequency, recordedAudio.Channels);
            stopwatch.Stop();
            lastRecognitionLatencyMs = stopwatch.ElapsedMilliseconds;

            string recognizedText = result != null ? result.Result.Trim() : string.Empty;

            if (string.IsNullOrWhiteSpace(recognizedText))
            {
                Log($"Whisper voice recognition found no speech in session {sessionId}. Latency: {lastRecognitionLatencyMs} ms.");

                if (ignoreEmptyTranscripts)
                    return;
            }
            else if (logRecognizedPhrases)
            {
                Debug.Log($"Whisper final phrase recognized in {lastRecognitionLatencyMs} ms: \"{recognizedText}\"", this);
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
