using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class IncantationTextDisplay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private IncantationManager incantationManager;
    [SerializeField] private TMP_Text targetText;

    [Header("Display")]
    [SerializeField] private string emptyText = "Awaiting incantation...";
    [SerializeField] private Color completedWordColor = new Color(0.45f, 0.45f, 0.45f);
    [SerializeField] private Color currentWordColor = Color.yellow;
    [SerializeField] private Color remainingWordColor = Color.white;
    [SerializeField] private Color correctFeedbackColor = Color.green;
    [SerializeField] private Color incorrectFeedbackColor = Color.red;
    [SerializeField] private float feedbackDuration = 0.25f;
    [Min(0f)]
    [SerializeField] private float incorrectFeedbackDuration = 0.45f;
    [SerializeField] private float replayStepDuration = 0.22f;
    [SerializeField] private float replayPulseScale = 1.15f;
    [Min(0f)]
    [SerializeField] private float writingSpeed = 18f;

    private Coroutine writingCoroutine;
    private Coroutine replayCoroutine;
    private readonly Queue<ReplayWordStep> replaySteps = new Queue<ReplayWordStep>();
    private int revealedCharacterCount = int.MaxValue;
    private int replayBaseCompletedWordIndex;
    private int replayAcceptedWordCount;
    private int replayWordIndex = -1;
    private bool isReplayingJudgment;
    private bool hasReplayWordColor;
    private bool hasReplayFinishedSignal;
    private bool hasFinalRejectedWord;
    private float replayWordScale = 1f;
    private Color replayWordColor;
    private int finalRejectedWordIndex = -1;

    public bool IsReplayingJudgment => isReplayingJudgment || replayCoroutine != null || replaySteps.Count > 0;

    private void Reset()
    {
        targetText = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        SubscribeToIncantationManager();
        UpdateDisplay();
    }

    private void OnDisable()
    {
        UnsubscribeFromIncantationManager();
        StopWriting();
        StopReplayAnimation();
    }

    private void OnValidate()
    {
        if (targetText == null)
            targetText = GetComponent<TMP_Text>();

        UpdateDisplay();
    }

    public void UpdateDisplay()
    {
        if (targetText == null)
            return;

        targetText.richText = true;

        if (incantationManager == null || incantationManager.CurrentIncantation.Count == 0)
        {
            targetText.text = emptyText;
            return;
        }

        targetText.text = BuildIncantationText(revealedCharacterCount);
    }

    public void ReplayReset()
    {
        StopReplayAnimation();
        replaySteps.Clear();
        replayBaseCompletedWordIndex = incantationManager != null ? incantationManager.CurrentWordIndex : 0;
        replayAcceptedWordCount = 0;
        isReplayingJudgment = true;
        hasReplayFinishedSignal = false;
        hasFinalRejectedWord = false;
        finalRejectedWordIndex = -1;
        UpdateDisplay();
    }

    public void ReplayAcceptedWord(PhraseValidationWordResult wordResult)
    {
        EnsureReplayStarted(wordResult.WordIndex);
        replaySteps.Enqueue(new ReplayWordStep(wordResult.WordIndex, correctFeedbackColor, true));
        StartReplayAnimation();
    }

    public void ReplayRejectedWord(PhraseValidationWordResult wordResult)
    {
        EnsureReplayStarted(wordResult.WordIndex);
        hasFinalRejectedWord = true;
        finalRejectedWordIndex = wordResult.WordIndex;
        replaySteps.Enqueue(new ReplayWordStep(wordResult.WordIndex, incorrectFeedbackColor, false));
        StartReplayAnimation();
    }

    public void ReplayFinished()
    {
        hasReplayFinishedSignal = true;

        if (replayCoroutine == null && replaySteps.Count == 0)
            CompleteReplay();
    }

    private void SubscribeToIncantationManager()
    {
        if (incantationManager == null)
            return;

        incantationManager.OnIncantationGenerated.AddListener(HandleIncantationGenerated);
        incantationManager.OnIncantationCompleted.AddListener(HandleIncantationCompleted);
        incantationManager.OnPhraseReplayReset += ReplayReset;
        incantationManager.OnPhraseReplayAcceptedWord += ReplayAcceptedWord;
        incantationManager.OnPhraseReplayRejectedWord += ReplayRejectedWord;
        incantationManager.OnPhraseReplayFinished += ReplayFinished;
    }

    private void UnsubscribeFromIncantationManager()
    {
        if (incantationManager == null)
            return;

        incantationManager.OnIncantationGenerated.RemoveListener(HandleIncantationGenerated);
        incantationManager.OnIncantationCompleted.RemoveListener(HandleIncantationCompleted);
        incantationManager.OnPhraseReplayReset -= ReplayReset;
        incantationManager.OnPhraseReplayAcceptedWord -= ReplayAcceptedWord;
        incantationManager.OnPhraseReplayRejectedWord -= ReplayRejectedWord;
        incantationManager.OnPhraseReplayFinished -= ReplayFinished;
    }

    private void HandleIncantationGenerated()
    {
        ReplayReset();
        isReplayingJudgment = false;
        StartWriting();
    }

    private void HandleIncantationCompleted()
    {
        if (replayCoroutine != null || replaySteps.Count > 0)
            return;

        UpdateDisplay();
    }

    private void EnsureReplayStarted(int wordIndex)
    {
        if (isReplayingJudgment)
            return;

        replayBaseCompletedWordIndex = Mathf.Max(0, wordIndex);
        replayAcceptedWordCount = 0;
        isReplayingJudgment = true;
        hasReplayFinishedSignal = false;
    }

    private void StartWriting()
    {
        StopWriting();

        if (incantationManager == null || incantationManager.CurrentIncantation.Count == 0)
        {
            revealedCharacterCount = int.MaxValue;
            UpdateDisplay();
            return;
        }

        int fullCharacterCount = GetPlainIncantationCharacterCount();

        if (!isActiveAndEnabled || writingSpeed <= 0f || fullCharacterCount == 0)
        {
            revealedCharacterCount = int.MaxValue;
            UpdateDisplay();
            return;
        }

        revealedCharacterCount = 0;
        UpdateDisplay();
        writingCoroutine = StartCoroutine(RevealIncantation(fullCharacterCount));
    }

    private IEnumerator RevealIncantation(int fullCharacterCount)
    {
        float visibleCharacters = 0f;

        while (revealedCharacterCount < fullCharacterCount)
        {
            visibleCharacters += writingSpeed * Time.deltaTime;
            revealedCharacterCount = Mathf.Clamp(Mathf.FloorToInt(visibleCharacters), 0, fullCharacterCount);
            UpdateDisplay();
            yield return null;
        }

        revealedCharacterCount = int.MaxValue;
        writingCoroutine = null;
        UpdateDisplay();
    }

    private void StopWriting()
    {
        if (writingCoroutine != null)
        {
            StopCoroutine(writingCoroutine);
            writingCoroutine = null;
        }

        revealedCharacterCount = int.MaxValue;
    }

    private void StartReplayAnimation()
    {
        if (!isActiveAndEnabled)
        {
            while (replaySteps.Count > 0)
                ApplyReplayStepImmediately(replaySteps.Dequeue());

            if (hasReplayFinishedSignal)
                CompleteReplay();

            return;
        }

        if (replayCoroutine == null)
            replayCoroutine = StartCoroutine(PlayReplaySteps());
    }

    private IEnumerator PlayReplaySteps()
    {
        while (replaySteps.Count > 0)
            yield return PlayReplayStep(replaySteps.Dequeue());

        replayCoroutine = null;

        if (hasReplayFinishedSignal)
            CompleteReplay();
    }

    private IEnumerator PlayReplayStep(ReplayWordStep replayStep)
    {
        BeginReplayStep(replayStep);

        float elapsed = 0f;
        float duration = GetReplayStepDuration(replayStep);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
            float pulse = Mathf.Sin(progress * Mathf.PI);
            replayWordScale = Mathf.Lerp(1f, Mathf.Max(1f, replayPulseScale), pulse);
            UpdateDisplay();
            yield return null;
        }

        FinishReplayStep(replayStep);
    }

    private float GetReplayStepDuration(ReplayWordStep replayStep)
    {
        if (!replayStep.IsAccepted)
            return Mathf.Max(0f, incorrectFeedbackDuration);

        return Mathf.Max(0f, replayStepDuration > 0f ? replayStepDuration : feedbackDuration);
    }

    private void ApplyReplayStepImmediately(ReplayWordStep replayStep)
    {
        BeginReplayStep(replayStep);
        FinishReplayStep(replayStep);
    }

    private void BeginReplayStep(ReplayWordStep replayStep)
    {
        replayWordIndex = replayStep.WordIndex;
        replayWordColor = replayStep.Color;
        replayWordScale = Mathf.Max(1f, replayPulseScale);
        hasReplayWordColor = true;
        UpdateDisplay();
    }

    private void FinishReplayStep(ReplayWordStep replayStep)
    {
        if (replayStep.IsAccepted)
            replayAcceptedWordCount++;

        replayWordIndex = -1;
        replayWordScale = 1f;
        hasReplayWordColor = false;
        UpdateDisplay();
    }

    private void CompleteReplay()
    {
        isReplayingJudgment = false;
        hasReplayFinishedSignal = false;
        replayAcceptedWordCount = 0;
        replayWordIndex = -1;
        replayWordScale = 1f;
        hasReplayWordColor = false;
        hasFinalRejectedWord = false;
        finalRejectedWordIndex = -1;
        UpdateDisplay();
    }

    private void StopReplayAnimation()
    {
        if (replayCoroutine != null)
        {
            StopCoroutine(replayCoroutine);
            replayCoroutine = null;
        }

        replaySteps.Clear();
        isReplayingJudgment = false;
        hasReplayFinishedSignal = false;
        hasReplayWordColor = false;
        hasFinalRejectedWord = false;
        replayWordIndex = -1;
        finalRejectedWordIndex = -1;
        replayWordScale = 1f;
    }

    private int GetPlainIncantationCharacterCount()
    {
        if (incantationManager == null)
            return 0;

        int characterCount = 0;

        for (int i = 0; i < incantationManager.CurrentIncantation.Count; i++)
        {
            if (i > 0)
                characterCount++;

            string wordText = incantationManager.CurrentIncantation[i].Text;

            if (!string.IsNullOrEmpty(wordText))
                characterCount += wordText.Length;
        }

        return characterCount;
    }

    private string BuildIncantationText(int maxVisibleCharacters)
    {
        StringBuilder builder = new StringBuilder();
        int remainingVisibleCharacters = maxVisibleCharacters;

        for (int i = 0; i < incantationManager.CurrentIncantation.Count; i++)
        {
            IncantationWord word = incantationManager.CurrentIncantation[i];

            if (i > 0)
            {
                if (remainingVisibleCharacters <= 0)
                    break;

                builder.Append(' ');
                remainingVisibleCharacters--;
            }

            string visibleWordText = GetVisibleWordText(word.Text, remainingVisibleCharacters);

            if (string.IsNullOrEmpty(visibleWordText))
                break;

            AppendColoredWord(builder, visibleWordText, GetWordColor(word, i), i);
            remainingVisibleCharacters -= visibleWordText.Length;
        }

        return builder.ToString();
    }

    private string GetVisibleWordText(string wordText, int remainingVisibleCharacters)
    {
        if (string.IsNullOrEmpty(wordText) || remainingVisibleCharacters <= 0)
            return string.Empty;

        if (remainingVisibleCharacters >= wordText.Length)
            return wordText;

        return wordText.Substring(0, remainingVisibleCharacters);
    }

    private Color GetWordColor(IncantationWord word, int wordIndex)
    {
        if (hasReplayWordColor && wordIndex == replayWordIndex)
            return replayWordColor;

        if (hasFinalRejectedWord && wordIndex == finalRejectedWordIndex)
            return incorrectFeedbackColor;

        if (isReplayingJudgment)
        {
            int visuallyCompletedWordCount = replayBaseCompletedWordIndex + replayAcceptedWordCount;

            if (wordIndex < visuallyCompletedWordCount)
                return completedWordColor;

            if (wordIndex == visuallyCompletedWordCount)
                return currentWordColor;

            return remainingWordColor;
        }

        if (word.IsCompleted)
            return completedWordColor;

        if (!incantationManager.IsCompleted && wordIndex == incantationManager.CurrentWordIndex)
            return currentWordColor;

        return remainingWordColor;
    }

    private void AppendColoredWord(StringBuilder builder, string wordText, Color color, int wordIndex)
    {
        bool shouldScaleWord = hasReplayWordColor && wordIndex == replayWordIndex && Mathf.Abs(replayWordScale - 1f) > 0.01f;

        if (shouldScaleWord)
        {
            builder.Append("<size=");
            builder.Append(Mathf.RoundToInt(replayWordScale * 100f));
            builder.Append("%>");
        }

        builder.Append("<color=#");
        builder.Append(ColorUtility.ToHtmlStringRGB(color));
        builder.Append('>');
        builder.Append(wordText);
        builder.Append("</color>");

        if (shouldScaleWord)
            builder.Append("</size>");
    }

    private readonly struct ReplayWordStep
    {
        public ReplayWordStep(int wordIndex, Color color, bool isAccepted)
        {
            WordIndex = wordIndex;
            Color = color;
            IsAccepted = isAccepted;
        }

        public int WordIndex { get; }
        public Color Color { get; }
        public bool IsAccepted { get; }
    }
}
