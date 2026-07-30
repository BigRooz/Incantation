using Incantation.Networking;
using Incantation.Networking.Ritual;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Presents ritual time through the legacy Timer. Offline it retains the local countdown;
/// during FishNet sessions it consumes NetworkRitualAuthority snapshots and forwards stop
/// requests without owning timer gameplay.
/// </summary>
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

    private NetworkRitualAuthority ritualAuthority;
    private NetworkRitualAuthority subscribedRitualAuthority;

    public UnityEvent OnStarted => onStarted;
    public UnityEvent OnWarning => onWarning;
    public UnityEvent OnFinished => onFinished;
    public UnityEvent OnStopped => onStopped;
    public UnityEvent OnReset => onReset;

    private void OnEnable()
    {
        SubscribeToTimer();
        TrySubscribeToRitualAuthority();
    }

    private void OnDisable()
    {
        UnsubscribeFromRitualAuthority();
        UnsubscribeFromTimer();
    }

    private void Update()
    {
        if (!TrySubscribeToRitualAuthority() ||
            subscribedRitualAuthority == null ||
            !subscribedRitualAuthority.IsNetworkSessionActive ||
            timer == null)
        {
            return;
        }

        timer.ApplyAuthoritativeRemainingTime(subscribedRitualAuthority.RemainingTime);
    }

    public void StartHourglass(float duration)
    {
        if (timer == null)
        {
            Debug.LogWarning("HourglassController requires a Timer reference.");
            return;
        }

        if (TryUseAuthoritativeTimer())
        {
            timer.ApplyAuthoritativeSnapshot(ritualAuthority.CurrentTimerSnapshot);
            return;
        }

        timer.StartTimer(duration);
    }

    public void StopHourglass()
    {
        if (timer == null)
            return;

        if (TryUseAuthoritativeTimer())
        {
            if (ritualAuthority.IsServerInitialized)
                ritualAuthority.TryStopTimerForCurrentTurn();

            return;
        }

        timer.StopTimer();
    }

    public void ResetHourglass()
    {
        if (timer == null)
            return;

        if (TryUseAuthoritativeTimer())
        {
            timer.ApplyAuthoritativeSnapshot(ritualAuthority.CurrentTimerSnapshot);
            return;
        }

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

    private bool TryUseAuthoritativeTimer()
    {
        TrySubscribeToRitualAuthority();
        return ritualAuthority != null && ritualAuthority.IsNetworkSessionActive;
    }

    private bool TrySubscribeToRitualAuthority()
    {
        NetworkRitualAuthority resolvedAuthority = ResolveRitualAuthority();
        if (resolvedAuthority == null)
            return false;

        if (subscribedRitualAuthority == resolvedAuthority)
            return true;

        UnsubscribeFromRitualAuthority();
        subscribedRitualAuthority = resolvedAuthority;
        subscribedRitualAuthority.TimerSnapshotChanged +=
            HandleAuthoritativeTimerSnapshotChanged;

        if (subscribedRitualAuthority.IsNetworkSessionActive && timer != null)
        {
            timer.ApplyAuthoritativeSnapshot(
                subscribedRitualAuthority.CurrentTimerSnapshot);
        }

        return true;
    }

    private void UnsubscribeFromRitualAuthority()
    {
        if (subscribedRitualAuthority == null)
            return;

        subscribedRitualAuthority.TimerSnapshotChanged -=
            HandleAuthoritativeTimerSnapshotChanged;
        subscribedRitualAuthority = null;
    }

    private NetworkRitualAuthority ResolveRitualAuthority()
    {
        if (ritualAuthority == null)
            ritualAuthority = NetworkRitualAuthority.Instance;

        if (ritualAuthority == null)
        {
            ritualAuthority = FindFirstObjectByType<NetworkRitualAuthority>(
                FindObjectsInactive.Include);
        }

        return ritualAuthority;
    }

    private void HandleAuthoritativeTimerSnapshotChanged(
        RitualTimerSnapshot snapshot)
    {
        if (timer != null)
            timer.ApplyAuthoritativeSnapshot(snapshot);
    }
}
