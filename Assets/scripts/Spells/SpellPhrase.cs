using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SpellPhrase
{
    [SerializeField] private string spellName;
    [SerializeField] private string spokenPhrase;
    [SerializeField] private List<string> speechAliases = new List<string>();
    [TextArea(2, 4)]
    [SerializeField] private string description;
    [SerializeField] private string gameplayEffectId;
    [SerializeField] private SpellPhraseRarity rarity = SpellPhraseRarity.Common;
    [SerializeField] private bool enabled = true;

    public string SpellName => spellName;
    public string SpokenPhrase => spokenPhrase;
    public IReadOnlyList<string> SpeechAliases
    {
        get
        {
            if (speechAliases == null)
                speechAliases = new List<string>();

            return speechAliases;
        }
    }

    public string Description => description;
    public string GameplayEffectId => gameplayEffectId;
    public SpellPhraseRarity Rarity => rarity;
    public bool Enabled => enabled;

    public SpellPhrase()
    {
        spellName = string.Empty;
        spokenPhrase = string.Empty;
        speechAliases = new List<string>();
        description = string.Empty;
        gameplayEffectId = string.Empty;
        rarity = SpellPhraseRarity.Common;
        enabled = true;
    }

    public SpellPhrase(
        string spellName,
        string spokenPhrase,
        IEnumerable<string> speechAliases,
        string description,
        string gameplayEffectId,
        SpellPhraseRarity rarity,
        bool enabled = true)
    {
        this.spellName = spellName;
        this.spokenPhrase = spokenPhrase;
        this.speechAliases = new List<string>();

        if (speechAliases != null)
            this.speechAliases.AddRange(speechAliases);

        this.description = description;
        this.gameplayEffectId = gameplayEffectId;
        this.rarity = rarity;
        this.enabled = enabled;
    }
}
