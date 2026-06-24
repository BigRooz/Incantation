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

        incantationManager.OnIncantationGenerated.AddListener(UpdateDisplay);
        incantationManager.OnCorrectWord.AddListener(UpdateDisplay);
        incantationManager.OnIncantationCompleted.AddListener(UpdateDisplay);
    }

    private void UnsubscribeFromIncantationManager()
    {
        if (incantationManager == null)
            return;

        incantationManager.OnIncantationGenerated.RemoveListener(UpdateDisplay);
        incantationManager.OnCorrectWord.RemoveListener(UpdateDisplay);
        incantationManager.OnIncantationCompleted.RemoveListener(UpdateDisplay);
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
