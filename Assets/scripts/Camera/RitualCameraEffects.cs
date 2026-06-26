using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class RitualCameraEffects : MonoBehaviour
{
    private const float CompleteShakeMultiplier = 0.75f;
    private const float FailedShakeMultiplier = 1.65f;

    [Header("References")]
    [FormerlySerializedAs("cameraTarget")]
    [SerializeField] private Transform targetTransform;

    [Header("Idle")]
    [SerializeField] private float idleAmplitude = 0.003f;
    [SerializeField] private float idleFrequency = 0.6f;

    [Header("Shake")]
    [SerializeField] private float successShakeStrength = 0.02f;
    [SerializeField] private float errorShakeStrength = 0.05f;
    [SerializeField] private float shakeDuration = 0.18f;

    private readonly List<ShakeInstance> activeShakes = new List<ShakeInstance>();
    private Vector3 baseLocalPosition;
    private bool isRitualActive;
    private float idleTime;

    private void Awake()
    {
        ResolveTargetTransform();
        StoreBaseLocalPosition();
    }

    private void OnEnable()
    {
        ResolveTargetTransform();
        StoreBaseLocalPosition();
    }

    private void OnDisable()
    {
        ClearEffects();
    }

    private void LateUpdate()
    {
        if (targetTransform == null)
            return;

        Vector3 idleOffset = GetIdleOffset();
        Vector3 shakeOffset = GetShakeOffset();

        targetTransform.localPosition = baseLocalPosition + idleOffset + shakeOffset;
    }

    public void BeginRitual()
    {
        isRitualActive = true;
        idleTime = 0f;
    }

    public void EndRitual()
    {
        isRitualActive = false;
    }

    public void HandleCorrectWord()
    {
        AddShake(successShakeStrength);
    }

    public void HandleIncorrectWord()
    {
        AddShake(errorShakeStrength);
    }

    public void HandleIncantationComplete()
    {
        AddShake(successShakeStrength * CompleteShakeMultiplier);
    }

    public void HandleIncantationFailed()
    {
        EndRitual();
        AddShake(errorShakeStrength * FailedShakeMultiplier);
    }

    public void ClearEffects()
    {
        activeShakes.Clear();
        idleTime = 0f;
        isRitualActive = false;

        if (targetTransform != null)
            targetTransform.localPosition = baseLocalPosition;
    }

    private void ResolveTargetTransform()
    {
        if (targetTransform != null)
            return;

        targetTransform = transform;
    }

    private void StoreBaseLocalPosition()
    {
        if (targetTransform == null)
            return;

        baseLocalPosition = targetTransform.localPosition;
    }

    private Vector3 GetIdleOffset()
    {
        if (!isRitualActive)
            return Vector3.zero;

        float safeAmplitude = Mathf.Max(0f, idleAmplitude);
        float safeFrequency = Mathf.Max(0f, idleFrequency);

        if (safeAmplitude <= 0f || safeFrequency <= 0f)
            return Vector3.zero;

        idleTime += Time.deltaTime;

        float idleWave = Mathf.Sin(idleTime * safeFrequency * Mathf.PI * 2f);
        float sideWave = Mathf.Cos(idleTime * safeFrequency * Mathf.PI * 2f * 0.5f);

        return new Vector3(sideWave * safeAmplitude * 0.25f, idleWave * safeAmplitude, 0f);
    }

    private Vector3 GetShakeOffset()
    {
        if (activeShakes.Count == 0)
            return Vector3.zero;

        Vector3 shakeOffset = Vector3.zero;

        for (int i = activeShakes.Count - 1; i >= 0; i--)
        {
            ShakeInstance shake = activeShakes[i];
            shake.Elapsed += Time.deltaTime;

            float duration = Mathf.Max(0.001f, shake.Duration);
            float progress = Mathf.Clamp01(shake.Elapsed / duration);
            float fade = 1f - SmoothStep(progress);
            float sampleTime = shake.Elapsed * 85f + shake.Seed;

            Vector3 sample = new Vector3(
                Mathf.PerlinNoise(sampleTime, shake.Seed) * 2f - 1f,
                Mathf.PerlinNoise(shake.Seed, sampleTime) * 2f - 1f,
                Mathf.PerlinNoise(sampleTime, sampleTime + shake.Seed) * 2f - 1f
            );

            shakeOffset += sample * shake.Strength * fade;

            if (progress >= 1f)
                activeShakes.RemoveAt(i);
            else
                activeShakes[i] = shake;
        }

        return shakeOffset;
    }

    private void AddShake(float strength)
    {
        float safeStrength = Mathf.Max(0f, strength);
        float safeDuration = Mathf.Max(0f, shakeDuration);

        if (safeStrength <= 0f || safeDuration <= 0f)
            return;

        activeShakes.Add(new ShakeInstance
        {
            Strength = safeStrength,
            Duration = safeDuration,
            Seed = Random.Range(0f, 1000f)
        });
    }

    private float SmoothStep(float value)
    {
        float clampedValue = Mathf.Clamp01(value);
        return clampedValue * clampedValue * (3f - 2f * clampedValue);
    }

    private struct ShakeInstance
    {
        public float Strength;
        public float Duration;
        public float Elapsed;
        public float Seed;
    }
}
