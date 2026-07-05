using UnityEngine;

/// <summary>
/// Drives optional hourglass sand visuals from Timer state without owning timer gameplay.
/// </summary>
public class HourglassVisualController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Timer timer;
    [SerializeField] private Transform hourglassRoot;
    [SerializeField] private Transform topSand;
    [SerializeField] private Transform bottomSand;

    [Header("Sand")]
    [SerializeField] private AnimationCurve sandCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [SerializeField] private Vector3 topSandFullScale = Vector3.one;
    [SerializeField] private Vector3 bottomSandFullScale = Vector3.one;
    [SerializeField] private bool resetVisualsOnEnable = true;

    private float activeDuration = 1f;
    private bool hasLoggedMissingTimer;

    public Transform HourglassRoot => hourglassRoot;

    private void OnEnable()
    {
        if (timer == null)
        {
            LogMissingTimerOnce();
            enabled = false;
            return;
        }

        activeDuration = GetSafeDuration(timer.RemainingTime);
        SubscribeToTimer();

        if (resetVisualsOnEnable)
        {
            ApplyVisualProgress(0f);
            return;
        }

        if (timer.IsRunning)
            ApplyVisualsFromTimer();
    }

    private void OnDisable()
    {
        UnsubscribeFromTimer();
    }

    private void Update()
    {
        if (timer == null || !timer.IsRunning)
            return;

        ApplyVisualsFromTimer();
    }

    private void OnValidate()
    {
        if (sandCurve == null || sandCurve.length == 0)
            sandCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    }

    private void SubscribeToTimer()
    {
        if (timer == null)
            return;

        timer.OnStarted.AddListener(HandleTimerStarted);
        timer.OnFinished.AddListener(HandleTimerFinished);
        timer.OnReset.AddListener(HandleTimerReset);
    }

    private void UnsubscribeFromTimer()
    {
        if (timer == null)
            return;

        timer.OnStarted.RemoveListener(HandleTimerStarted);
        timer.OnFinished.RemoveListener(HandleTimerFinished);
        timer.OnReset.RemoveListener(HandleTimerReset);
    }

    private void HandleTimerStarted()
    {
        activeDuration = GetSafeDuration(timer.RemainingTime);
        ApplyVisualsFromTimer();
    }

    private void HandleTimerFinished()
    {
        ApplyVisualProgress(1f);
    }

    private void HandleTimerReset()
    {
        activeDuration = GetSafeDuration(0f);
        ApplyVisualProgress(0f);
    }

    private void ApplyVisualsFromTimer()
    {
        float normalizedRemaining = Mathf.Clamp01(timer.RemainingTime / activeDuration);
        float elapsedProgress = 1f - normalizedRemaining;

        ApplyVisualProgress(elapsedProgress);
    }

    private void ApplyVisualProgress(float progress)
    {
        float curvedProgress = Mathf.Clamp01(sandCurve.Evaluate(Mathf.Clamp01(progress)));
        float topAmount = 1f - curvedProgress;
        float bottomAmount = curvedProgress;

        if (topSand != null)
            topSand.localScale = ScaleByAmount(topSandFullScale, topAmount);

        if (bottomSand != null)
            bottomSand.localScale = ScaleByAmount(bottomSandFullScale, bottomAmount);
    }

    private Vector3 ScaleByAmount(Vector3 fullScale, float amount)
    {
        return new Vector3(
            fullScale.x * amount,
            fullScale.y * amount,
            fullScale.z * amount);
    }

    private float GetSafeDuration(float observedRemainingTime)
    {
        return Mathf.Max(Mathf.Max(timer.Duration, observedRemainingTime), 0.0001f);
    }

    private void LogMissingTimerOnce()
    {
        if (hasLoggedMissingTimer)
            return;

        hasLoggedMissingTimer = true;
        Debug.LogWarning($"{nameof(HourglassVisualController)} on '{gameObject.name}' requires a {nameof(Timer)} reference. Assign the scene Timer in the Inspector.", this);
    }
}
