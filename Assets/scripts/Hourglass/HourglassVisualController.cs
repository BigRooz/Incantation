using UnityEngine;

/// <summary>
/// Drives optional hourglass sand visuals from Timer state without owning timer gameplay.
/// </summary>
public class HourglassVisualController : MonoBehaviour
{
    private const float MinimumDuration = 0.0001f;

    [Header("References")]
    [SerializeField] private Timer timer;
    [SerializeField] private Transform hourglassRoot;
    [SerializeField] private Transform topSand;
    [SerializeField] private Transform bottomSand;
    [SerializeField] private Transform fallingSandStream;

    [Header("Sand")]
    [SerializeField] private AnimationCurve sandCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [SerializeField, Range(0.001f, 0.1f)] private float minimumSandHeightFraction = 0.01f;
    [SerializeField] private bool resetVisualsOnEnable = true;

    [Header("Top Sand Late Narrowing")]
    [SerializeField, Range(0.01f, 1f)]
    [Tooltip("Remaining sand fraction where horizontal narrowing begins.")]
    private float topHorizontalShrinkStart = 0.20f;
    [SerializeField, Range(0.01f, 1f)]
    [Tooltip("Final X/Y fraction of the authored TopSand size.")]
    private float topMinimumHorizontalScale = 0.35f;

    private float activeDuration = 1f;
    private Vector3 topSandAuthoredPosition;
    private Vector3 topSandAuthoredScale;
    private float topSandAuthoredHeight;
    private Vector3 bottomSandAuthoredPosition;
    private Vector3 bottomSandAuthoredScale;
    private float bottomSandAuthoredHeight;
    private bool hasCachedAuthoredState;
    private bool hasLoggedMissingTimer;

    public Transform HourglassRoot => hourglassRoot;

    private void Awake()
    {
        CacheAuthoredSandState();
        SetStreamVisible(false);
    }

    private void OnEnable()
    {
        if (timer == null)
        {
            LogMissingTimerOnce();
            enabled = false;
            return;
        }

        CacheAuthoredSandState();
        activeDuration = GetSafeDuration(timer.Duration);
        SubscribeToTimer();

        if (timer.IsExpired)
        {
            ApplyVisualProgress(1f);
            SetStreamVisible(false);
            return;
        }

        if (timer.IsRunning)
        {
            ApplyVisualsFromTimer();
            return;
        }

        if (resetVisualsOnEnable)
            ApplyReadyState();
        else
            SetStreamVisible(false);
    }

    private void OnDisable()
    {
        UnsubscribeFromTimer();
        SetStreamVisible(false);
    }

    private void Update()
    {
        if (timer == null)
            return;

        if (timer.IsRunning)
        {
            ApplyVisualsFromTimer();
            return;
        }

        if (timer.IsExpired)
        {
            ApplyVisualProgress(1f);
            SetStreamVisible(false);
            return;
        }

        ApplyReadyState();
    }

    private void OnValidate()
    {
        if (sandCurve == null || sandCurve.length == 0)
            sandCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        minimumSandHeightFraction = Mathf.Clamp(
            minimumSandHeightFraction,
            0.001f,
            0.1f);
        topHorizontalShrinkStart = Mathf.Clamp(topHorizontalShrinkStart, 0.01f, 1f);
        topMinimumHorizontalScale = Mathf.Clamp(topMinimumHorizontalScale, 0.01f, 1f);
    }

    private void SubscribeToTimer()
    {
        timer.OnStarted.AddListener(HandleTimerStarted);
        timer.OnFinished.AddListener(HandleTimerFinished);
        timer.OnStopped.AddListener(HandleTimerStopped);
        timer.OnReset.AddListener(HandleTimerReset);
    }

    private void UnsubscribeFromTimer()
    {
        if (timer == null)
            return;

        timer.OnStarted.RemoveListener(HandleTimerStarted);
        timer.OnFinished.RemoveListener(HandleTimerFinished);
        timer.OnStopped.RemoveListener(HandleTimerStopped);
        timer.OnReset.RemoveListener(HandleTimerReset);
    }

    private void HandleTimerStarted()
    {
        activeDuration = GetSafeDuration(timer.Duration);
        ApplyVisualsFromTimer();
    }

    private void HandleTimerFinished()
    {
        ApplyVisualProgress(1f);
        SetStreamVisible(false);
    }

    private void HandleTimerStopped()
    {
        ApplyReadyState();
    }

    private void HandleTimerReset()
    {
        activeDuration = GetSafeDuration(0f);
        ApplyReadyState();
    }

    private void ApplyVisualsFromTimer()
    {
        float normalizedRemaining = Mathf.Clamp01(timer.RemainingTime / activeDuration);
        float elapsedProgress = 1f - normalizedRemaining;

        ApplyVisualProgress(elapsedProgress);
        SetStreamVisible(timer.IsRunning && timer.RemainingTime > 0f);
    }

    private void ApplyReadyState()
    {
        ApplyVisualProgress(0f);
        SetStreamVisible(false);
    }

    private void ApplyVisualProgress(float progress)
    {
        if (!hasCachedAuthoredState)
            CacheAuthoredSandState();

        float clampedProgress = Mathf.Clamp01(progress);
        float normalizedRemaining = 1f - clampedProgress;
        float curvedProgress = Mathf.Clamp01(sandCurve.Evaluate(clampedProgress));
        float topAmount = 1f - curvedProgress;
        float bottomAmount = curvedProgress;

        ApplySandHeight(
            topSand,
            topSandAuthoredPosition,
            topSandAuthoredScale,
            topSandAuthoredHeight,
            topAmount,
            1f,
            CalculateTopHorizontalFactor(normalizedRemaining));
        ApplySandHeight(
            bottomSand,
            bottomSandAuthoredPosition,
            bottomSandAuthoredScale,
            bottomSandAuthoredHeight,
            bottomAmount,
            -1f,
            1f);
    }

    private float CalculateTopHorizontalFactor(float remainingAmount)
    {
        if (remainingAmount >= topHorizontalShrinkStart)
            return 1f;

        float latePhase = Mathf.Clamp01(
            remainingAmount / Mathf.Max(topHorizontalShrinkStart, MinimumDuration));
        return Mathf.Lerp(topMinimumHorizontalScale, 1f, latePhase);
    }

    private void ApplySandHeight(
        Transform sand,
        Vector3 authoredPosition,
        Vector3 authoredScale,
        float authoredHeight,
        float amount,
        float anchoredEdgeDirection,
        float horizontalScaleFactor)
    {
        if (sand == null)
            return;

        float heightFraction = Mathf.Lerp(
            minimumSandHeightFraction,
            1f,
            Mathf.Clamp01(amount));
        Vector3 scale = authoredScale;
        scale.x = authoredScale.x * horizontalScaleFactor;
        scale.y = authoredScale.y * horizontalScaleFactor;
        scale.z = authoredScale.z * heightFraction;
        sand.localScale = scale;

        float lostHeight = authoredHeight * (1f - heightFraction);
        Vector3 localHeightDirection = sand.localRotation * Vector3.forward;
        sand.localPosition = authoredPosition +
            localHeightDirection * (anchoredEdgeDirection * lostHeight * 0.5f);
    }

    private void CacheAuthoredSandState()
    {
        if (hasCachedAuthoredState)
            return;

        CacheSandState(
            topSand,
            out topSandAuthoredPosition,
            out topSandAuthoredScale,
            out topSandAuthoredHeight);
        CacheSandState(
            bottomSand,
            out bottomSandAuthoredPosition,
            out bottomSandAuthoredScale,
            out bottomSandAuthoredHeight);
        hasCachedAuthoredState = true;
    }

    private static void CacheSandState(
        Transform sand,
        out Vector3 authoredPosition,
        out Vector3 authoredScale,
        out float authoredHeight)
    {
        if (sand == null)
        {
            authoredPosition = Vector3.zero;
            authoredScale = Vector3.one;
            authoredHeight = 0f;
            return;
        }

        authoredPosition = sand.localPosition;
        authoredScale = sand.localScale;
        MeshFilter meshFilter = sand.GetComponent<MeshFilter>();
        authoredHeight = meshFilter != null && meshFilter.sharedMesh != null
            ? meshFilter.sharedMesh.bounds.size.z * Mathf.Abs(authoredScale.z)
            : 0f;
    }

    private void SetStreamVisible(bool isVisible)
    {
        if (fallingSandStream == null ||
            fallingSandStream.gameObject.activeSelf == isVisible)
        {
            return;
        }

        fallingSandStream.gameObject.SetActive(isVisible);
    }

    private float GetSafeDuration(float observedRemainingTime)
    {
        return Mathf.Max(Mathf.Max(timer.Duration, observedRemainingTime), MinimumDuration);
    }

    private void LogMissingTimerOnce()
    {
        if (hasLoggedMissingTimer)
            return;

        hasLoggedMissingTimer = true;
        Debug.LogWarning(
            $"{nameof(HourglassVisualController)} on '{gameObject.name}' requires a {nameof(Timer)} reference. Assign the scene Timer in the Inspector.",
            this);
    }
}
