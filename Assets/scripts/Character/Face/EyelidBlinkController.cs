using System.Collections;
using UnityEngine;

public sealed class EyelidBlinkController : MonoBehaviour
{
    private const float MinimumDuration = 0.001f;

    [Header("Eyelids")]
    [SerializeField] private Transform leftEyelid;
    [SerializeField] private Transform rightEyelid;

    [Header("Scale")]
    [SerializeField] private float openScaleZ = 0f;
    [SerializeField] private float closedScaleZ = -2.2f;

    [Header("Timing")]
    [SerializeField] private float closeDuration = 0.08f;
    [SerializeField] private float openDuration = 0.12f;
    [SerializeField] private float minimumBlinkInterval = 3f;
    [SerializeField] private float maximumBlinkInterval = 8f;
    [SerializeField] private bool blinkOnEnable;

    private bool isBlinking;
    private bool hasLoggedMissingEyelidWarning;

    private void OnEnable()
    {
        ClampSettings();

        if (leftEyelid == null && rightEyelid == null)
        {
            Debug.LogWarning(
                $"{nameof(EyelidBlinkController)} on '{name}' has no eyelid references and has been disabled.",
                this);
            enabled = false;
            return;
        }

        if ((leftEyelid == null || rightEyelid == null) && !hasLoggedMissingEyelidWarning)
        {
            Debug.LogWarning(
                $"{nameof(EyelidBlinkController)} on '{name}' is missing one eyelid reference. The assigned eyelid will still blink.",
                this);
            hasLoggedMissingEyelidWarning = true;
        }

        SetEyelidScaleZ(openScaleZ);
        StartCoroutine(AutomaticBlinkRoutine());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        isBlinking = false;
        SetEyelidScaleZ(openScaleZ);
    }

    private void OnValidate()
    {
        ClampSettings();
    }

    [ContextMenu("Blink")]
    public void Blink()
    {
        if (!isActiveAndEnabled || isBlinking)
        {
            return;
        }

        if (leftEyelid == null && rightEyelid == null)
        {
            Debug.LogWarning(
                $"{nameof(EyelidBlinkController)} on '{name}' cannot blink because neither eyelid is assigned.",
                this);
            enabled = false;
            return;
        }

        isBlinking = true;
        StartCoroutine(BlinkRoutine());
    }

    private IEnumerator AutomaticBlinkRoutine()
    {
        if (blinkOnEnable)
        {
            Blink();
            yield return new WaitUntil(() => !isBlinking);
        }

        while (enabled)
        {
            float waitDuration = Random.Range(minimumBlinkInterval, maximumBlinkInterval);
            yield return new WaitForSecondsRealtime(waitDuration);

            Blink();
            yield return new WaitUntil(() => !isBlinking);
        }
    }

    private IEnumerator BlinkRoutine()
    {
        SetEyelidScaleZ(openScaleZ);
        yield return AnimateScaleZ(openScaleZ, closedScaleZ, closeDuration);
        yield return AnimateScaleZ(closedScaleZ, openScaleZ, openDuration);
        SetEyelidScaleZ(openScaleZ);
        isBlinking = false;
    }

    private IEnumerator AnimateScaleZ(float startScaleZ, float targetScaleZ, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            SetEyelidScaleZ(Mathf.Lerp(startScaleZ, targetScaleZ, progress));
            yield return null;
        }

        SetEyelidScaleZ(targetScaleZ);
    }

    private void SetEyelidScaleZ(float scaleZ)
    {
        SetScaleZ(leftEyelid, scaleZ);
        SetScaleZ(rightEyelid, scaleZ);
    }

    private static void SetScaleZ(Transform eyelid, float scaleZ)
    {
        if (eyelid == null)
        {
            return;
        }

        Vector3 localScale = eyelid.localScale;
        localScale.z = scaleZ;
        eyelid.localScale = localScale;
    }

    private void ClampSettings()
    {
        closeDuration = Mathf.Max(MinimumDuration, closeDuration);
        openDuration = Mathf.Max(MinimumDuration, openDuration);
        minimumBlinkInterval = Mathf.Max(0f, minimumBlinkInterval);
        maximumBlinkInterval = Mathf.Max(minimumBlinkInterval, maximumBlinkInterval);
    }
}
