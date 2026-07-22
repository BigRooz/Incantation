using TMPro;
using UnityEngine;

/// <summary>
/// Owns the render-only references and presentation controls for one spell card.
/// It has no spell data, gameplay, ritual, or networking dependencies.
/// </summary>
public sealed class SpellCardView : MonoBehaviour
{
    [Header("Card Visuals")]
    [SerializeField] private Renderer cardMesh;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text incantationText;
    [SerializeField] private Renderer glowRenderer;
    [SerializeField] private Canvas cardCanvas;

    public Renderer CardMesh => cardMesh;
    public TMP_Text NameText => nameText;
    public TMP_Text DescriptionText => descriptionText;
    public TMP_Text IncantationText => incantationText;
    public Renderer GlowRenderer => glowRenderer;
    public Canvas CardCanvas => cardCanvas;

    /// <summary>
    /// Enables or disables this card's authored visual components without changing gameplay state.
    /// </summary>
    public void SetVisible(bool isVisible)
    {
        if (cardMesh != null)
            cardMesh.enabled = isVisible;

        if (nameText != null)
            nameText.enabled = isVisible;

        if (descriptionText != null)
            descriptionText.enabled = isVisible;

        if (incantationText != null)
            incantationText.enabled = isVisible;

        if (glowRenderer != null)
            glowRenderer.enabled = isVisible;

        if (cardCanvas != null)
            cardCanvas.enabled = isVisible;
    }
}
