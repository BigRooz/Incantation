using System.Collections;
using UnityEngine;

public class BookFeedbackController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private IncantationManager incantationManager;
    [SerializeField] private BookController bookController;
    [SerializeField] private BookMenuReturnInteractable bookHighlight;

    [Header("Local Listening Glow")]
    [Min(0f)]
    [SerializeField] private float listeningGlowRiseSpeed = 10f;

    [Header("Pulse")]
    [Min(1f)]
    [SerializeField] private float ritualAcceptedPulseScale = 1.18f;
    [Min(0f)]
    [SerializeField] private float ritualAcceptedPulseDuration = 0.3f;

    private Coroutine feedbackCoroutine;
    private Vector3 baseLocalScale;
    private float listeningGlow;
    private float targetListeningGlow;

    private void Awake()
    {
        baseLocalScale = transform.localScale;
        if (bookController == null)
            bookController = GetComponent<BookController>();
        ResolveBookHighlight();
    }

    private void OnEnable()
    {
        SubscribeToIncantationManager();
    }

    private void Update()
    {
        if (listeningGlow >= targetListeningGlow)
            return;

        listeningGlow = Mathf.MoveTowards(
            listeningGlow,
            targetListeningGlow,
            Mathf.Max(0f, listeningGlowRiseSpeed) * Time.unscaledDeltaTime);
        ApplyListeningGlowRequest();
    }

    private void OnDisable()
    {
        UnsubscribeFromIncantationManager();
        SetListeningGlow(0f);
        StopFeedback();
    }

    public void SetListeningGlow(float glow)
    {
        targetListeningGlow = Mathf.Clamp01(glow);
        if (targetListeningGlow >= listeningGlow)
            return;

        listeningGlow = targetListeningGlow;
        ApplyListeningGlowRequest();
    }

    private void SubscribeToIncantationManager()
    {
        if (incantationManager == null)
        {
            SubscribeToBookController();
            return;
        }

        SubscribeToBookController();
    }

    private void UnsubscribeFromIncantationManager()
    {
        if (bookController != null)
            bookController.OnRitualAccepted -= HandleRitualAccepted;
    }

    private void SubscribeToBookController()
    {
        if (bookController == null)
            return;

        bookController.OnRitualAccepted -= HandleRitualAccepted;
        bookController.OnRitualAccepted += HandleRitualAccepted;
    }

    private void HandleRitualAccepted()
    {
        StopFeedback();
        feedbackCoroutine = StartCoroutine(PulseBook(
            ritualAcceptedPulseScale,
            ritualAcceptedPulseDuration));
    }

    private IEnumerator PulseBook(float scale, float duration)
    {
        float safeDuration = Mathf.Max(0f, duration);

        if (safeDuration <= 0f)
        {
            transform.localScale = baseLocalScale;
            feedbackCoroutine = null;
            yield break;
        }

        Vector3 startScale = baseLocalScale;
        Vector3 targetScale = baseLocalScale * Mathf.Max(0f, scale);
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

    private void StopFeedback()
    {
        if (feedbackCoroutine != null)
        {
            StopCoroutine(feedbackCoroutine);
            feedbackCoroutine = null;
        }

        transform.localScale = baseLocalScale;
    }

    private void ResolveBookHighlight()
    {
        if (bookHighlight == null)
            bookHighlight = GetComponent<BookMenuReturnInteractable>();
    }

    private void ApplyListeningGlowRequest()
    {
        ResolveBookHighlight();
        bookHighlight?.SetVoiceListeningIntensity(listeningGlow);
    }
}
