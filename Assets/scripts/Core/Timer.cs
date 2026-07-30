using Incantation.Networking.Ritual;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Owns the offline countdown and presents server-authoritative timer snapshots in network
/// sessions. Network presentation never computes or commits gameplay expiration.
/// </summary>
public class Timer : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField, Min(1f)] private float duration = 10f;
    [SerializeField] private float warningThreshold = 3f;

    [Header("Events")]
    [SerializeField] private UnityEvent onStarted = new UnityEvent();
    [SerializeField] private UnityEvent onWarning = new UnityEvent();
    [SerializeField] private UnityEvent onFinished = new UnityEvent();
    [SerializeField] private UnityEvent onStopped = new UnityEvent();
    [SerializeField] private UnityEvent onReset = new UnityEvent();

    private bool hasWarned;
    private bool isAuthoritativePresentation;
    private float authoritativeDuration;

    public bool IsRunning { get; private set; }
    public float RemainingTime { get; private set; }
    public float Duration => isAuthoritativePresentation
        ? authoritativeDuration
        : duration;

    public UnityEvent OnStarted => onStarted;
    public UnityEvent OnWarning => onWarning;
    public UnityEvent OnFinished => onFinished;
    public UnityEvent OnStopped => onStopped;
    public UnityEvent OnReset => onReset;

    private void Update()
    {
        if (!IsRunning)
            return;

        if (isAuthoritativePresentation)
            return;

        RemainingTime -= Time.deltaTime;

        if (!hasWarned && RemainingTime <= warningThreshold)
        {
            hasWarned = true;
            onWarning.Invoke();
        }

        if (RemainingTime > 0f)
            return;

        RemainingTime = 0f;
        IsRunning = false;
        onFinished.Invoke();
    }

    public void StartTimer(float requestedDuration)
    {
        isAuthoritativePresentation = false;
        RemainingTime = requestedDuration;
        IsRunning = true;
        hasWarned = false;

        onStarted.Invoke();

        if (requestedDuration <= 0f)
        {
            IsRunning = false;
            onFinished.Invoke();
        }
    }

    public void StopTimer()
    {
        isAuthoritativePresentation = false;
        if (!IsRunning)
            return;

        IsRunning = false;
        onStopped.Invoke();
    }

    public void ResetTimer()
    {
        isAuthoritativePresentation = false;
        IsRunning = false;
        RemainingTime = duration;
        hasWarned = false;
        onReset.Invoke();
    }

    /// <summary>
    /// Applies read-only authoritative state and translates lifecycle transitions for existing
    /// visual and legacy compatibility listeners.
    /// </summary>
    public void ApplyAuthoritativeSnapshot(RitualTimerSnapshot snapshot)
    {
        bool wasRunning = IsRunning;
        bool wasExpired = isAuthoritativePresentation &&
            !IsRunning &&
            RemainingTime <= 0f;

        isAuthoritativePresentation = true;
        authoritativeDuration = Mathf.Max(0f, (float)snapshot.Duration);
        RemainingTime = Mathf.Max(0f, (float)snapshot.RemainingTime);
        IsRunning = snapshot.IsRunning;

        if (snapshot.IsRunning)
        {
            if (!wasRunning)
            {
                hasWarned = false;
                onStarted.Invoke();
            }

            TryRaisePresentationWarning();
            return;
        }

        if (snapshot.IsExpired)
        {
            RemainingTime = 0f;
            if (!wasExpired)
                onFinished.Invoke();

            return;
        }

        if (wasRunning)
            onStopped.Invoke();
    }

    /// <summary>
    /// Updates display-only remaining time from the authority's read-only calculation.
    /// </summary>
    public void ApplyAuthoritativeRemainingTime(double remainingTime)
    {
        if (!isAuthoritativePresentation || !IsRunning)
            return;

        RemainingTime = Mathf.Max(0f, (float)remainingTime);
        TryRaisePresentationWarning();
    }

    private void TryRaisePresentationWarning()
    {
        if (hasWarned || RemainingTime > warningThreshold)
            return;

        hasWarned = true;
        onWarning.Invoke();
    }
}
