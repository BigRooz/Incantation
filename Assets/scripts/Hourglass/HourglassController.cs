using UnityEngine;
using UnityEngine.Events;

public class HourglassController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Timer timer;

    [Header("Events")]
    [SerializeField] private UnityEvent onStarted = new UnityEvent();
    [SerializeField] private UnityEvent onWarning = new UnityEvent();
    [SerializeField] private UnityEvent onFinished = new UnityEvent();
    [SerializeField] private UnityEvent onStopped = new UnityEvent();
    [SerializeField] private UnityEvent onReset = new UnityEvent();

    public UnityEvent OnStarted => onStarted;
    public UnityEvent OnWarning => onWarning;
    public UnityEvent OnFinished => onFinished;
    public UnityEvent OnStopped => onStopped;
    public UnityEvent OnReset => onReset;

    private void OnEnable()
    {
        SubscribeToTimer();
    }

    private void OnDisable()
    {
        UnsubscribeFromTimer();
    }

    public void StartHourglass(float duration)
    {
        if (timer == null)
        {
            Debug.LogWarning("HourglassController requires a Timer reference.");
            return;
        }

        timer.StartTimer(duration);
    }

    public void StopHourglass()
    {
        if (timer == null)
            return;

        timer.StopTimer();
    }

    public void ResetHourglass()
    {
        if (timer == null)
            return;

        timer.ResetTimer();
    }

    private void SubscribeToTimer()
    {
        if (timer == null)
            return;

        timer.OnStarted.AddListener(HandleTimerStarted);
        timer.OnWarning.AddListener(HandleTimerWarning);
        timer.OnFinished.AddListener(HandleTimerFinished);
        timer.OnStopped.AddListener(HandleTimerStopped);
        timer.OnReset.AddListener(HandleTimerReset);
    }

    private void UnsubscribeFromTimer()
    {
        if (timer == null)
            return;

        timer.OnStarted.RemoveListener(HandleTimerStarted);
        timer.OnWarning.RemoveListener(HandleTimerWarning);
        timer.OnFinished.RemoveListener(HandleTimerFinished);
        timer.OnStopped.RemoveListener(HandleTimerStopped);
        timer.OnReset.RemoveListener(HandleTimerReset);
    }

    private void HandleTimerStarted()
    {
        Debug.Log("Hourglass Started");
        onStarted.Invoke();
    }

    private void HandleTimerWarning()
    {
        Debug.Log("Hourglass Warning");
        onWarning.Invoke();
    }

    private void HandleTimerFinished()
    {
        Debug.Log("Hourglass Finished");
        onFinished.Invoke();
    }

    private void HandleTimerStopped()
    {
        Debug.Log("Hourglass Stopped");
        onStopped.Invoke();
    }

    private void HandleTimerReset()
    {
        Debug.Log("Hourglass Reset");
        onReset.Invoke();
    }
}
