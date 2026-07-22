using UnityEngine;

/// <summary>
/// Stores designer-authored spell card identity and presentation data.
/// It contains no spell execution, drawing, inventory, voice validation, or networking behavior.
/// </summary>
[CreateAssetMenu(fileName = "New Spell Definition", menuName = "Incantation/Spell Definition")]
public sealed class SpellDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string stableIdentifier;
    [SerializeField] private string displayName;

    [Header("Card Text")]
    [SerializeField, TextArea(1, 3)] private string spokenIncantation;
    [SerializeField, TextArea(2, 6)] private string description;

    [Header("Presentation")]
    [SerializeField] private SpellRarity rarity = SpellRarity.Common;
    [SerializeField] private Sprite cardArtwork;
    [SerializeField] private bool overrideRarityGlowColor;
    [SerializeField, ColorUsage(true, true)] private Color rarityGlowColor = Color.white;
    [SerializeField] private AudioClip audioClip;
    [SerializeField] private GameObject visualPrefab;

    public string StableIdentifier => stableIdentifier;
    public string DisplayName => displayName;
    public string SpokenIncantation => spokenIncantation;
    public string Description => description;
    public SpellRarity Rarity => rarity;
    public Sprite CardArtwork => cardArtwork;
    public AudioClip AudioClip => audioClip;
    public GameObject VisualPrefab => visualPrefab;
    public Color GlowColor => overrideRarityGlowColor ? rarityGlowColor : GetDefaultGlowColor(rarity);

    /// <summary>Returns the presentation-only default color for a rarity.</summary>
    public static Color GetDefaultGlowColor(SpellRarity spellRarity)
    {
        switch (spellRarity)
        {
            case SpellRarity.Uncommon:
                return new Color(0.2f, 0.85f, 0.25f, 1f);
            case SpellRarity.Rare:
                return new Color(0.15f, 0.45f, 1f, 1f);
            case SpellRarity.Epic:
                return new Color(0.65f, 0.2f, 0.9f, 1f);
            case SpellRarity.UltraRare:
                return new Color(1f, 0.08f, 0.05f, 1f);
            default:
                return Color.white;
        }
    }
}
