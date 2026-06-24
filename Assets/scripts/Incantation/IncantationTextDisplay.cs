using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;

public class IncantationTextDisplay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private IncantationManager incantationManager;
    [SerializeField] private TextMeshProUGUI targetText;

    [Header("Display")]
    [SerializeField] private string emptyText = "Awaiting incantation...";
    [SerializeField] private Color completedWordColor = new Color(0.45f, 0.45f, 0.45f);
    [SerializeField] private Color currentWordColor = Color.yellow;
    [SerializeField] private Color remainingWordColor = Color.white;
    [SerializeField] private Color correctFeedbackColor = Color.green;
    [SerializeField] private Color incorrectFeedbackColor = Color.red;
    [SerializeField] private float feedbackDuration = 0.25f;

    private Coroutine feedbackCoroutine;
    private int feedbackWordIndex = -1;
    private bool hasFeedbackColor;
    private Color feedbackColor;

    private void Reset()
    {
        targetText = GetComponent<TextMeshProUGUI>();
    }

    private void OnEnable()
    {
        SubscribeToIncantationManager();
        UpdateDisplay();
    }

    private void OnDisable()
    {
        UnsubscribeFromIncantationManager();
        StopFeedback();
    }

    private void OnValidate()
    {
        if (targetText == null)
            targetText = GetComponent<TextMeshProUGUI>();

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

        targetText.text = BuildIncantationText();
    }

    private void SubscribeToIncantationManager()
    {
        if (incantationManager == null)
            return;

        incantationManager.OnIncantationGenerated.AddListener(HandleIncantationGenerated);
        incantationManager.OnCorrectWord.AddListener(HandleCorrectWord);
        incantationManager.OnIncorrectWord.AddListener(HandleIncorrectWord);
        incantationManager.OnIncantationCompleted.AddListener(HandleIncantationCompleted);
    }

    private void UnsubscribeFromIncantationManager()
    {
        if (incantationManager == null)
            return;

        incantationManager.OnIncantationGenerated.RemoveListener(HandleIncantationGenerated);
        incantationManager.OnCorrectWord.RemoveListener(HandleCorrectWord);
        incantationManager.OnIncorrectWord.RemoveListener(HandleIncorrectWord);
        incantationManager.OnIncantationCompleted.RemoveListener(HandleIncantationCompleted);
    }

    private void HandleIncantationGenerated()
    {
        StopFeedback();
        UpdateDisplay();
    }

    private void HandleCorrectWord()
    {
        if (incantationManager == null)
            return;

        int completedWordIndex = incantationManager.CurrentWordIndex - 1;
        ShowFeedback(completedWordIndex, correctFeedbackColor);
    }

    private void HandleIncorrectWord()
    {
        if (incantationManager == null)
            return;

        ShowFeedback(incantationManager.CurrentWordIndex, incorrectFeedbackColor);
    }

    private void HandleIncantationCompleted()
    {
        if (feedbackCoroutine != null)
            return;

        UpdateDisplay();
    }

    private void ShowFeedback(int wordIndex, Color color)
    {
        if (wordIndex < 0 || incantationManager == null || wordIndex >= incantationManager.CurrentIncantation.Count)
        {
            UpdateDisplay();
            return;
        }

        StopFeedback();

        feedbackWordIndex = wordIndex;
        feedbackColor = color;
        hasFeedbackColor = true;
        UpdateDisplay();

        if (isActiveAndEnabled)
            feedbackCoroutine = StartCoroutine(ClearFeedbackAfterDelay());
    }

    private IEnumerator ClearFeedbackAfterDelay()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, feedbackDuration));

        feedbackCoroutine = null;
        ClearFeedbackColor();
        UpdateDisplay();
    }

    private void StopFeedback()
    {
        if (feedbackCoroutine != null)
        {
            StopCoroutine(feedbackCoroutine);
            feedbackCoroutine = null;
        }

        ClearFeedbackColor();
    }

    private void ClearFeedbackColor()
    {
        feedbackWordIndex = -1;
        hasFeedbackColor = false;
    }

    private string BuildIncantationText()
    {
        StringBuilder builder = new StringBuilder();

        for (int i = 0; i < incantationManager.CurrentIncantation.Count; i++)
        {
            IncantationWord word = incantationManager.CurrentIncantation[i];

            if (i > 0)
                builder.Append(' ');

            AppendColoredWord(builder, word.Text, GetWordColor(word, i));
        }

        return builder.ToString();
    }

    private Color GetWordColor(IncantationWord word, int wordIndex)
    {
        if (hasFeedbackColor && wordIndex == feedbackWordIndex)
            return feedbackColor;

        if (word.IsCompleted)
            return completedWordColor;

        if (!incantationManager.IsCompleted && wordIndex == incantationManager.CurrentWordIndex)
            return currentWordColor;

        return remainingWordColor;
    }

    private void AppendColoredWord(StringBuilder builder, string wordText, Color color)
    {
        builder.Append("<color=#");
        builder.Append(ColorUtility.ToHtmlStringRGB(color));
        builder.Append('>');
        builder.Append(wordText);
        builder.Append("</color>");
    }
}
