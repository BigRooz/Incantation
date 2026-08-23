using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presents one assigned SpellDefinition through optional authored card visuals.
/// It has no spell execution, gameplay, ritual, or networking dependencies.
/// </summary>
public sealed class SpellCardView : MonoBehaviour
{
    [Header("Definition")]
    [SerializeField] private SpellDefinition definition;

    [Header("Card Visuals")]
    [SerializeField] private Renderer cardMesh;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text incantationText;
    [SerializeField] private Image artworkImage;
    [SerializeField] private SpriteRenderer artworkRenderer;
    [SerializeField] private Canvas cardCanvas;

    [Header("Selected Card Light")]
    [SerializeField] private Light glowLight;
    [SerializeField, Min(0f)] private float selectedLightIntensity = 1f;
    [SerializeField, Min(0f)] private float selectedLightRange = 3f;

    [Header("Light Transition")]
    [SerializeField, Min(0f)] private float fadeInDuration = 0.25f;
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.15f;
    [SerializeField] private AnimationCurve fadeInCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve fadeOutCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Optional Selected Flicker")]
    [SerializeField] private bool enableSelectedFlicker;
    [SerializeField, Min(0f)] private float flickerAmount = 0.05f;
    [SerializeField, Min(0f)] private float flickerSpeed = 1.5f;

    private bool isVisible = true;
    private bool isSelected;
    private bool fadeInComplete;
    private Coroutine lightTransition;
    private Light controlledGlowLight;
    private bool isConsumptionGlowActive;

    public SpellDefinition CurrentDefinition => definition;
    public Renderer CardMesh => cardMesh;
    public TMP_Text NameText => nameText;
    public TMP_Text DescriptionText => descriptionText;
    public TMP_Text IncantationText => incantationText;
    public Light GlowLight => glowLight;
    public Canvas CardCanvas => cardCanvas;

    private void Awake()
    {
        ApplyDefinition();
    }

    private void OnEnable()
    {
        ApplyDefinition();
    }

    private void OnDisable()
    {
        DisableGlowLightImmediately();
    }

    private void Update()
    {
        if (!enableSelectedFlicker || !fadeInComplete || !CanIlluminate())
            return;

        float time = Time.unscaledTime * flickerSpeed;
        float wave = Mathf.Sin(time * Mathf.PI * 2f) * 0.65f
            + Mathf.Sin((time * 1.73f + 0.41f) * Mathf.PI * 2f) * 0.35f;
        glowLight.intensity = Mathf.Max(0f, selectedLightIntensity + wave * flickerAmount);
    }

    /// <summary>
    /// Assigns presentation data and refreshes every available visual reference.
    /// </summary>
    public void SetDefinition(SpellDefinition spellDefinition)
    {
        definition = spellDefinition;
        ApplyDefinition();
    }

    /// <summary>
    /// Enables or disables this card's authored visual components without changing gameplay state.
    /// </summary>
    public void SetVisible(bool isVisible)
    {
        this.isVisible = isVisible;

        if (cardMesh != null)
            cardMesh.enabled = isVisible;

        if (nameText != null)
            nameText.enabled = isVisible;

        if (descriptionText != null)
            descriptionText.enabled = isVisible;

        if (incantationText != null)
            incantationText.enabled = isVisible;

        ApplyArtworkVisibility();

        if (cardCanvas != null)
            cardCanvas.enabled = isVisible;

        if (isVisible)
            RefreshGlowLightState();
        else
            DisableGlowLightImmediately();
    }

    /// <summary>
    /// Updates selected presentation without changing hand selection rules or spell gameplay.
    /// </summary>
    public void SetSelected(bool selected)
    {
        isSelected = selected;

        if (selected)
            BeginFadeIn();
        else
            BeginFadeOut();
    }

    /// <summary>
    /// Cancels any transition and extinguishes the physical Light immediately.
    /// Used by destructive or hidden presentation states such as consumption.
    /// </summary>
    public void DisableGlowLightImmediately()
    {
        StopLightTransition();
        isConsumptionGlowActive = false;
        isSelected = false;
        fadeInComplete = false;

        if (controlledGlowLight != null)
        {
            controlledGlowLight.intensity = 0f;
            controlledGlowLight.enabled = false;
        }

        if (glowLight != null && glowLight != controlledGlowLight)
        {
            glowLight.intensity = 0f;
            glowLight.enabled = false;
        }
    }

    /// <summary>
    /// Gives card-consumption presentation exclusive control of the existing glow light.
    /// No material instance is created and selection state is not restored afterward.
    /// </summary>
    public void BeginConsumptionGlow()
    {
        StopLightTransition();
        isConsumptionGlowActive = glowLight != null && isActiveAndEnabled && isVisible;
        isSelected = false;
        fadeInComplete = false;

        if (!isConsumptionGlowActive)
            return;

        glowLight.color = definition != null ? definition.GlowColor : glowLight.color;
        glowLight.range = selectedLightRange;
        glowLight.enabled = true;
        glowLight.intensity = selectedLightIntensity;
    }

    /// <summary>Ramps the existing physical light during magical consumption.</summary>
    public void SetConsumptionGlowProgress(float normalizedProgress, float intensityMultiplier)
    {
        if (!isConsumptionGlowActive || glowLight == null)
            return;

        float multiplier = Mathf.Max(1f, intensityMultiplier);
        glowLight.intensity = Mathf.Lerp(
            selectedLightIntensity,
            selectedLightIntensity * multiplier,
            Mathf.Clamp01(normalizedProgress));
    }

    private void ApplyDefinition()
    {
        string displayName = definition != null ? definition.DisplayName : string.Empty;
        string description = definition != null ? definition.Description : string.Empty;
        string incantation = definition != null ? definition.SpokenIncantation : string.Empty;

        if (nameText != null)
            nameText.text = displayName;

        if (descriptionText != null)
            descriptionText.text = description;

        if (incantationText != null)
            incantationText.text = incantation;

        if (artworkImage != null)
            artworkImage.sprite = definition != null ? definition.CardArtwork : null;

        if (artworkRenderer != null)
            artworkRenderer.sprite = definition != null ? definition.CardArtwork : null;

        ApplyArtworkVisibility();
        ApplyGlowLightSettings();
    }

    private void ApplyArtworkVisibility()
    {
        bool hasArtwork = definition != null && definition.CardArtwork != null;

        if (artworkImage != null)
            artworkImage.enabled = isVisible && hasArtwork;

        if (artworkRenderer != null)
            artworkRenderer.enabled = isVisible && hasArtwork;
    }

    private void ApplyGlowLightSettings()
    {
        SynchronizeControlledGlowLight();

        if (glowLight == null)
        {
            DisableGlowLightImmediately();
            return;
        }

        glowLight.range = selectedLightRange;

        if (definition != null)
            glowLight.color = definition.GlowColor;

        RefreshGlowLightState();
    }

    private void RefreshGlowLightState()
    {
        if (!CanIlluminate())
        {
            DisableGlowLightImmediately();
            return;
        }

        BeginFadeIn();
    }

    private void BeginFadeIn()
    {
        if (!CanIlluminate())
        {
            DisableGlowLightImmediately();
            return;
        }

        glowLight.color = definition.GlowColor;
        glowLight.range = selectedLightRange;
        glowLight.enabled = true;
        fadeInComplete = false;
        StartLightTransition(selectedLightIntensity, fadeInDuration, fadeInCurve, true);
    }

    private void BeginFadeOut()
    {
        fadeInComplete = false;

        if (glowLight == null || !glowLight.enabled)
        {
            DisableGlowLightImmediately();
            return;
        }

        StartLightTransition(0f, fadeOutDuration, fadeOutCurve, false);
    }

    private void StartLightTransition(float targetIntensity, float duration, AnimationCurve curve, bool markFadeInComplete)
    {
        StopLightTransition();

        if (duration <= 0f)
        {
            if (glowLight == null)
                return;

            glowLight.intensity = Mathf.Max(0f, targetIntensity);
            fadeInComplete = markFadeInComplete && CanIlluminate();

            if (!markFadeInComplete)
                glowLight.enabled = false;

            return;
        }

        lightTransition = StartCoroutine(FadeLightRoutine(targetIntensity, duration, curve, markFadeInComplete));
    }

    private IEnumerator FadeLightRoutine(float targetIntensity, float duration, AnimationCurve curve, bool markFadeInComplete)
    {
        Light fadingLight = glowLight;
        float startIntensity = fadingLight != null ? fadingLight.intensity : 0f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (fadingLight == null || fadingLight != glowLight)
            {
                if (fadingLight != null)
                {
                    fadingLight.intensity = 0f;
                    fadingLight.enabled = false;
                }

                lightTransition = null;
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            float progress = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
            float curvedProgress = curve != null ? curve.Evaluate(progress) : progress;
            fadingLight.intensity = Mathf.Max(0f, Mathf.LerpUnclamped(startIntensity, targetIntensity, curvedProgress));
            yield return null;
        }

        if (fadingLight == null || fadingLight != glowLight)
        {
            if (fadingLight != null)
            {
                fadingLight.intensity = 0f;
                fadingLight.enabled = false;
            }

            lightTransition = null;
            yield break;
        }

        fadingLight.intensity = Mathf.Max(0f, targetIntensity);
        fadeInComplete = markFadeInComplete && CanIlluminate();

        if (!markFadeInComplete)
            fadingLight.enabled = false;

        lightTransition = null;
    }

    private void StopLightTransition()
    {
        if (lightTransition == null)
            return;

        StopCoroutine(lightTransition);
        lightTransition = null;
    }

    private bool CanIlluminate()
    {
        return glowLight != null && isActiveAndEnabled && isVisible &&
            (isSelected || isConsumptionGlowActive) && definition != null;
    }

    private void SynchronizeControlledGlowLight()
    {
        if (controlledGlowLight == glowLight)
            return;

        StopLightTransition();

        if (controlledGlowLight != null)
        {
            controlledGlowLight.intensity = 0f;
            controlledGlowLight.enabled = false;
        }

        controlledGlowLight = glowLight;
        fadeInComplete = false;
    }

    private void OnValidate()
    {
        selectedLightIntensity = Mathf.Max(0f, selectedLightIntensity);
        selectedLightRange = Mathf.Max(0f, selectedLightRange);
        fadeInDuration = Mathf.Max(0f, fadeInDuration);
        fadeOutDuration = Mathf.Max(0f, fadeOutDuration);
        flickerAmount = Mathf.Max(0f, flickerAmount);
        flickerSpeed = Mathf.Max(0f, flickerSpeed);
        ApplyDefinition();
    }
}
