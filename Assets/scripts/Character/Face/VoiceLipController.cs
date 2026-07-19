using UnityEngine;

/// <summary>
/// Animates separate upper- and lower-lip transforms from a normalized voice amplitude.
/// Consumes a VoiceAmplitudeProvider supplied in the Inspector and does not own microphone capture.
/// </summary>
public sealed class VoiceLipController : MonoBehaviour
{
    private const float MinimumSmoothingSpeed = 0.01f;

    [Header("Lips")]
    [Tooltip("Upper lip Transform. Only its local X rotation is animated.")]
    [SerializeField] private Transform upperLip;

    [Tooltip("Lower lip Transform. Only its local X rotation is animated.")]
    [SerializeField] private Transform lowerLip;

    [Header("Voice Input")]
    [Tooltip("Provider of normalized voice amplitude. This component only reads CurrentAmplitude.")]
    [SerializeField] private VoiceAmplitudeProvider voiceAmplitudeProvider;

    [Tooltip("When enabled, SetOpenAmount controls mouth openness instead of the voice amplitude provider.")]
    [SerializeField] private bool useManualOpenAmount;

    [Header("Mouth Angles")]
    [Tooltip("Local X rotation, in degrees, used when the mouth is closed.")]
    [SerializeField] private float closedAngle = 0f;

    [Tooltip("Upper lip local X rotation, in degrees, used at maximum openness.")]
    [SerializeField] private float upperOpenAngle = -20f;

    [Tooltip("Lower lip local X rotation, in degrees, used at maximum openness.")]
    [SerializeField] private float lowerOpenAngle = 20f;

    [Header("Response")]
    [Tooltip("Speed used to smooth changes in mouth openness.")]
    [SerializeField] private float smoothingSpeed = 15f;

    private Vector3 upperOriginalLocalEulerAngles;
    private Vector3 lowerOriginalLocalEulerAngles;
    private Quaternion upperOriginalLocalRotation;
    private Quaternion lowerOriginalLocalRotation;
    private float manualOpenAmount;
    private float currentOpenAmount;
    private bool hasCapturedOriginalRotations;
    private bool hasInitialized;
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

        WarnIfVoiceSourceIsMissing();
        ApplyOpenAmount(0f);
        hasInitialized = true;
    }

    private void OnEnable()
    {
        if (!hasInitialized)
        {
            return;
        }

        WarnIfVoiceSourceIsMissing();
        ApplyOpenAmount(0f);
    }

    private void Update()
    {
        float targetOpenAmount = useManualOpenAmount
            ? manualOpenAmount
            : GetAutomaticOpenAmount();

        float interpolation = 1f - Mathf.Exp(-smoothingSpeed * Time.deltaTime);
        currentOpenAmount = Mathf.Lerp(currentOpenAmount, targetOpenAmount, interpolation);
        ApplyOpenAmount(currentOpenAmount);
    }

    private void OnDisable()
    {
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

        manualOpenAmount = Mathf.Clamp01(amount);
        currentOpenAmount = manualOpenAmount;
        ApplyOpenAmount(currentOpenAmount);
    }

    private float GetAutomaticOpenAmount()
    {
        if (voiceAmplitudeProvider == null)
        {
            WarnIfVoiceSourceIsMissing();
            return 0f;
        }

        return Mathf.Clamp01(voiceAmplitudeProvider.CurrentAmplitude);
    }

    private void WarnIfVoiceSourceIsMissing()
    {
        if (useManualOpenAmount || voiceAmplitudeProvider != null || hasLoggedMissingVoiceSourceWarning)
        {
            return;
        }

        Debug.LogWarning(
            $"{nameof(VoiceLipController)} on '{name}' has no {nameof(VoiceAmplitudeProvider)}. The lips will remain closed.",
            this);
        hasLoggedMissingVoiceSourceWarning = true;
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
