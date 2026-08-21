#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;
using Whisper;
using Whisper.Utils;
using Debug = UnityEngine.Debug;

/// <summary>
/// Development-only manual A/B harness that reproduces WhisperSandboxUI's
/// StartRecord -> StopRecord -> OnRecordStop -> GetTextAsync lifecycle.
/// It never submits, normalizes, validates, or interacts with ritual authority.
/// </summary>
public sealed class MainGameWhisperSandboxAB : MonoBehaviour
{
    private const string SandboxPrompt =
        "mor tor nok vak\ndor vak lum kor\nIncantation test words: mor tor nok vak dor lum kor.";
    private const float SandboxStepSeconds = 3f;
    private const bool SandboxManagerUseVad = true;

    private WhisperManager whisper;
    private MicrophoneRecord microphoneRecord;
    private WhisperVoiceRecognizer productionRecognizer;
    private readonly List<VoiceAmplitudeProvider> suspendedAmplitudeProviders =
        new List<VoiceAmplitudeProvider>();
    private bool productionRecognizerWasEnabled;
    private bool productionRecognizerWasListening;
    private bool ownsRecording;
    private bool isTranscribing;
    private bool sandboxSettingsApplied;
    private string originalPrompt;
    private float originalStepSeconds;
    private bool originalManagerUseVad;
    private int version;
    private string rawTranscript = string.Empty;
    private string status = "IDLE — F8 START / F9 STOP";
    private long inferenceMilliseconds;

    public int Version => version;
    public bool IsRecording => ownsRecording && microphoneRecord != null &&
        microphoneRecord.IsRecording;
    public bool IsWhisperReady => whisper != null && whisper.IsLoaded;
    public string RawTranscript => rawTranscript;
    public string Status => status;
    public long InferenceMilliseconds => inferenceMilliseconds;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateForDevelopmentBuild()
    {
        if (!Debug.isDebugBuild ||
            FindFirstObjectByType<MainGameWhisperSandboxAB>() != null ||
            FindFirstObjectByType<WhisperVoiceRecognizer>(FindObjectsInactive.Include) == null)
        {
            return;
        }

        new GameObject(nameof(MainGameWhisperSandboxAB))
            .AddComponent<MainGameWhisperSandboxAB>();
    }

    private void Awake()
    {
        if (!Debug.isDebugBuild)
        {
            Destroy(gameObject);
            return;
        }

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
        if (microphoneRecord != null)
            microphoneRecord.OnRecordStop -= HandleRecordStop;

        ownsRecording = false;
        if (microphoneRecord != null && microphoneRecord.IsRecording)
            microphoneRecord.StopRecord();
        RestoreSandboxSettings();
        RestoreCompetingMicrophoneConsumers();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F8))
            StartSandboxRecording();
        if (Input.GetKeyDown(KeyCode.F9))
            StopSandboxRecording();
    }

    private async void StartSandboxRecording()
    {
        if (ownsRecording || isTranscribing)
        {
            SetStatus("BUSY — WAIT FOR CURRENT A/B ATTEMPT");
            return;
        }

        ResolveReferences();
        if (whisper == null || microphoneRecord == null)
        {
            SetStatus("MISSING MAIN GAME WHISPER REFERENCES");
            return;
        }

        SuspendCompetingMicrophoneConsumers();
        ApplySandboxSettings();
        await EnsureWhisperIsReady();
        if (whisper == null || !whisper.IsLoaded)
        {
            SetStatus("WHISPER MODEL NOT READY");
            RestoreSandboxSettings();
            RestoreCompetingMicrophoneConsumers();
            return;
        }

        rawTranscript = string.Empty;
        inferenceMilliseconds = 0;
        microphoneRecord.StartRecord();
        ownsRecording = microphoneRecord.IsRecording;
        SetStatus(ownsRecording ? "RECORDING — PRESS F9 TO STOP" : "RECORDING FAILED");
        if (!ownsRecording)
        {
            RestoreSandboxSettings();
            RestoreCompetingMicrophoneConsumers();
        }
    }

    private void StopSandboxRecording()
    {
        if (!ownsRecording || microphoneRecord == null || !microphoneRecord.IsRecording)
        {
            SetStatus("NOT RECORDING — PRESS F8 TO START");
            return;
        }

        SetStatus("STOPPING / TRANSCRIBING");
        microphoneRecord.StopRecord();
    }

    private async void HandleRecordStop(AudioChunk recordedAudio)
    {
        if (!ownsRecording)
            return;

        ownsRecording = false;
        await TranscribeRecording(recordedAudio);
    }

    private async Task TranscribeRecording(AudioChunk recordedAudio)
    {
        if (recordedAudio.Data == null || recordedAudio.Data.Length == 0)
        {
            SetStatus("NO AUDIO CAPTURED");
            RestoreAfterAttempt();
            return;
        }

        isTranscribing = true;
        SetStatus("TRANSCRIBING");
        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            WhisperResult result = await whisper.GetTextAsync(
                recordedAudio.Data,
                recordedAudio.Frequency,
                recordedAudio.Channels);
            stopwatch.Stop();
            inferenceMilliseconds = stopwatch.ElapsedMilliseconds;
            rawTranscript = result != null ? result.Result.Trim() : string.Empty;
            SetStatus(string.IsNullOrWhiteSpace(rawTranscript)
                ? "COMPLETE — NO SPEECH RECOGNIZED"
                : "COMPLETE — F8 TO REPEAT");
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            inferenceMilliseconds = stopwatch.ElapsedMilliseconds;
            rawTranscript = string.Empty;
            SetStatus($"TRANSCRIPTION FAILED: {exception.Message}");
        }
        finally
        {
            isTranscribing = false;
            RestoreAfterAttempt();
        }
    }

    private async Task EnsureWhisperIsReady()
    {
        if (whisper == null || whisper.IsLoaded)
            return;
        if (!whisper.IsLoading)
            await whisper.InitModel();
        while (whisper != null && whisper.IsLoading)
            await Task.Yield();
    }

    private void SuspendCompetingMicrophoneConsumers()
    {
        productionRecognizer = FindFirstObjectByType<WhisperVoiceRecognizer>(
            FindObjectsInactive.Include);
        productionRecognizerWasEnabled = productionRecognizer != null &&
            productionRecognizer.enabled;
        productionRecognizerWasListening = productionRecognizerWasEnabled &&
            productionRecognizer.IsListening;
        if (productionRecognizerWasEnabled)
            productionRecognizer.enabled = false;

        suspendedAmplitudeProviders.Clear();
        foreach (VoiceAmplitudeProvider provider in
            FindObjectsByType<VoiceAmplitudeProvider>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None))
        {
            if (provider == null || !provider.IsRecording)
                continue;
            suspendedAmplitudeProviders.Add(provider);
            provider.StopRecording();
        }

        foreach (MicrophoneRecord recorder in FindObjectsByType<MicrophoneRecord>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None))
        {
            if (recorder != null && recorder != microphoneRecord && recorder.IsRecording)
            {
                Debug.LogWarning(
                    $"[SANDBOX A/B] Additional MicrophoneRecord '{recorder.name}' was recording and has been stopped for isolation.",
                    recorder);
                recorder.StopRecord();
            }
        }
    }

    private void RestoreCompetingMicrophoneConsumers()
    {
        foreach (VoiceAmplitudeProvider provider in suspendedAmplitudeProviders)
        {
            if (provider != null && provider.isActiveAndEnabled)
                provider.StartRecording();
        }
        suspendedAmplitudeProviders.Clear();

        if (productionRecognizer != null && productionRecognizerWasEnabled)
        {
            productionRecognizer.enabled = true;
            if (productionRecognizerWasListening)
                productionRecognizer.StartListening();
        }
        productionRecognizer = null;
        productionRecognizerWasEnabled = false;
        productionRecognizerWasListening = false;
    }

    private void ApplySandboxSettings()
    {
        if (whisper == null || sandboxSettingsApplied)
            return;
        originalPrompt = whisper.initialPrompt;
        originalStepSeconds = whisper.stepSec;
        originalManagerUseVad = whisper.useVad;
        whisper.initialPrompt = SandboxPrompt;
        whisper.stepSec = SandboxStepSeconds;
        whisper.useVad = SandboxManagerUseVad;
        sandboxSettingsApplied = true;
    }

    private void RestoreSandboxSettings()
    {
        if (whisper == null || !sandboxSettingsApplied)
            return;
        whisper.initialPrompt = originalPrompt;
        whisper.stepSec = originalStepSeconds;
        whisper.useVad = originalManagerUseVad;
        sandboxSettingsApplied = false;
    }

    private void RestoreAfterAttempt()
    {
        RestoreSandboxSettings();
        RestoreCompetingMicrophoneConsumers();
    }

    private void ResolveReferences()
    {
        productionRecognizer = FindFirstObjectByType<WhisperVoiceRecognizer>(
            FindObjectsInactive.Include);
        if (productionRecognizer == null)
            return;
        whisper = productionRecognizer.GetComponent<WhisperManager>();
        microphoneRecord = productionRecognizer.GetComponent<MicrophoneRecord>();
    }

    private void SetStatus(string value)
    {
        status = value ?? string.Empty;
        version++;
    }
}
#endif
