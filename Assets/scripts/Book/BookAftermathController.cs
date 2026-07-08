using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

/// <summary>
/// Visual/audio-only punctuation after the book consumes a player.
/// Does not decide failure, elimination, seating, turn order, or ritual state.
/// </summary>
public class BookAftermathController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AudioSource audioSource;
    [FormerlySerializedAs("burpClip")]
    [SerializeField] private AudioClip[] burpClips;
    [SerializeField] private ParticleSystem smokeParticles;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float burpDelay = 0.20f;

    [Header("Events")]
    [SerializeField] private UnityEvent onAftermathStarted;
    [SerializeField] private UnityEvent onBurpPlayed;
    [SerializeField] private UnityEvent onAftermathFinished;

    private Coroutine aftermathRoutine;

    public void PlayAftermath()
    {
        if (aftermathRoutine != null)
            return;

        aftermathRoutine = StartCoroutine(RunAftermath());
    }

    private IEnumerator RunAftermath()
    {
        onAftermathStarted?.Invoke();

        float safeDelay = Mathf.Max(0f, burpDelay);

        if (safeDelay > 0f)
            yield return new WaitForSeconds(safeDelay);

        AudioClip burpClip = GetRandomBurpClip();

        if (audioSource != null && burpClip != null)
            audioSource.PlayOneShot(burpClip);

        if (smokeParticles != null)
        {
            smokeParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            smokeParticles.Play(true);
        }

        onBurpPlayed?.Invoke();
        aftermathRoutine = null;
        onAftermathFinished?.Invoke();
    }

    private AudioClip GetRandomBurpClip()
    {
        if (burpClips == null || burpClips.Length == 0)
            return null;

        if (burpClips.Length == 1)
            return burpClips[0];

        return burpClips[Random.Range(0, burpClips.Length)];
    }
}
