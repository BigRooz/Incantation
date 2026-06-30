using System;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;
using Whisper;
using Whisper.Utils;
using Debug = UnityEngine.Debug;

public class WhisperVoiceRecognizer : MonoBehaviour, IVoiceRecognizer, IVoiceRecognizerProcessingStatus
{
    private enum RecognitionMode
    {
        RecordThenTranscribe,
        StreamingChunks
    }

    [Header("Whisper")]
    [SerializeField] private WhisperManager whisper;
    [SerializeField] private MicrophoneRecord microphoneRecord;

    [Header("Recognition")]
    [SerializeField] private RecognitionMode recognitionMode = RecognitionMode.StreamingChunks;
    [SerializeField] private bool ignoreEmptyTranscripts = true;
    [SerializeField] private float minimumRecordingLengthSeconds = 0.1f;
    [SerializeField] private float silenceRmsThreshold = 0.01f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool logRecognizedPhrases = true;

    private bool isListening;
    private bool isStarting;
    private bool isTranscribing;
    private bool isProcessingRecognition;
    private bool isStreaming;
    private bool isStoppingStream;
    private bool isSubscribedToStreamingChunks;
    private bool hasTemporaryWhisperVadOverride;
    private bool previousWhisperUseVad;
    private int listeningSessionId;
    private int submittedStreamingChunkCount;
    private long lastRecognitionLatencyMs;
    private WhisperStream activeStream;

    public bool IsListening => isListening;
    public bool IsProcessingRecognition => isProcessingRecognition || isTranscribing || isStoppingStream;

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

        try
        {
            await EnsureWhisperIsReady();

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

            if (recognitionMode == RecognitionMode.StreamingChunks)
                await StartStreamingRecognition();
            else
                StartRecordedRecognition();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"WhisperVoiceRecognizer failed to start: {exception.Message}", this);
            isListening = false;
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

    private void StopListeningAndTranscribeRecording()
    {
        if (microphoneRecord == null)
        {
            isListening = false;
            return;
        }

        if (!isListening && !microphoneRecord.IsRecording && !isStreaming)
            return;

        isListening = false;

        if (isStreaming)
        {
            StopStreamingRecognition();
            return;
        }

        if (!microphoneRecord.IsRecording)
        {
            Log("Whisper voice recognition stopped without an active microphone recording.");
            return;
        }

        isProcessingRecognition = true;
        Log($"Whisper voice recognition stopping. Session {listeningSessionId} will be transcribed.");
        microphoneRecord.StopRecord();
    }

    private void StopListeningAndDiscardRecording()
    {
        if (microphoneRecord == null)
        {
            isListening = false;
            return;
        }

        if (!isListening && !microphoneRecord.IsRecording && !isStreaming)
            return;

        isListening = false;

        if (isStreaming)
        {
            ClearStreamingChunkSubscription();
            activeStream?.StopStream();
            DisposeActiveStream();
            isStreaming = false;
            isStoppingStream = false;
        }

        EndTemporaryWhisperVadOverride();

        if (!microphoneRecord.IsRecording)
            return;

        if (microphoneRecord != null)
            microphoneRecord.OnRecordStop -= HandleRecordStop;

        Log($"Whisper voice recognition stopping. Session {listeningSessionId} recording will be discarded.");
        microphoneRecord.StopRecord();
    }

    private async void HandleRecordStop(AudioChunk recordedAudio)
    {
        if (isStreaming || isStoppingStream)
            return;

        int sessionId = listeningSessionId;
        isListening = false;
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
        microphoneRecord.StartRecord();
        isListening = true;
        Log($"Whisper voice recognition started. Session {listeningSessionId}.");
    }

    private async Task StartStreamingRecognition()
    {
        listeningSessionId++;
        int sessionId = listeningSessionId;

        submittedStreamingChunkCount = 0;
        activeStream = await CreateManualChunkStream();

        if (activeStream == null)
        {
            Debug.LogWarning("WhisperVoiceRecognizer could not start streaming because WhisperStream creation failed.", this);
            return;
        }

        activeStream.OnResultUpdated += HandleStreamResultUpdated;
        activeStream.OnSegmentUpdated += HandleStreamSegmentUpdated;
        activeStream.OnStreamFinished += HandleStreamFinished;
        microphoneRecord.OnChunkReady += HandleMicrophoneChunkReady;
        isSubscribedToStreamingChunks = true;
        activeStream.StartStream();

        string selectedDevice = GetSelectedMicrophoneDeviceLabel();
        Log(
            $"Whisper realtime microphone starting. Session {sessionId}. " +
            $"Device={selectedDevice}, requestedSampleRate={microphoneRecord.frequency}Hz, " +
            $"chunkDuration={microphoneRecord.chunksLengthSec:0.00}s, streamVadDisabledForManualChunks=True.");

        microphoneRecord.StartRecord();
        int initialMicrophonePosition = microphoneRecord.IsRecording
            ? Microphone.GetPosition(microphoneRecord.RecordStartMicDevice)
            : -1;
        isListening = true;
        isStreaming = true;
        isStoppingStream = false;
        Log(
            $"Whisper realtime listening started. Session {sessionId}. " +
            $"RecordStartDevice={GetRecordStartMicrophoneDeviceLabel()}, sampleRate={microphoneRecord.frequency}Hz, " +
            $"chunkDuration={microphoneRecord.chunksLengthSec:0.00}s, isRecording={microphoneRecord.IsRecording}, " +
            $"initialMicPosition={initialMicrophonePosition}. Local microphone chunks only.");
    }

    private void StopStreamingRecognition()
    {
        if (!isStreaming)
            return;

        isListening = false;
        isStreaming = false;
        isStoppingStream = true;
        ClearStreamingChunkSubscription();
        Log($"Whisper realtime listening stopping. Session {listeningSessionId} submitted {submittedStreamingChunkCount} chunk(s).");

        if (microphoneRecord != null && microphoneRecord.IsRecording)
            microphoneRecord.StopRecord();

        if (activeStream != null)
            activeStream.StopStream();
        else
            isStoppingStream = false;
    }

    private async Task<WhisperStream> CreateManualChunkStream()
    {
        if (whisper == null || microphoneRecord == null)
            return null;

        BeginTemporaryWhisperVadOverride();

        try
        {
            return await whisper.CreateStream(microphoneRecord.frequency, 1);
        }
        finally
        {
            EndTemporaryWhisperVadOverride();
        }
    }

    private void BeginTemporaryWhisperVadOverride()
    {
        if (whisper == null)
            return;

        previousWhisperUseVad = whisper.useVad;
        hasTemporaryWhisperVadOverride = true;
        whisper.useVad = false;
    }

    private void EndTemporaryWhisperVadOverride()
    {
        if (!hasTemporaryWhisperVadOverride || whisper == null)
            return;

        whisper.useVad = previousWhisperUseVad;
        hasTemporaryWhisperVadOverride = false;
    }

    private void HandleMicrophoneChunkReady(AudioChunk chunk)
    {
        if (activeStream == null || !isStreaming)
            return;

        int sampleCount = chunk.Data != null ? chunk.Data.Length : 0;
        float rms = CalculateRms(chunk.Data);
        bool isAboveSilenceThreshold = rms >= silenceRmsThreshold;

        Log(
            $"Whisper microphone chunk captured. Session {listeningSessionId}, " +
            $"samples={sampleCount}, frequency={chunk.Frequency}Hz, channels={chunk.Channels}, " +
            $"duration={chunk.Length:0.00}s, rms={rms:0.000000}, aboveSilenceThreshold={isAboveSilenceThreshold}, " +
            $"packageVad={chunk.IsVoiceDetected}.");

        if (sampleCount == 0)
        {
            Log($"Whisper microphone chunk skipped because it contained no samples. Session {listeningSessionId}.");
            return;
        }

        submittedStreamingChunkCount++;
        Log($"Whisper submitting chunk {submittedStreamingChunkCount} to WhisperStream. Session {listeningSessionId}.");
        activeStream.AddToStream(chunk);
    }

    private void HandleStreamResultUpdated(string updatedResult)
    {
        string recognizedText = updatedResult != null ? updatedResult.Trim() : string.Empty;
        Log($"WhisperStream returned text. Session {listeningSessionId}: \"{recognizedText}\"");

        if (string.IsNullOrWhiteSpace(recognizedText))
        {
            if (ignoreEmptyTranscripts)
                return;
        }

        if (logRecognizedPhrases)
            Debug.Log($"Whisper phrase candidate: \"{recognizedText}\"", this);

        OnPhraseRecognized?.Invoke(recognizedText);
    }

    private void HandleStreamSegmentUpdated(WhisperResult segment)
    {
        if (segment == null)
            return;

        string segmentText = segment.Result != null ? segment.Result.Trim() : string.Empty;
        Log($"Whisper chunk transcription started/updated. Session {listeningSessionId}: \"{segmentText}\"");
    }

    private void HandleStreamFinished(string finalResult)
    {
        string recognizedText = finalResult != null ? finalResult.Trim() : string.Empty;
        Log($"Whisper realtime listening finished. Session {listeningSessionId}: \"{recognizedText}\"");
        DisposeActiveStream();
        isStoppingStream = false;
        isProcessingRecognition = false;
    }

    private void DisposeActiveStream()
    {
        ClearStreamingChunkSubscription();

        if (activeStream == null)
            return;

        activeStream.OnResultUpdated -= HandleStreamResultUpdated;
        activeStream.OnSegmentUpdated -= HandleStreamSegmentUpdated;
        activeStream.OnStreamFinished -= HandleStreamFinished;
        activeStream = null;
    }

    private void ClearStreamingChunkSubscription()
    {
        if (!isSubscribedToStreamingChunks || microphoneRecord == null)
            return;

        microphoneRecord.OnChunkReady -= HandleMicrophoneChunkReady;
        isSubscribedToStreamingChunks = false;
    }

    private float CalculateRms(float[] samples)
    {
        if (samples == null || samples.Length == 0)
            return 0f;

        double sumSquares = 0d;

        for (int i = 0; i < samples.Length; i++)
            sumSquares += samples[i] * samples[i];

        return Mathf.Sqrt((float)(sumSquares / samples.Length));
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
            Log($"Whisper voice recognition transcribing session {sessionId}: {recordedAudio.Length:0.00}s, {recordedAudio.Frequency}Hz, {recordedAudio.Channels} channel(s).");

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
                Debug.Log($"Whisper phrase recognized in {lastRecognitionLatencyMs} ms: \"{recognizedText}\"", this);
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

    private string GetSelectedMicrophoneDeviceLabel()
    {
        if (microphoneRecord == null)
            return "None";

        return string.IsNullOrEmpty(microphoneRecord.SelectedMicDevice)
            ? "Default microphone"
            : microphoneRecord.SelectedMicDevice;
    }

    private string GetRecordStartMicrophoneDeviceLabel()
    {
        if (microphoneRecord == null)
            return "None";

        return string.IsNullOrEmpty(microphoneRecord.RecordStartMicDevice)
            ? "Default microphone"
            : microphoneRecord.RecordStartMicDevice;
    }

    private void Log(string message)
    {
        if (!enableDebugLogs)
            return;

        Debug.Log(message, this);
    }
}
