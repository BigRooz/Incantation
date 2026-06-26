using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class RitualLightingController : MonoBehaviour
{
    [Header("Timer")]
    [SerializeField] private Timer timer;

    [Header("General Lights")]
    [FormerlySerializedAs("lights")]
    [SerializeField] private List<Light> generalLights = new List<Light>();

    [Header("Candle Lights")]
    [SerializeField] private List<Light> candleLights = new List<Light>();

    [InspectorName("Minimum Intensity")]
    [SerializeField] private float minimumIntensityMultiplier = 0.15f;

    [InspectorName("Candle Minimum Intensity")]
    [SerializeField] private float candleMinimumIntensityMultiplier = 0.45f;

    [SerializeField] private float recoverySpeed = 3f;
    [SerializeField] private AnimationCurve intensityCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    private readonly List<float> originalGeneralIntensities = new List<float>();
    private readonly List<float> originalCandleIntensities = new List<float>();

    private void Awake()
    {
        StoreOriginalIntensities();
    }

    private void Update()
    {
        if (generalLights.Count != originalGeneralIntensities.Count ||
            candleLights.Count != originalCandleIntensities.Count)
        {
            StoreOriginalIntensities();
        }

        if (timer != null && timer.IsRunning)
        {
            ApplyTimerLighting();
            return;
        }

        RestoreOriginalLighting();
    }

    private void StoreOriginalIntensities()
    {
        StoreOriginalIntensities(generalLights, originalGeneralIntensities);
        StoreOriginalIntensities(candleLights, originalCandleIntensities);
    }

    private void StoreOriginalIntensities(List<Light> lightGroup, List<float> originalIntensities)
    {
        originalIntensities.Clear();

        for (int i = 0; i < lightGroup.Count; i++)
        {
            Light ritualLight = lightGroup[i];
            originalIntensities.Add(ritualLight != null ? ritualLight.intensity : 0f);
        }
    }

    private void ApplyTimerLighting()
    {
        float timeRatio = GetTimerTimeRatio();
        float curvedRatio = Mathf.Clamp01(intensityCurve.Evaluate(timeRatio));

        ApplyTimerLighting(generalLights, originalGeneralIntensities, minimumIntensityMultiplier, curvedRatio);
        ApplyTimerLighting(candleLights, originalCandleIntensities, candleMinimumIntensityMultiplier, curvedRatio);
    }

    private void ApplyTimerLighting(
        List<Light> lightGroup,
        List<float> originalIntensities,
        float minimumMultiplier,
        float curvedRatio)
    {
        float clampedMinimumMultiplier = Mathf.Clamp01(minimumMultiplier);
        float intensityMultiplier = Mathf.Lerp(clampedMinimumMultiplier, 1f, curvedRatio);

        for (int i = 0; i < lightGroup.Count; i++)
        {
            Light ritualLight = lightGroup[i];

            if (ritualLight == null)
                continue;

            ritualLight.intensity = originalIntensities[i] * intensityMultiplier;
        }
    }

    private float GetTimerTimeRatio()
    {
        if (timer == null || timer.Duration <= 0f)
            return 0f;

        return Mathf.Clamp01(timer.RemainingTime / timer.Duration);
    }

    private void RestoreOriginalLighting()
    {
        RestoreOriginalLighting(generalLights, originalGeneralIntensities);
        RestoreOriginalLighting(candleLights, originalCandleIntensities);
    }

    private void RestoreOriginalLighting(List<Light> lightGroup, List<float> originalIntensities)
    {
        for (int i = 0; i < lightGroup.Count; i++)
        {
            Light ritualLight = lightGroup[i];

            if (ritualLight == null)
                continue;

            ritualLight.intensity = Mathf.MoveTowards(
                ritualLight.intensity,
                originalIntensities[i],
                recoverySpeed * Time.deltaTime
            );
        }
    }
}
