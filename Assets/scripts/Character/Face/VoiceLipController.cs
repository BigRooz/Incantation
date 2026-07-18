using UnityEngine;
using Whisper.Utils;

/// <summary>
/// Animates separate upper- and lower-lip transforms from the existing microphone recording stream.
/// Depends on a MicrophoneRecord supplied in the Inspector and does not own microphone capture.
/// </summary>
public sealed class VoiceLipController : MonoBehaviour
{
    private const float MinimumVoiceRange = 0.0001f;
    private const float MinimumSmoothingSpeed = 0.01f;

    [Header("Lips")]
    [Tooltip("Upper lip Transform. Only its local X rotation is animated.")]
    [SerializeField] private Transform upperLip;

    [Tooltip("Lower lip Transform. Only its local X rotation is animated.")]
    [SerializeField] private Transform lowerLip;

    [Header("Voice Input")]
    [Tooltip("Existing project microphone recorder used as the voice amplitude source. This component never starts or stops it.")]
    [SerializeField] private MicrophoneRecord microphoneRecord;

    [Header("Mouth Angles")]
    [Tooltip("Local X rotation, in degrees, used when the mouth is closed.")]
    [SerializeField] private float closedAngle = 0f;

    [Tooltip("Upper lip local X rotation, in degrees, used at maximum openness.")]
    [SerializeField] private float upperOpenAngle = -20f;

    [Tooltip("Lower lip local X rotation, in degrees, used at maximum openness.")]
    [SerializeField] private float lowerOpenAngle = 20f;

    [Header("Response")]
    [Tooltip("RMS voice levels at or below this value keep the mouth closed.")]
    [SerializeField] private float voiceThreshold = 0.02f;

    [Tooltip("RMS voice level that produces a fully open mouth.")]
    [SerializeField] private float maxVoiceLevel = 0.25f;

    [Tooltip("Speed used to smooth changes in mouth openness.")]
    [SerializeField] private float smoothingSpeed = 15f;

    private Vector3 upperOriginalLocalEulerAngles;
    private Vector3 lowerOriginalLocalEulerAngles;
    private Quaternion upperOriginalLocalRotation;
    private Quaternion lowerOriginalLocalRotation;
    private float currentVoiceLevel;
    private float currentOpenAmount;
    private bool hasCapturedOriginalRotations;
    private bool hasInitialized;
    private bool isSubscribed;
    private bool hasLoggedMissingVoiceSourceWarning;

    private void Start()
    {
        ClampSettings();
        CaptureOriginalRotations();

        if (upperLip == null && lowerLip == null)
        {
            Debug.LogWarning(
                $"{nameof(VoiceLipController)} on '{name}' has no lip references and has been disabled.",
                this);
            enabled = false;
            return;
        }

        if (upperLip == null || lowerLip == null)
        {
            Debug.LogWarning(
                $"{nameof(VoiceLipController)} on '{name}' is missing one lip reference. The assigned lip will still animate.",
                this);
        }

        SubscribeToVoiceInput();
        ApplyOpenAmount(0f);
        hasInitialized = true;
    }

    private void OnEnable()
    {
        if (!hasInitialized)
        {
            return;
        }

        SubscribeToVoiceInput();
        ApplyOpenAmount(0f);
    }

    private void Update()
    {
        float targetOpenAmount = 0f;

        if (microphoneRecord != null && microphoneRecord.IsRecording)
        {
            targetOpenAmount = Mathf.InverseLerp(voiceThreshold, maxVoiceLevel, currentVoiceLevel);
        }

        float interpolation = 1f - Mathf.Exp(-smoothingSpeed * Time.deltaTime);
        currentOpenAmount = Mathf.Lerp(currentOpenAmount, targetOpenAmount, interpolation);
        ApplyOpenAmount(currentOpenAmount);
    }

    private void OnDisable()
    {
        UnsubscribeFromVoiceInput();
        currentVoiceLevel = 0f;
        currentOpenAmount = 0f;
        RestoreOriginalRotations();
    }

    private void OnValidate()
    {
        ClampSettings();
    }

    /// <summary>
    /// Immediately applies a normalized mouth openness without reading microphone input.
    /// Call each frame when a future system needs sustained manual control.
    /// </summary>
    public void SetOpenAmount(float amount)
    {
        if (!hasCapturedOriginalRotations)
        {
            CaptureOriginalRotations();
        }

        currentOpenAmount = Mathf.Clamp01(amount);
        ApplyOpenAmount(currentOpenAmount);
    }

    private void SubscribeToVoiceInput()
    {
        if (microphoneRecord == null)
        {
            if (!hasLoggedMissingVoiceSourceWarning)
            {
                Debug.LogWarning(
                    $"{nameof(VoiceLipController)} on '{name}' has no valid voice amplitude source. The lips will remain closed.",
                    this);
                hasLoggedMissingVoiceSourceWarning = true;
            }

            return;
        }

        microphoneRecord.OnChunkReady += HandleVoiceChunk;
        isSubscribed = true;
    }

    private void UnsubscribeFromVoiceInput()
    {
        if (!isSubscribed || microphoneRecord == null)
        {
            return;
        }

        microphoneRecord.OnChunkReady -= HandleVoiceChunk;
        isSubscribed = false;
    }

    private void HandleVoiceChunk(AudioChunk chunk)
    {
        if (chunk.Data == null || chunk.Data.Length == 0)
        {
            currentVoiceLevel = 0f;
            return;
        }

        double sumOfSquares = 0d;
        for (int i = 0; i < chunk.Data.Length; i++)
        {
            float sample = chunk.Data[i];
            sumOfSquares += sample * sample;
        }

        currentVoiceLevel = Mathf.Sqrt((float)(sumOfSquares / chunk.Data.Length));
    }

    private void CaptureOriginalRotations()
    {
        if (upperLip != null)
        {
            upperOriginalLocalRotation = upperLip.localRotation;
            upperOriginalLocalEulerAngles = upperLip.localEulerAngles;
        }

        if (lowerLip != null)
        {
            lowerOriginalLocalRotation = lowerLip.localRotation;
            lowerOriginalLocalEulerAngles = lowerLip.localEulerAngles;
        }

        hasCapturedOriginalRotations = true;
    }

    private void ApplyOpenAmount(float amount)
    {
        SetLocalRotationX(upperLip, upperOriginalLocalEulerAngles, Mathf.Lerp(closedAngle, upperOpenAngle, amount));
        SetLocalRotationX(lowerLip, lowerOriginalLocalEulerAngles, Mathf.Lerp(closedAngle, lowerOpenAngle, amount));
    }

    private static void SetLocalRotationX(Transform lip, Vector3 originalEulerAngles, float angle)
    {
        if (lip == null)
        {
            return;
        }

        lip.localRotation = Quaternion.Euler(angle, originalEulerAngles.y, originalEulerAngles.z);
    }

    private void RestoreOriginalRotations()
    {
        if (!hasCapturedOriginalRotations)
        {
            return;
        }

        if (upperLip != null)
        {
            upperLip.localRotation = upperOriginalLocalRotation;
        }

        if (lowerLip != null)
        {
            lowerLip.localRotation = lowerOriginalLocalRotation;
        }
    }

    private void ClampSettings()
    {
        closedAngle = ClampFiniteAngle(closedAngle, 0f);
        upperOpenAngle = ClampFiniteAngle(upperOpenAngle, -20f);
        lowerOpenAngle = ClampFiniteAngle(lowerOpenAngle, 20f);
        voiceThreshold = Mathf.Max(0f, GetFiniteValue(voiceThreshold, 0.02f));
        maxVoiceLevel = Mathf.Max(voiceThreshold + MinimumVoiceRange, GetFiniteValue(maxVoiceLevel, 0.25f));
        smoothingSpeed = Mathf.Max(MinimumSmoothingSpeed, GetFiniteValue(smoothingSpeed, 15f));
    }

    private static float ClampFiniteAngle(float value, float fallback)
    {
        return Mathf.Clamp(GetFiniteValue(value, fallback), -180f, 180f);
    }

    private static float GetFiniteValue(float value, float fallback)
    {
        return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
    }
}
