using TMPro;
using UnityEngine;

public class NotebookSpellPage : MonoBehaviour
{
    [Header("Text")]
    [SerializeField] private TMP_Text spellNameText;
    [SerializeField] private TMP_Text spokenPhraseText;
    [SerializeField] private TMP_Text rarityText;
    [SerializeField] private TMP_Text descriptionText;

    private void Reset()
    {
        TMP_Text[] textFields = GetComponentsInChildren<TMP_Text>(true);

        if (textFields.Length > 0)
            spellNameText = textFields[0];

        if (textFields.Length > 1)
            spokenPhraseText = textFields[1];

        if (textFields.Length > 2)
            rarityText = textFields[2];

        if (textFields.Length > 3)
            descriptionText = textFields[3];
    }

    public void Display(SpellPhrase spellPhrase)
    {
        if (spellPhrase == null)
        {
            Clear();
            return;
        }

        SetText(spellNameText, spellPhrase.SpellName);
        SetText(spokenPhraseText, spellPhrase.SpokenPhrase);
        SetText(rarityText, spellPhrase.Rarity.ToString());
        SetText(descriptionText, spellPhrase.Description);
    }

    public void Clear()
    {
        SetText(spellNameText, string.Empty);
        SetText(spokenPhraseText, string.Empty);
        SetText(rarityText, string.Empty);
        SetText(descriptionText, string.Empty);
    }

    private void SetText(TMP_Text targetText, string value)
    {
        if (targetText == null)
            return;

        targetText.text = value;
    }
}
