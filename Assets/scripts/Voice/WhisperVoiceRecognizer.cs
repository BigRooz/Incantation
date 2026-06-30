using System;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;
using Whisper;
using Whisper.Utils;
using Debug = UnityEngine.Debug;

public class WhisperVoiceRecognizer : MonoBehaviour, IVoiceRecognizer
{
    [Header("Whisper")]
    [SerializeField] private WhisperManager whisper;
    [SerializeField] private MicrophoneRecord microphoneRecord;

    [Header("Recognition")]
    [SerializeField] private bool ignoreEmptyTranscripts = true;
    [SerializeField] private float minimumRecordingLengthSeconds = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool logRecognizedPhrases = true;

    private bool isListening;
    private bool isStarting;
    private bool isTranscribing;
    private int listeningSessionId;
    private long lastRecognitionLatencyMs;

    public bool IsListening => isListening;

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

            listeningSessionId++;
            microphoneRecord.StartRecord();
            isListening = true;
            Log($"Whisper voice recognition started. Session {listeningSessionId}.");
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

        if (!isListening && !microphoneRecord.IsRecording)
            return;

        isListening = false;

        if (!microphoneRecord.IsRecording)
        {
            Log("Whisper voice recognition stopped without an active microphone recording.");
            return;
        }

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

        if (!isListening && !microphoneRecord.IsRecording)
            return;

        isListening = false;

        if (!microphoneRecord.IsRecording)
            return;

        if (microphoneRecord != null)
            microphoneRecord.OnRecordStop -= HandleRecordStop;

        Log($"Whisper voice recognition stopping. Session {listeningSessionId} recording will be discarded.");
        microphoneRecord.StopRecord();
    }

    private async void HandleRecordStop(AudioChunk recordedAudio)
    {
        int sessionId = listeningSessionId;
        isListening = false;

        await TranscribeRecording(recordedAudio, sessionId);
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

    private void Log(string message)
    {
        if (!enableDebugLogs)
            return;

        Debug.Log(message, this);
    }
}
