using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Visual-only full-screen death vision overlay used to hide absorption clipping.
/// Timing events let the absorption bridge delay the actual player pull-in.
/// </summary>
public class DeathVisionVignetteController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CanvasGroup vignetteCanvasGroup;
    [SerializeField] private RectTransform vignetteRoot;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float initialDelayAfterGrab = 0.0f;
    [SerializeField, Min(0f)] private float absorptionDelayAfterGrab = 0.25f;
    [SerializeField, Min(0f)] private float fullBlackDelayAfterGrab = 0.85f;
    [SerializeField, Min(0f)] private float totalDuration = 1.0f;

    [Header("Animation")]
    [SerializeField] private AnimationCurve opacityCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 0.82f, 1f, 1f);
    [SerializeField, Min(1f)] private float jumpPulseScale = 1.08f;
    [SerializeField, Min(0f)] private float jumpPulseDuration = 0.045f;
    [SerializeField] private bool hideOnAwake = true;

    [Header("Events")]
    [SerializeField] private UnityEvent onVignetteStarted;
    [SerializeField] private UnityEvent onAbsorptionMoment;
    [SerializeField] private UnityEvent onFullBlack;
    [SerializeField] private UnityEvent onVignetteFinished;

    private const int StageCount = 5;

    public event UnityAction AbsorptionMomentReached;

    private Coroutine vignetteRoutine;
    private Vector3 baseScale = Vector3.one;
    private bool hasLoggedMissingReferences;

    private void Awake()
    {
        if (vignetteRoot != null)
            baseScale = vignetteRoot.localScale;

        if (hideOnAwake)
            HideImmediate();
    }

    public void Play()
    {
        if (vignetteRoutine != null)
            return;

        if (!HasRequiredReferences())
            return;

        vignetteRoutine = StartCoroutine(RunVignette());
    }

    public void ResetVignette()
    {
        StopVignetteRoutine();
        ApplyFrame(0f);
        SetVisible(false);
    }

    public void HideImmediate()
    {
        StopVignetteRoutine();
        ApplyFrame(0f);
        SetVisible(false);
    }

    public void ShowFullBlackImmediate()
    {
        StopVignetteRoutine();
        SetVisible(true);
        ApplyFrame(1f);
    }

    private IEnumerator RunVignette()
    {
        SetVisible(true);
        ApplyFrame(0f);
        onVignetteStarted?.Invoke();

        bool absorptionInvoked = false;
        bool fullBlackInvoked = false;
        float elapsed = 0f;
        float safeInitialDelay = Mathf.Max(0f, initialDelayAfterGrab);
        float safeTotalDuration = Mathf.Max(safeInitialDelay, totalDuration);
        float safeAbsorptionDelay = Mathf.Max(0f, absorptionDelayAfterGrab);
        float safeFullBlackDelay = Mathf.Max(0f, fullBlackDelayAfterGrab);

        while (elapsed < safeTotalDuration)
        {
            elapsed += Time.deltaTime;

            if (!absorptionInvoked && elapsed >= safeAbsorptionDelay)
            {
                absorptionInvoked = true;
                InvokeAbsorptionMoment();
            }

            if (!fullBlackInvoked && elapsed >= safeFullBlackDelay)
            {
                fullBlackInvoked = true;
                onFullBlack?.Invoke();
            }

            float animationElapsed = Mathf.Max(0f, elapsed - safeInitialDelay);
            float animationDuration = Mathf.Max(0.0001f, safeTotalDuration - safeInitialDelay);
            ApplyFrame(Mathf.Clamp01(animationElapsed / animationDuration));
            yield return null;
        }

        if (!absorptionInvoked)
            InvokeAbsorptionMoment();

        if (!fullBlackInvoked)
            onFullBlack?.Invoke();

        ApplyFrame(1f);
        vignetteRoutine = null;
        onVignetteFinished?.Invoke();
    }

    private void ApplyFrame(float normalizedTime)
    {
        float stagedTime = GetStagedTime(normalizedTime);
        float opacityAmount = EvaluateCurve(opacityCurve, stagedTime);
        float scaleAmount = EvaluateCurve(scaleCurve, stagedTime);
        float pulseAmount = GetPulseAmount(normalizedTime);

        if (vignetteCanvasGroup != null)
            vignetteCanvasGroup.alpha = Mathf.Clamp01(opacityAmount);

        if (vignetteRoot != null)
            vignetteRoot.localScale = baseScale * scaleAmount * pulseAmount;
    }

    private float GetStagedTime(float normalizedTime)
    {
        if (normalizedTime >= 1f)
            return 1f;

        float clampedTime = Mathf.Clamp01(normalizedTime);
        return Mathf.Clamp01(Mathf.Ceil(clampedTime * StageCount) / StageCount);
    }

    private float GetPulseAmount(float normalizedTime)
    {
        if (jumpPulseDuration <= 0f || normalizedTime <= 0f || normalizedTime >= 1f)
            return 1f;

        float stagePosition = Mathf.Clamp01(normalizedTime) * StageCount;
        float distanceFromJump = stagePosition - Mathf.Floor(stagePosition);
        float pulseWindow = Mathf.Clamp01(jumpPulseDuration / Mathf.Max(0.0001f, totalDuration)) * StageCount;

        if (distanceFromJump > pulseWindow)
            return 1f;

        float pulseTime = 1f - Mathf.Clamp01(distanceFromJump / Mathf.Max(0.0001f, pulseWindow));
        return Mathf.Lerp(1f, jumpPulseScale, pulseTime);
    }

    private float EvaluateCurve(AnimationCurve curve, float normalizedTime)
    {
        if (curve == null || curve.length == 0)
            return normalizedTime;

        return curve.Evaluate(normalizedTime);
    }

    private void InvokeAbsorptionMoment()
    {
        onAbsorptionMoment?.Invoke();
        AbsorptionMomentReached?.Invoke();
    }

    private void SetVisible(bool visible)
    {
        if (vignetteCanvasGroup == null)
            return;

        vignetteCanvasGroup.blocksRaycasts = visible;
        vignetteCanvasGroup.interactable = false;
    }

    private void StopVignetteRoutine()
    {
        if (vignetteRoutine == null)
            return;

        StopCoroutine(vignetteRoutine);
        vignetteRoutine = null;
    }

    private bool HasRequiredReferences()
    {
        if (vignetteCanvasGroup != null && vignetteRoot != null)
            return true;

        if (!hasLoggedMissingReferences)
        {
            Debug.LogWarning($"{nameof(DeathVisionVignetteController)} on '{gameObject.name}' needs both vignetteCanvasGroup and vignetteRoot assigned before it can play.", this);
            hasLoggedMissingReferences = true;
        }

        return false;
    }
}
