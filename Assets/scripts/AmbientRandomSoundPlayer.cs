using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plays random ambient audio clips at randomized intervals.
/// Uses a serialized AudioSource when assigned, or the local AudioSource required by this component.
/// TODO: Route through an audio mixer group if the project adds dedicated ambience mixing.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AmbientRandomSoundPlayer : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private List<AudioClip> ambientClips = new List<AudioClip>();

    [Header("Delay")]
    [SerializeField, Min(0f)] private float minDelaySeconds = 8f;
    [SerializeField, Min(0f)] private float maxDelaySeconds = 22f;

    [Header("Volume")]
    [SerializeField, Range(0f, 1f)] private float minVolume = 0.35f;
    [SerializeField, Range(0f, 1f)] private float maxVolume = 0.8f;

    [Header("Pitch")]
    [SerializeField] private float minPitch = 0.92f;
    [SerializeField] private float maxPitch = 1.08f;

    [Header("Playback")]
    [SerializeField] private bool playOnStart = true;

    private Coroutine playbackRoutine;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        ConfigureAudioSource();
    }

    private void OnEnable()
    {
        if (playOnStart)
            StartPlaying();
    }

    private void OnDisable()
    {
        StopPlaying();
    }

    private void OnValidate()
    {
        minDelaySeconds = Mathf.Max(0f, minDelaySeconds);
        maxDelaySeconds = Mathf.Max(0f, maxDelaySeconds);

        minVolume = Mathf.Clamp01(minVolume);
        maxVolume = Mathf.Clamp01(maxVolume);

        if (maxDelaySeconds < minDelaySeconds)
            maxDelaySeconds = minDelaySeconds;

        if (maxVolume < minVolume)
            maxVolume = minVolume;

        if (maxPitch < minPitch)
            maxPitch = minPitch;
    }

    public void StartPlaying()
    {
        if (playbackRoutine != null)
            return;

        playbackRoutine = StartCoroutine(PlayAmbientLoop());
    }

    public void StopPlaying()
    {
        if (playbackRoutine != null)
        {
            StopCoroutine(playbackRoutine);
            playbackRoutine = null;
        }

        if (audioSource != null)
            audioSource.Stop();
    }

    private IEnumerator PlayAmbientLoop()
    {
        while (enabled)
        {
            yield return new WaitForSeconds(GetRandomDelay());

            if (audioSource == null || ambientClips.Count == 0)
                continue;

            AudioClip selectedClip = GetRandomClip();

            if (selectedClip == null)
                continue;

            yield return WaitForCurrentSound();

            PlayClip(selectedClip);
        }

        playbackRoutine = null;
    }

    private float GetRandomDelay()
    {
        return Random.Range(minDelaySeconds, maxDelaySeconds);
    }

    private AudioClip GetRandomClip()
    {
        int clipIndex = Random.Range(0, ambientClips.Count);
        return ambientClips[clipIndex];
    }

    private IEnumerator WaitForCurrentSound()
    {
        while (audioSource != null && audioSource.isPlaying)
            yield return null;
    }

    private void ConfigureAudioSource()
    {
        if (audioSource == null)
            return;

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
    }

    private void PlayClip(AudioClip clip)
    {
        audioSource.pitch = Random.Range(minPitch, maxPitch);
        audioSource.volume = Random.Range(minVolume, maxVolume);
        audioSource.clip = clip;
        audioSource.Play();
    }
}
