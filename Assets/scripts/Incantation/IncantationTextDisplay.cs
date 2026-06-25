using System.Collections;
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
    [SerializeField] private float writingSpeed = 18f;

    private Coroutine writingCoroutine;
    private Coroutine feedbackCoroutine;
    private int revealedCharacterCount = int.MaxValue;
    private int feedbackWordIndex = -1;
    private bool hasFeedbackColor;
    private Color feedbackColor;

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
        StopFeedback();
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
        StartWriting();
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

            AppendColoredWord(builder, visibleWordText, GetWordColor(word, i));
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
