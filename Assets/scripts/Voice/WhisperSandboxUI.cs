using System.Diagnostics;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Whisper;
using Whisper.Utils;
using Debug = UnityEngine.Debug;

public class WhisperSandboxUI : MonoBehaviour
{
    [Header("Whisper")]
    [SerializeField] private WhisperManager whisper;
    [SerializeField] private MicrophoneRecord microphoneRecord;

    [Header("UI")]
    [SerializeField] private Button startRecordingButton;
    [SerializeField] private Button stopRecordingButton;
    [SerializeField] private Button clearButton;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text recognizedTextOutput;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    private bool isTranscribing;
    private long lastRecognitionLatencyMs;

    private void Awake()
    {
        if (whisper == null)
            whisper = GetComponent<WhisperManager>();

        if (microphoneRecord == null)
            microphoneRecord = GetComponent<MicrophoneRecord>();
    }

    private void OnEnable()
    {
        if (startRecordingButton != null)
            startRecordingButton.onClick.AddListener(StartRecording);

        if (stopRecordingButton != null)
            stopRecordingButton.onClick.AddListener(StopRecording);

        if (clearButton != null)
            clearButton.onClick.AddListener(ClearResults);

        if (microphoneRecord != null)
            microphoneRecord.OnRecordStop += HandleRecordStop;

        RefreshUi();
    }

    private void OnDisable()
    {
        if (startRecordingButton != null)
            startRecordingButton.onClick.RemoveListener(StartRecording);

        if (stopRecordingButton != null)
            stopRecordingButton.onClick.RemoveListener(StopRecording);

        if (clearButton != null)
            clearButton.onClick.RemoveListener(ClearResults);

        if (microphoneRecord != null)
            microphoneRecord.OnRecordStop -= HandleRecordStop;
    }

    public async void StartRecording()
    {
        if (!ValidateReferences())
            return;

        if (isTranscribing)
        {
            Log("Start ignored because Whisper is still transcribing the previous recording.");
            return;
        }

        if (microphoneRecord.IsRecording)
        {
            Log("Start ignored because microphone recording is already active.");
            return;
        }

        await EnsureWhisperIsReady();

        if (whisper == null || !whisper.IsLoaded)
        {
            SetStatus("Whisper model is not loaded. Check model setup.");
            Log("Recording did not start because Whisper model is not loaded.");
            return;
        }

        microphoneRecord.StartRecord();
        SetStatus("Recording...");
        Log("Whisper sandbox recording started.");
        RefreshUi();
    }

    public void StopRecording()
    {
        if (!ValidateReferences())
            return;

        if (!microphoneRecord.IsRecording)
        {
            Log("Stop ignored because microphone recording is not active.");
            RefreshUi();
            return;
        }

        SetStatus("Stopping recording...");
        Log("Whisper sandbox recording stopped. Beginning transcription.");
        microphoneRecord.StopRecord();
        RefreshUi();
    }

    public void ClearResults()
    {
        lastRecognitionLatencyMs = 0;

        if (recognizedTextOutput != null)
            recognizedTextOutput.text = string.Empty;

        SetStatus(GetIdleStatus());
        Log("Whisper sandbox results cleared.");
        RefreshUi();
    }

    private async void HandleRecordStop(AudioChunk recordedAudio)
    {
        await TranscribeRecording(recordedAudio);
    }

    private async Task TranscribeRecording(AudioChunk recordedAudio)
    {
        if (!ValidateReferences())
            return;

        if (recordedAudio.Data == null || recordedAudio.Data.Length == 0)
        {
            SetStatus("No audio captured.");
            Log("Whisper sandbox received an empty recording.");
            RefreshUi();
            return;
        }

        isTranscribing = true;
        SetStatus("Transcribing...");
        RefreshUi();

        Stopwatch stopwatch = Stopwatch.StartNew();
        WhisperResult result = await whisper.GetTextAsync(recordedAudio.Data, recordedAudio.Frequency, recordedAudio.Channels);
        stopwatch.Stop();

        lastRecognitionLatencyMs = stopwatch.ElapsedMilliseconds;
        string recognizedText = result != null ? result.Result.Trim() : string.Empty;

        if (recognizedTextOutput != null)
            recognizedTextOutput.text = recognizedText;

        if (string.IsNullOrWhiteSpace(recognizedText))
            SetStatus($"No speech recognized. Latency: {lastRecognitionLatencyMs} ms");
        else
            SetStatus($"Recognized in {lastRecognitionLatencyMs} ms");

        Log($"Whisper sandbox transcription finished in {lastRecognitionLatencyMs} ms: \"{recognizedText}\"");

        isTranscribing = false;
        RefreshUi();
    }

    private async Task EnsureWhisperIsReady()
    {
        if (whisper == null)
            return;

        if (whisper.IsLoaded)
            return;

        SetStatus(whisper.IsLoading ? "Loading Whisper model..." : "Initializing Whisper model...");
        Log("Whisper sandbox is initializing the Whisper model.");

        if (!whisper.IsLoading)
            await whisper.InitModel();

        while (whisper.IsLoading)
            await Task.Yield();
    }

    private bool ValidateReferences()
    {
        bool hasReferences = true;

        if (whisper == null)
        {
            Debug.LogWarning("WhisperSandboxUI requires a WhisperManager reference.", this);
            hasReferences = false;
        }

        if (microphoneRecord == null)
        {
            Debug.LogWarning("WhisperSandboxUI requires a MicrophoneRecord reference.", this);
            hasReferences = false;
        }

        return hasReferences;
    }

    private void RefreshUi()
    {
        bool isRecording = microphoneRecord != null && microphoneRecord.IsRecording;
        bool canRecord = whisper != null && microphoneRecord != null && !isTranscribing;

        if (startRecordingButton != null)
            startRecordingButton.interactable = canRecord && !isRecording;

        if (stopRecordingButton != null)
            stopRecordingButton.interactable = canRecord && isRecording;

        if (clearButton != null)
            clearButton.interactable = !isRecording && !isTranscribing;

        if (statusText != null && string.IsNullOrWhiteSpace(statusText.text))
            statusText.text = GetIdleStatus();
    }

    private string GetIdleStatus()
    {
        return lastRecognitionLatencyMs > 0 ? $"Idle. Last latency: {lastRecognitionLatencyMs} ms" : "Idle.";
    }

    private void SetStatus(string status)
    {
        if (statusText != null)
            statusText.text = status;
    }

    private void Log(string message)
    {
        if (!enableDebugLogs)
            return;

        Debug.Log(message, this);
    }
}
