using UnityEngine;
using UnityEngine.Events;

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

    private bool hasWarned;

    public bool IsRunning { get; private set; }
    public float RemainingTime { get; private set; }
    public float Duration => duration;

    public UnityEvent OnStarted => onStarted;
    public UnityEvent OnWarning => onWarning;
    public UnityEvent OnFinished => onFinished;
    public UnityEvent OnStopped => onStopped;

    private void Update()
    {
        if (!IsRunning)
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
        RemainingTime = duration;
        IsRunning = true;
        hasWarned = false;

        onStarted.Invoke();

        if (duration <= 0f)
        {
            IsRunning = false;
            onFinished.Invoke();
        }
    }

    public void StopTimer()
    {
        if (!IsRunning)
            return;

        IsRunning = false;
        onStopped.Invoke();
    }

    public void ResetTimer()
    {
        IsRunning = false;
        RemainingTime = duration;
        hasWarned = false;
    }
}
