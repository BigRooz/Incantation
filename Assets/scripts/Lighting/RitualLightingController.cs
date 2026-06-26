using System.Collections.Generic;
using UnityEngine;

public class RitualLightingController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Timer timer;
    [SerializeField] private List<Light> lights = new List<Light>();

    [Header("Settings")]
    [SerializeField] private float minimumIntensityMultiplier = 0.15f;
    [SerializeField] private float recoverySpeed = 3f;
    [SerializeField] private AnimationCurve intensityCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    private readonly List<float> originalIntensities = new List<float>();

    private void Awake()
    {
        StoreOriginalIntensities();
    }

    private void Update()
    {
        if (lights.Count != originalIntensities.Count)
            StoreOriginalIntensities();

        if (timer != null && timer.IsRunning)
        {
            ApplyTimerLighting();
            return;
        }

        RestoreOriginalLighting();
    }

    private void StoreOriginalIntensities()
    {
        originalIntensities.Clear();

        for (int i = 0; i < lights.Count; i++)
        {
            Light ritualLight = lights[i];
            originalIntensities.Add(ritualLight != null ? ritualLight.intensity : 0f);
        }
    }

    private void ApplyTimerLighting()
    {
        float timeRatio = GetTimerTimeRatio();
        float curvedRatio = Mathf.Clamp01(intensityCurve.Evaluate(timeRatio));
        float minimumMultiplier = Mathf.Clamp01(minimumIntensityMultiplier);
        float intensityMultiplier = Mathf.Lerp(minimumMultiplier, 1f, curvedRatio);

        for (int i = 0; i < lights.Count; i++)
        {
            Light ritualLight = lights[i];

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
        for (int i = 0; i < lights.Count; i++)
        {
            Light ritualLight = lights[i];

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
