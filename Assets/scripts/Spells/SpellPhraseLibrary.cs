using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SpellPhraseLibrary", menuName = "Incantation/Spell Phrase Library")]
public class SpellPhraseLibrary : ScriptableObject
{
    [Header("Secret Spell Phrases")]
    [Tooltip("Future-only phrase data for secret notebooks, hidden spell cards, demon events, campaign, and unlockable content. This is intentionally separate from ritual word generation.")]
    [SerializeField] private List<SpellPhrase> spellPhrases = new List<SpellPhrase>();

    public IReadOnlyList<SpellPhrase> SpellPhrases
    {
        get
        {
            if (spellPhrases == null)
                spellPhrases = new List<SpellPhrase>();

            return spellPhrases;
        }
    }

    public IReadOnlyList<SpellPhrase> GetSpellPhrases()
    {
        return SpellPhrases;
    }
}
