using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives assigned fire light flickers into a dim possession state near the end of an active timer.
/// Depends on a Timer for remaining-time authority and FireLightFlicker components for visual output.
/// </summary>
public class HourglassLightPossessionController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Timer timer;

    [Header("Possession")]
    [SerializeField] private List<FireLightFlicker> affectedLights = new List<FireLightFlicker>();
    [SerializeField, Min(0f)] private float possessionStartRemainingSeconds = 8f;
    [SerializeField] private bool usePossessedFlicker = true;
    [SerializeField, Min(0f)] private float possessedFlickerSpeed = 15f;
    [SerializeField, Min(0f)] private float possessedFlickerDepth = 1f;
    [SerializeField, Min(0f)] private float minimumIntensityMultiplier = 0f;
    [SerializeField] private bool affectRange = true;
    [SerializeField, Min(0f)] private float minimumRangeMultiplier = 0f;
    [SerializeField] private AnimationCurve possessionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField, Min(0f)] private float recoveryDurationSeconds = 1.25f;
    [SerializeField] private bool restoreOnStop = true;

    private const float RangeNoiseOffset = 43.7f;

    private Coroutine recoveryCoroutine;
    private float currentIntensityMultiplier = 1f;
    private float currentRangeMultiplier = 1f;
    private float noiseOffset;

    private void Awake()
    {
        noiseOffset = Random.Range(0f, 1000f);
    }

    private void OnEnable()
    {
        SubscribeToTimer();
    }

    private void OnDisable()
    {
        UnsubscribeFromTimer();
        StopRecovery();
        ApplyMultipliers(1f, 1f);
    }

    private void Update()
    {
        if (timer == null || !timer.IsRunning)
            return;

        ApplyPossessionFromRemainingTime(timer.RemainingTime);
    }

    private void OnValidate()
    {
        possessionStartRemainingSeconds = Mathf.Max(0f, possessionStartRemainingSeconds);
        possessedFlickerSpeed = Mathf.Max(0f, possessedFlickerSpeed);
        possessedFlickerDepth = Mathf.Max(0f, possessedFlickerDepth);
        minimumIntensityMultiplier = Mathf.Max(0f, minimumIntensityMultiplier);
        minimumRangeMultiplier = Mathf.Max(0f, minimumRangeMultiplier);
        recoveryDurationSeconds = Mathf.Max(0f, recoveryDurationSeconds);

        if (possessionCurve == null || possessionCurve.length == 0)
            possessionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    }

    private void SubscribeToTimer()
    {
        if (timer == null)
            return;

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
        StopRecovery();
        ApplyMultipliers(1f, 1f);
    }

    private void HandleTimerFinished()
    {
        ApplyPossessionMultipliers(1f);

        if (restoreOnStop)
            StartRecovery();
    }

    private void HandleTimerStopped()
    {
        if (restoreOnStop)
            StartRecovery();
    }

    private void HandleTimerReset()
    {
        if (restoreOnStop)
            StartRecovery();
    }

    private void ApplyPossessionFromRemainingTime(float remainingTime)
    {
        if (possessionStartRemainingSeconds <= 0f)
        {
            if (remainingTime <= 0f)
                ApplyPossessionMultipliers(1f);
            else
                ApplyMultipliers(1f, 1f);

            return;
        }

        if (remainingTime > possessionStartRemainingSeconds)
        {
            ApplyMultipliers(1f, 1f);
            return;
        }

        float dangerAmount = 1f - Mathf.Clamp01(remainingTime / possessionStartRemainingSeconds);
        ApplyPossessionMultipliers(dangerAmount);
    }

    private void ApplyPossessionMultipliers(float dangerAmount)
    {
        float curvedDanger = Mathf.Clamp01(possessionCurve.Evaluate(Mathf.Clamp01(dangerAmount)));
        float possessionPulse = usePossessedFlicker ? CalculatePossessionPulse(curvedDanger) : curvedDanger;
        float intensityMultiplier = Mathf.Lerp(1f, minimumIntensityMultiplier, possessionPulse);
        float rangeMultiplier = affectRange ? Mathf.Lerp(1f, minimumRangeMultiplier, possessionPulse) : 1f;

        ApplyMultipliers(intensityMultiplier, rangeMultiplier);
    }

    private float CalculatePossessionPulse(float dangerAmount)
    {
        float speed = Mathf.Max(0.01f, possessedFlickerSpeed);
        float time = Time.time * speed * Mathf.Lerp(0.75f, 1.75f, dangerAmount);
        float intensityNoise = Mathf.PerlinNoise(noiseOffset, time);
        float rangeNoise = Mathf.PerlinNoise(noiseOffset + RangeNoiseOffset, time * 1.27f);
        float mixedNoise = (intensityNoise * 0.7f) + (rangeNoise * 0.3f);
        float dipAmount = 1f - mixedNoise;
        float depth = Mathf.Clamp01(possessedFlickerDepth * Mathf.Lerp(0.2f, 1f, dangerAmount));

        return Mathf.Clamp01(dangerAmount * Mathf.Lerp(0.35f, 1f, dipAmount * depth));
    }

    private void ApplyMultipliers(float intensityMultiplier, float rangeMultiplier)
    {
        currentIntensityMultiplier = Mathf.Max(0f, intensityMultiplier);
        currentRangeMultiplier = Mathf.Max(0f, rangeMultiplier);

        for (int i = 0; i < affectedLights.Count; i++)
        {
            if (affectedLights[i] == null)
                continue;

            affectedLights[i].SetExternalMultipliers(currentIntensityMultiplier, currentRangeMultiplier);
        }
    }

    private void StartRecovery()
    {
        StopRecovery();

        if (recoveryDurationSeconds <= 0f)
        {
            ApplyMultipliers(1f, 1f);
            return;
        }

        recoveryCoroutine = StartCoroutine(RecoverLights());
    }

    private void StopRecovery()
    {
        if (recoveryCoroutine == null)
            return;

        StopCoroutine(recoveryCoroutine);
        recoveryCoroutine = null;
    }

    private IEnumerator RecoverLights()
    {
        float startIntensityMultiplier = currentIntensityMultiplier;
        float startRangeMultiplier = currentRangeMultiplier;
        float elapsedSeconds = 0f;

        while (elapsedSeconds < recoveryDurationSeconds)
        {
            elapsedSeconds += Time.deltaTime;

            float progress = Mathf.Clamp01(elapsedSeconds / recoveryDurationSeconds);
            float easedProgress = Mathf.SmoothStep(0f, 1f, progress);

            ApplyMultipliers(
                Mathf.Lerp(startIntensityMultiplier, 1f, easedProgress),
                Mathf.Lerp(startRangeMultiplier, 1f, easedProgress));

            yield return null;
        }

        ApplyMultipliers(1f, 1f);
        recoveryCoroutine = null;
    }
}
