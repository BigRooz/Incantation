using System.Collections;
using UnityEngine;

public class BookFeedbackController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private IncantationManager incantationManager;

    [Header("Pulse")]
    [Min(1f)]
    [SerializeField] private float pulseScale = 1.12f;
    [Min(0f)]
    [SerializeField] private float pulseDuration = 0.18f;

    [Header("Shake")]
    [Min(0f)]
    [SerializeField] private float shakeStrength = 0.04f;
    [Min(0f)]
    [SerializeField] private float shakeDuration = 0.2f;

    private Coroutine feedbackCoroutine;
    private Vector3 baseLocalScale;
    private Vector3 previousShakeOffset;

    private void Awake()
    {
        baseLocalScale = transform.localScale;
    }

    private void OnEnable()
    {
        SubscribeToIncantationManager();
    }

    private void OnDisable()
    {
        UnsubscribeFromIncantationManager();
        StopFeedback();
    }

    private void SubscribeToIncantationManager()
    {
        if (incantationManager == null)
            return;

        incantationManager.OnCorrectWord.AddListener(HandleCorrectWord);
        incantationManager.OnIncorrectWord.AddListener(HandleIncorrectWord);
    }

    private void UnsubscribeFromIncantationManager()
    {
        if (incantationManager == null)
            return;

        incantationManager.OnCorrectWord.RemoveListener(HandleCorrectWord);
        incantationManager.OnIncorrectWord.RemoveListener(HandleIncorrectWord);
    }

    private void HandleCorrectWord()
    {
        StopFeedback();
        feedbackCoroutine = StartCoroutine(PulseBook());
    }

    private void HandleIncorrectWord()
    {
        StopFeedback();
        feedbackCoroutine = StartCoroutine(ShakeBook());
    }

    private IEnumerator PulseBook()
    {
        float safeDuration = Mathf.Max(0f, pulseDuration);

        if (safeDuration <= 0f)
        {
            transform.localScale = baseLocalScale;
            feedbackCoroutine = null;
            yield break;
        }

        Vector3 startScale = baseLocalScale;
        Vector3 targetScale = baseLocalScale * Mathf.Max(0f, pulseScale);
        float halfDuration = safeDuration * 0.5f;

        yield return ScaleOverTime(startScale, targetScale, halfDuration);
        yield return ScaleOverTime(targetScale, startScale, halfDuration);

        transform.localScale = baseLocalScale;
        feedbackCoroutine = null;
    }

    private IEnumerator ScaleOverTime(Vector3 startScale, Vector3 targetScale, float duration)
    {
        if (duration <= 0f)
        {
            transform.localScale = targetScale;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);

            transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }

        transform.localScale = targetScale;
    }

    private IEnumerator ShakeBook()
    {
        float safeDuration = Mathf.Max(0f, shakeDuration);
        float safeStrength = Mathf.Max(0f, shakeStrength);

        if (safeDuration <= 0f || safeStrength <= 0f)
        {
            ClearShakeOffset();
            feedbackCoroutine = null;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < safeDuration)
        {
            elapsed += Time.deltaTime;

            Vector3 nextOffset = Random.insideUnitSphere * safeStrength;
            nextOffset.y *= 0.35f;

            transform.localPosition = transform.localPosition - previousShakeOffset + nextOffset;
            previousShakeOffset = nextOffset;

            yield return null;
        }

        ClearShakeOffset();
        feedbackCoroutine = null;
    }

    private void StopFeedback()
    {
        if (feedbackCoroutine != null)
        {
            StopCoroutine(feedbackCoroutine);
            feedbackCoroutine = null;
        }

        transform.localScale = baseLocalScale;
        ClearShakeOffset();
    }

    private void ClearShakeOffset()
    {
        if (previousShakeOffset == Vector3.zero)
            return;

        transform.localPosition -= previousShakeOffset;
        previousShakeOffset = Vector3.zero;
    }
}
