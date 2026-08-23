using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns continuous local microphone capture and exposes a clean normalized amplitude level.
/// Future lip animation, voice chat presentation, voice settings, meters, and character reactions
/// should consume this provider instead of opening their own microphone amplitude sessions.
/// This component does not perform recognition, validation, or voice chat networking.
/// </summary>
public sealed class VoiceAmplitudeProvider : MonoBehaviour
{
    private const int RecordingLengthSeconds = 1;

    [Header("Voice Settings")]
    [Tooltip("Preferred microphone device. Leave empty to use Unity's default microphone.")]
    [SerializeField] private string inputDevice;

    [Tooltip("Microphone capture rate in samples per second.")]
    [SerializeField] private int sampleRate = 16000;

    [Tooltip("Number of recent microphone samples used for each RMS calculation.")]
    [SerializeField] private int sampleWindow = 256;

    [Tooltip("Speed used to smooth changes in the exposed normalized amplitude.")]
    [SerializeField] private float smoothingSpeed = 15f;

    [Tooltip("Raw RMS levels at or below this value are treated as silence.")]
    [SerializeField] private float silenceThreshold = 0.02f;

    [Tooltip("Start microphone capture whenever this component becomes enabled.")]
    [SerializeField] private bool startOnEnable = true;

    private AudioClip recordingClip;
    private float[] sampleBuffer;
    private string[] availableDevices = Array.Empty<string>();
    private string currentDevice;
    private float maximumObservedAmplitude;
    private bool recordingRequested;
    private bool microphoneCaptureAllowed;

    public float CurrentAmplitude { get; private set; }

    public bool IsRecording { get; private set; }

    public string CurrentDevice => currentDevice;

    public string SelectedInputDevice => inputDevice;

    public float SilenceThreshold => silenceThreshold;

    public IReadOnlyList<string> AvailableDevices => availableDevices;

    private void OnEnable()
    {
        RefreshDevices();

        if (startOnEnable && microphoneCaptureAllowed)
        {
            StartRecording();
        }
    }

    private void Update()
    {
        if (!IsRecording)
        {
            return;
        }

        if (string.IsNullOrEmpty(currentDevice) || !Microphone.IsRecording(currentDevice))
        {
            Debug.LogWarning($"{nameof(VoiceAmplitudeProvider)} recording stopped because the microphone is no longer available.", this);
            recordingRequested = true;
            ClearRecordingState();
            return;
        }

        int microphonePosition = Microphone.GetPosition(currentDevice);
        int activeWindowSize = sampleBuffer.Length;
        if (microphonePosition < activeWindowSize)
        {
            SmoothAmplitude(0f);
            return;
        }

        int sampleOffset = microphonePosition - activeWindowSize;
        if (!recordingClip.GetData(sampleBuffer, sampleOffset))
        {
            SmoothAmplitude(0f);
            return;
        }

        float rmsAmplitude = CalculateRms(sampleBuffer);
        float normalizedAmplitude = NormalizeAmplitude(rmsAmplitude);
        SmoothAmplitude(normalizedAmplitude);
    }

    private void OnDisable()
    {
        StopRecording();
    }

    private void OnValidate()
    {
        sampleWindow = Mathf.Max(1, sampleWindow);
        smoothingSpeed = Mathf.Max(0f, smoothingSpeed);
        silenceThreshold = Mathf.Max(0f, silenceThreshold);
    }

    public void StartRecording()
    {
        if (!microphoneCaptureAllowed)
            return;

        recordingRequested = true;

        if (IsRecording)
        {
            return;
        }

        if (sampleRate <= 0)
        {
            Debug.LogWarning($"{nameof(VoiceAmplitudeProvider)} cannot record with invalid sample rate {sampleRate}.", this);
            return;
        }

        if (availableDevices.Length == 0)
        {
            RefreshDevices();
        }

        if (IsRecording)
        {
            return;
        }

        if (availableDevices.Length == 0)
        {
            Debug.LogWarning($"{nameof(VoiceAmplitudeProvider)} found no microphone.", this);
            return;
        }

        string device = ResolveInputDevice();
        if (string.IsNullOrEmpty(device) || !SupportsSampleRate(device, sampleRate))
        {
            Debug.LogWarning($"{nameof(VoiceAmplitudeProvider)} cannot use sample rate {sampleRate} with the selected microphone.", this);
            return;
        }

        int maximumWindowSize = sampleRate * RecordingLengthSeconds;
        int activeWindowSize = Mathf.Clamp(sampleWindow, 1, maximumWindowSize);
        EnsureSampleBuffer(activeWindowSize);

        try
        {
            recordingClip = Microphone.Start(device, true, RecordingLengthSeconds, sampleRate);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"{nameof(VoiceAmplitudeProvider)} recording failed: {exception.Message}", this);
            ClearRecordingState();
            return;
        }

        if (recordingClip == null)
        {
            Debug.LogWarning($"{nameof(VoiceAmplitudeProvider)} recording failed for microphone '{device}'.", this);
            ClearRecordingState();
            return;
        }

        currentDevice = device;
        maximumObservedAmplitude = silenceThreshold;
        CurrentAmplitude = 0f;
        IsRecording = true;
    }

    public void StopRecording()
    {
        recordingRequested = false;
        StopActiveRecording();
    }

    public void SetMicrophoneCaptureAllowed(bool allowed)
    {
        if (microphoneCaptureAllowed == allowed)
            return;

        microphoneCaptureAllowed = allowed;
        if (!microphoneCaptureAllowed)
            StopRecording();
        else if (startOnEnable && isActiveAndEnabled)
            StartRecording();
    }

    private void StopActiveRecording()
    {
        if (!string.IsNullOrEmpty(currentDevice) && Microphone.IsRecording(currentDevice))
        {
            Microphone.End(currentDevice);
        }

        ClearRecordingState();
    }

    public void RefreshDevices()
    {
        string[] refreshedDevices = Microphone.devices;
        availableDevices = refreshedDevices ?? Array.Empty<string>();

        if (IsRecording && !ContainsDevice(currentDevice))
        {
            Debug.LogWarning($"{nameof(VoiceAmplitudeProvider)} stopped because microphone '{currentDevice}' disappeared.", this);
            StopActiveRecording();
        }

        if (recordingRequested && !IsRecording && availableDevices.Length > 0)
        {
            StartRecording();
        }
    }

    public void SetInputDevice(string deviceName)
    {
        string requestedDevice = deviceName ?? string.Empty;
        if (string.Equals(inputDevice, requestedDevice, StringComparison.Ordinal))
        {
            return;
        }

        bool restartRecording = recordingRequested;
        StopRecording();
        inputDevice = requestedDevice;

        if (restartRecording)
        {
            StartRecording();
        }
    }

    public void SetSilenceThreshold(float value)
    {
        silenceThreshold = Mathf.Max(0f, value);
        maximumObservedAmplitude = Mathf.Max(maximumObservedAmplitude, silenceThreshold);
    }

    private string ResolveInputDevice()
    {
        if (!string.IsNullOrEmpty(inputDevice) && ContainsDevice(inputDevice))
        {
            return inputDevice;
        }

        return availableDevices[0];
    }

    private bool ContainsDevice(string deviceName)
    {
        if (string.IsNullOrEmpty(deviceName))
        {
            return false;
        }

        for (int i = 0; i < availableDevices.Length; i++)
        {
            if (string.Equals(availableDevices[i], deviceName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SupportsSampleRate(string device, int requestedSampleRate)
    {
        Microphone.GetDeviceCaps(device, out int minimumFrequency, out int maximumFrequency);
        if (minimumFrequency == 0 && maximumFrequency == 0)
        {
            return true;
        }

        return requestedSampleRate >= minimumFrequency && requestedSampleRate <= maximumFrequency;
    }

    private void EnsureSampleBuffer(int requiredSize)
    {
        if (sampleBuffer == null || sampleBuffer.Length != requiredSize)
        {
            sampleBuffer = new float[requiredSize];
        }
    }

    private static float CalculateRms(float[] samples)
    {
        double sumOfSquares = 0d;
        for (int i = 0; i < samples.Length; i++)
        {
            float sample = samples[i];
            sumOfSquares += sample * sample;
        }

        return Mathf.Sqrt((float)(sumOfSquares / samples.Length));
    }

    private float NormalizeAmplitude(float rmsAmplitude)
    {
        if (rmsAmplitude <= silenceThreshold)
        {
            return 0f;
        }

        maximumObservedAmplitude = Mathf.Max(maximumObservedAmplitude, rmsAmplitude);
        float audibleRange = maximumObservedAmplitude - silenceThreshold;
        if (audibleRange <= Mathf.Epsilon)
        {
            return 0f;
        }

        return Mathf.Clamp01((rmsAmplitude - silenceThreshold) / audibleRange);
    }

    private void SmoothAmplitude(float targetAmplitude)
    {
        if (smoothingSpeed <= 0f)
        {
            CurrentAmplitude = targetAmplitude;
            return;
        }

        float interpolation = 1f - Mathf.Exp(-smoothingSpeed * Time.unscaledDeltaTime);
        CurrentAmplitude = Mathf.Lerp(CurrentAmplitude, targetAmplitude, interpolation);
    }

    private void ClearRecordingState()
    {
        recordingClip = null;
        currentDevice = string.Empty;
        maximumObservedAmplitude = silenceThreshold;
        CurrentAmplitude = 0f;
        IsRecording = false;
    }
}
