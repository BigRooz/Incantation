using System.Collections;
using UnityEngine;

/// <summary>
/// Plays warning audio from Timer state without owning timer or ritual gameplay.
/// </summary>
public class HourglassWarningAudio : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Timer timer;
    [SerializeField] private AudioSource audioSource;

    [Header("Audio")]
    [SerializeField] private AudioClip warningClip;

    [Header("Warning")]
    [SerializeField] private float warningThreshold = 10f;
    [SerializeField] private bool playEverySecond = true;
    [SerializeField] private bool resetOnTimerRestart = true;

    [Header("Fade Out")]
    [SerializeField] private float fadeOutDuration = 0.35f;
    [SerializeField] private bool fadeOutOnTimerStop = true;

    private const int NoSecondPlayed = -1;

    private int lastPlayedSecond = NoSecondPlayed;
    private float configuredAudioSourceVolume = 1f;
    private Coroutine fadeRoutine;
    private bool hasPlayedSingleWarning;
    private bool isWarningAudioActive;
    private bool wasTimerRunning;
    private bool hasLoggedMissingTimer;
    private bool hasLoggedMissingAudioSource;

    private void Awake()
    {
        CacheConfiguredAudioSourceVolume();
    }

    private void OnEnable()
    {
        if (!ValidateRequiredReferences())
        {
            enabled = false;
            return;
        }

        CacheConfiguredAudioSourceVolume();
        RestoreConfiguredAudioSourceVolume();
        ResetWarningState();
        wasTimerRunning = timer.IsRunning;
        SubscribeToTimer();
    }

    private void OnDisable()
    {
        UnsubscribeFromTimer();
        CancelFadeRoutine();

        if (audioSource != null)
        {
            audioSource.Stop();
            RestoreConfiguredAudioSourceVolume();
        }
    }

    private void Update()
    {
        if (!timer.IsRunning)
        {
            if (wasTimerRunning)
            {
                ResetWarningState();
                wasTimerRunning = false;
            }

            return;
        }

        if (!wasTimerRunning)
        {
            if (resetOnTimerRestart)
                ResetWarningState();

            wasTimerRunning = true;
        }

        float remainingTime = timer.RemainingTime;

        if (remainingTime <= 0f || remainingTime > warningThreshold)
            return;

        if (playEverySecond)
        {
            PlayWarningForCurrentSecond(remainingTime);
            return;
        }

        PlaySingleWarning();
    }

    private void OnValidate()
    {
        warningThreshold = Mathf.Max(0f, warningThreshold);
        fadeOutDuration = Mathf.Max(0f, fadeOutDuration);
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
        StopWarningAudioImmediately();
        RestoreConfiguredAudioSourceVolume();
        ResetWarningState();
        wasTimerRunning = true;
    }

    private void HandleTimerFinished()
    {
        StopWarningAudioCleanly();
        ResetWarningState();
        wasTimerRunning = false;
    }

    private void HandleTimerStopped()
    {
        StopWarningAudioCleanly();
        ResetWarningState();
        wasTimerRunning = false;
    }

    private void HandleTimerReset()
    {
        StopWarningAudioCleanly();
        ResetWarningState();
        wasTimerRunning = false;
    }

    private void PlayWarningForCurrentSecond(float remainingTime)
    {
        int currentSecond = Mathf.CeilToInt(remainingTime);

        if (currentSecond <= 0 || currentSecond == lastPlayedSecond)
            return;

        lastPlayedSecond = currentSecond;
        PlayWarningClip();
    }

    private void PlaySingleWarning()
    {
        if (hasPlayedSingleWarning)
            return;

        hasPlayedSingleWarning = true;
        PlayWarningClip();
    }

    private void PlayWarningClip()
    {
        if (warningClip == null)
            return;

        CancelFadeRoutine();
        RestoreConfiguredAudioSourceVolume();
        isWarningAudioActive = true;
        audioSource.PlayOneShot(warningClip);
    }

    private void ResetWarningState()
    {
        lastPlayedSecond = NoSecondPlayed;
        hasPlayedSingleWarning = false;
        isWarningAudioActive = false;
    }

    private void StopWarningAudioCleanly()
    {
        if (!isWarningAudioActive && !audioSource.isPlaying)
            return;

        CancelFadeRoutine();

        if (!fadeOutOnTimerStop || fadeOutDuration <= 0f)
        {
            StopWarningAudioImmediately();
            return;
        }

        fadeRoutine = StartCoroutine(FadeOutAndStop());
    }

    private void StopWarningAudioImmediately()
    {
        CancelFadeRoutine();

        if (audioSource != null)
            audioSource.Stop();

        isWarningAudioActive = false;
    }

    private IEnumerator FadeOutAndStop()
    {
        float startVolume = audioSource.volume;
        float elapsedTime = 0f;

        while (elapsedTime < fadeOutDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / fadeOutDuration);
            audioSource.volume = Mathf.Lerp(startVolume, 0f, progress);

            yield return null;
        }

        audioSource.Stop();
        RestoreConfiguredAudioSourceVolume();
        isWarningAudioActive = false;
        fadeRoutine = null;
    }

    private void CancelFadeRoutine()
    {
        if (fadeRoutine == null)
            return;

        StopCoroutine(fadeRoutine);
        fadeRoutine = null;
    }

    private void CacheConfiguredAudioSourceVolume()
    {
        if (audioSource == null)
            return;

        configuredAudioSourceVolume = audioSource.volume;
    }

    private void RestoreConfiguredAudioSourceVolume()
    {
        if (audioSource == null)
            return;

        audioSource.volume = configuredAudioSourceVolume;
    }

    private bool ValidateRequiredReferences()
    {
        bool hasRequiredReferences = true;

        if (timer == null)
        {
            LogMissingTimerOnce();
            hasRequiredReferences = false;
        }

        if (audioSource == null)
        {
            LogMissingAudioSourceOnce();
            hasRequiredReferences = false;
        }

        return hasRequiredReferences;
    }

    private void LogMissingTimerOnce()
    {
        if (hasLoggedMissingTimer)
            return;

        hasLoggedMissingTimer = true;
        Debug.LogWarning($"{nameof(HourglassWarningAudio)} on '{gameObject.name}' requires a {nameof(Timer)} reference. Assign the scene Timer in the Inspector.", this);
    }

    private void LogMissingAudioSourceOnce()
    {
        if (hasLoggedMissingAudioSource)
            return;

        hasLoggedMissingAudioSource = true;
        Debug.LogWarning($"{nameof(HourglassWarningAudio)} on '{gameObject.name}' requires an {nameof(AudioSource)} reference. Assign a warning AudioSource in the Inspector.", this);
    }
}
