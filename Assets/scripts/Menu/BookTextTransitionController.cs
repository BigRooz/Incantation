using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Animates text already placed on the Living Book and coordinates its paper sound.
/// It never owns Book state or menu actions; callers supply both the text entries and target strings.
/// </summary>
public sealed class BookTextTransitionController : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip paperTransitionClip;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float disappearDuration = 0.25f;
    [SerializeField, Min(0f)] private float revealDuration = 0.9f;
    [SerializeField, Min(0f)] private float delayBetweenPhases = 0.05f;
    [SerializeField] private AnimationCurve revealCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    private Coroutine activeTransition;
    private readonly List<BookMenuItem> affectedMenuItems = new List<BookMenuItem>();

    public void PlayTransition(
        IReadOnlyList<TMP_Text> texts,
        IReadOnlyList<string> targetStrings,
        Action onCompleted = null)
    {
        if (activeTransition != null)
        {
            StopCoroutine(activeTransition);
            activeTransition = null;
            RestoreMenuInteraction();
        }

        RestoreFullVisibility(texts);
        CacheAndDisableMenuItems(texts);

        if (audioSource != null && paperTransitionClip != null)
        {
            audioSource.Stop();
            audioSource.PlayOneShot(paperTransitionClip);
        }

        activeTransition = StartCoroutine(RunTransition(texts, targetStrings, onCompleted));
    }

    private IEnumerator RunTransition(
        IReadOnlyList<TMP_Text> texts,
        IReadOnlyList<string> targetStrings,
        Action onCompleted)
    {
        yield return AnimateVisibility(texts, disappearDuration, false);

        AssignTargets(texts, targetStrings);

        if (delayBetweenPhases > 0f)
        {
            yield return WaitForUnscaledSeconds(delayBetweenPhases);
        }

        yield return AnimateVisibility(texts, revealDuration, true);

        RestoreFullVisibility(texts);
        RestoreMenuInteraction();
        activeTransition = null;
        onCompleted?.Invoke();
    }

    private IEnumerator AnimateVisibility(IReadOnlyList<TMP_Text> texts, float duration, bool revealing)
    {
        int largestCharacterCount = RefreshAndGetLargestCharacterCount(texts);

        if (duration <= 0f || largestCharacterCount == 0)
        {
            SetVisibility(texts, revealing ? 1f : 0f, revealing);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            SetVisibility(texts, progress, revealing);
            yield return null;
        }

        SetVisibility(texts, 1f, revealing);
    }

    private void SetVisibility(IReadOnlyList<TMP_Text> texts, float progress, bool revealing)
    {
        if (texts == null)
        {
            return;
        }

        float evaluatedProgress = revealing && revealCurve != null
            ? Mathf.Clamp01(revealCurve.Evaluate(progress))
            : progress;

        for (int i = 0; i < texts.Count; i++)
        {
            TMP_Text textEntry = texts[i];

            if (textEntry == null)
            {
                continue;
            }

            int characterCount = textEntry.textInfo.characterCount;
            float visibleFraction = revealing ? evaluatedProgress : 1f - progress;
            textEntry.maxVisibleCharacters = Mathf.RoundToInt(characterCount * visibleFraction);
        }
    }

    private static int RefreshAndGetLargestCharacterCount(IReadOnlyList<TMP_Text> texts)
    {
        int largestCharacterCount = 0;

        if (texts == null)
        {
            return largestCharacterCount;
        }

        for (int i = 0; i < texts.Count; i++)
        {
            TMP_Text textEntry = texts[i];

            if (textEntry == null)
            {
                continue;
            }

            textEntry.ForceMeshUpdate();
            largestCharacterCount = Mathf.Max(largestCharacterCount, textEntry.textInfo.characterCount);
        }

        return largestCharacterCount;
    }

    private static void AssignTargets(IReadOnlyList<TMP_Text> texts, IReadOnlyList<string> targetStrings)
    {
        if (texts == null)
        {
            return;
        }

        for (int i = 0; i < texts.Count; i++)
        {
            if (texts[i] == null)
            {
                continue;
            }

            texts[i].text = targetStrings != null && i < targetStrings.Count
                ? targetStrings[i] ?? string.Empty
                : string.Empty;
            texts[i].maxVisibleCharacters = 0;
            texts[i].ForceMeshUpdate();
        }
    }

    private void CacheAndDisableMenuItems(IReadOnlyList<TMP_Text> texts)
    {
        affectedMenuItems.Clear();

        if (texts == null)
        {
            return;
        }

        for (int i = 0; i < texts.Count; i++)
        {
            TMP_Text textEntry = texts[i];
            BookMenuItem menuItem = textEntry != null ? textEntry.GetComponent<BookMenuItem>() : null;

            if (menuItem == null || affectedMenuItems.Contains(menuItem))
            {
                continue;
            }

            menuItem.SetInteractionEnabled(false);
            affectedMenuItems.Add(menuItem);
        }
    }

    private void RestoreMenuInteraction()
    {
        for (int i = 0; i < affectedMenuItems.Count; i++)
        {
            BookMenuItem menuItem = affectedMenuItems[i];

            if (menuItem == null)
            {
                continue;
            }

            TMP_Text textEntry = menuItem.GetComponent<TMP_Text>();
            bool shouldEnable = textEntry != null && !string.IsNullOrEmpty(textEntry.text);
            menuItem.RefreshVisualBaseline();
            menuItem.SetInteractionEnabled(shouldEnable);
        }

        affectedMenuItems.Clear();
    }

    private static void RestoreFullVisibility(IReadOnlyList<TMP_Text> texts)
    {
        if (texts == null)
        {
            return;
        }

        for (int i = 0; i < texts.Count; i++)
        {
            if (texts[i] != null)
            {
                texts[i].maxVisibleCharacters = int.MaxValue;
            }
        }
    }

    private static IEnumerator WaitForUnscaledSeconds(float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }
}
