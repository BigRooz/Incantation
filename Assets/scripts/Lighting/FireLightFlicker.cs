using UnityEngine;

public class FireLightFlicker : MonoBehaviour
{
    [SerializeField] private Light targetLight;
    [SerializeField] private float baseIntensity = 1.5f;
    [SerializeField] private float intensityVariation = 0.35f;
    [SerializeField] private float baseRange = 4f;
    [SerializeField] private float rangeVariation = 0.4f;
    [SerializeField] private float flickerSpeed = 8f;
    [SerializeField] private bool randomizeOffset = true;

    private float noiseOffset;
    private float rangeNoiseOffset;
    private float originalIntensity;
    private float originalRange;
    private bool hasStoredOriginalValues;

    private void Awake()
    {
        ResolveTargetLight();
        SetNoiseOffsets();
    }

    private void OnEnable()
    {
        ResolveTargetLight();
        StoreOriginalValues();
    }

    private void Update()
    {
        if (targetLight == null)
        {
            ResolveTargetLight();
            StoreOriginalValues();
        }

        if (targetLight == null)
            return;

        float time = Time.time * Mathf.Max(0f, flickerSpeed);
        float intensityNoise = Mathf.PerlinNoise(noiseOffset, time);
        float rangeNoise = Mathf.PerlinNoise(rangeNoiseOffset, time * 0.75f);

        targetLight.intensity = Mathf.Max(0f, baseIntensity + ((intensityNoise * 2f - 1f) * intensityVariation));
        targetLight.range = Mathf.Max(0f, baseRange + ((rangeNoise * 2f - 1f) * rangeVariation));
    }

    private void OnDisable()
    {
        if (targetLight == null || !hasStoredOriginalValues)
            return;

        targetLight.intensity = originalIntensity;
        targetLight.range = originalRange;
    }

    private void OnValidate()
    {
        baseIntensity = Mathf.Max(0f, baseIntensity);
        intensityVariation = Mathf.Max(0f, intensityVariation);
        baseRange = Mathf.Max(0f, baseRange);
        rangeVariation = Mathf.Max(0f, rangeVariation);
        flickerSpeed = Mathf.Max(0f, flickerSpeed);

        ResolveTargetLight();
    }

    private void ResolveTargetLight()
    {
        if (targetLight != null)
            return;

        targetLight = GetComponent<Light>();
    }

    private void SetNoiseOffsets()
    {
        noiseOffset = randomizeOffset ? Random.Range(0f, 1000f) : 0f;
        rangeNoiseOffset = noiseOffset + 31.7f;
    }

    private void StoreOriginalValues()
    {
        if (targetLight == null || hasStoredOriginalValues)
            return;

        originalIntensity = targetLight.intensity;
        originalRange = targetLight.range;
        hasStoredOriginalValues = true;
    }
}
