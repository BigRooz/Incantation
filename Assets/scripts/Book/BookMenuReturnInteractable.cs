using System.Collections;
using UnityEngine;

public class BookMenuReturnInteractable : MonoBehaviour
{
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private const string EmissionKeyword = "_EMISSION";

    [SerializeField] private CameraTransitionManager cameraTransitionManager;
    [SerializeField] private Transform bookMenuTarget;
    [SerializeField] private BookRotationController bookRotationController;
    [SerializeField] private Renderer[] highlightRenderers;
    [SerializeField] private HoverMode hoverMode = HoverMode.Automatic;
    [SerializeField] private Color hoverEmissionColor = Color.white;
    [SerializeField] private float hoverEmissionIntensity = 0.35f;
    [SerializeField] private float hoverBaseColorMultiplier = 1.2f;
    [SerializeField] private Light hoverLight;
    [SerializeField] private float hoverLightIntensity = 1.5f;
    [SerializeField] private float hoverLightFadeDuration = 0.12f;
    [SerializeField] private bool interactionEnabled = true;

    private MaterialPropertyBlock workingPropertyBlock;
    private RendererHighlightState[] rendererStates;
    private Coroutine hoverLightFadeCoroutine;
    private bool isHovered;

    private void Awake()
    {
        workingPropertyBlock = new MaterialPropertyBlock();
        CacheOriginalPropertyBlocks();
    }

    private void OnEnable()
    {
        LocalInputContextGate.ContextChanged += HandleInputContextChanged;

        if (LocalInputContextGate.IsBookInputCaptured)
            StopHoverEffects();
    }

    private void OnDisable()
    {
        LocalInputContextGate.ContextChanged -= HandleInputContextChanged;
        StopHoverLightFade();

        if (hoverLight != null)
            hoverLight.intensity = 0f;

        RestoreOriginalVisualState();
        isHovered = false;
    }

    private void OnMouseEnter()
    {
        if (!interactionEnabled || LocalInputContextGate.IsBookInputCaptured)
            return;

        isHovered = true;
        ApplyHoverVisualState();
        StartHoverLightFade(hoverLightIntensity);
    }

    private void OnMouseExit()
    {
        isHovered = false;
        RestoreOriginalVisualState();
        StartHoverLightFade(0f);
    }

    private void OnMouseDown()
    {
        if (!interactionEnabled ||
            LocalInputContextGate.IsBookInputCaptured ||
            cameraTransitionManager == null)
            return;

        if (bookRotationController != null)
            bookRotationController.RotateToFront();

        cameraTransitionManager.MoveTo(bookMenuTarget);
    }

    private void HandleInputContextChanged(LocalInputContext context)
    {
        if (context == LocalInputContext.BookInteraction ||
            context == LocalInputContext.TextEntry)
            StopHoverEffects();
    }

    public void SetInteractionEnabled(bool enabled)
    {
        interactionEnabled = enabled;

        if (!interactionEnabled)
            StopHoverEffects();
    }

    public void EnableInteraction()
    {
        interactionEnabled = true;
    }

    public void DisableInteraction()
    {
        interactionEnabled = false;
        StopHoverEffects();
    }

    private void StopHoverEffects()
    {
        isHovered = false;
        RestoreOriginalVisualState();
        StartHoverLightFade(0f);
    }

    private void StartHoverLightFade(float targetIntensity)
    {
        if (hoverLight == null)
            return;

        StopHoverLightFade();
        hoverLightFadeCoroutine = StartCoroutine(FadeHoverLight(targetIntensity));
    }

    private IEnumerator FadeHoverLight(float targetIntensity)
    {
        float startIntensity = hoverLight.intensity;
        float duration = Mathf.Max(0f, hoverLightFadeDuration);

        if (duration <= 0f)
        {
            hoverLight.intensity = targetIntensity;
            hoverLightFadeCoroutine = null;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            hoverLight.intensity = Mathf.Lerp(startIntensity, targetIntensity, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        hoverLight.intensity = targetIntensity;
        hoverLightFadeCoroutine = null;
    }

    private void StopHoverLightFade()
    {
        if (hoverLightFadeCoroutine == null)
            return;

        StopCoroutine(hoverLightFadeCoroutine);
        hoverLightFadeCoroutine = null;
    }

    private void CacheOriginalPropertyBlocks()
    {
        int rendererCount = highlightRenderers == null ? 0 : highlightRenderers.Length;
        rendererStates = new RendererHighlightState[rendererCount];

        for (int i = 0; i < rendererCount; i++)
        {
            Renderer renderer = highlightRenderers[i];

            if (renderer == null)
                continue;

            MaterialPropertyBlock originalPropertyBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(originalPropertyBlock);

            rendererStates[i] = new RendererHighlightState(
                renderer,
                originalPropertyBlock,
                !originalPropertyBlock.isEmpty
            );
        }
    }

    private void ApplyHoverVisualState()
    {
        if (rendererStates == null)
            CacheOriginalPropertyBlocks();

        Color emissionColor = hoverEmissionColor * Mathf.Max(0f, hoverEmissionIntensity);
        float baseColorMultiplier = Mathf.Max(1f, hoverBaseColorMultiplier);

        for (int i = 0; i < rendererStates.Length; i++)
        {
            RendererHighlightState rendererState = rendererStates[i];

            if (!rendererState.HasRenderer)
                continue;

            workingPropertyBlock.Clear();
            rendererState.Renderer.GetPropertyBlock(workingPropertyBlock);

            if (ShouldUseEmission(rendererState.Renderer))
            {
                workingPropertyBlock.SetColor(EmissionColorId, emissionColor);
            }
            else if (ShouldUseBaseColor(rendererState.Renderer))
            {
                Color baseColor = GetSharedMaterialColor(rendererState.Renderer, BaseColorId, Color.white);
                workingPropertyBlock.SetColor(BaseColorId, BrightenColor(baseColor, baseColorMultiplier));
            }

            rendererState.Renderer.SetPropertyBlock(workingPropertyBlock);
        }
    }

    private void RestoreOriginalVisualState()
    {
        if (rendererStates == null)
            return;

        for (int i = 0; i < rendererStates.Length; i++)
        {
            RendererHighlightState rendererState = rendererStates[i];

            if (!rendererState.HasRenderer)
                continue;

            if (rendererState.HadOriginalPropertyBlock)
                rendererState.Renderer.SetPropertyBlock(rendererState.OriginalPropertyBlock);
            else
                rendererState.Renderer.SetPropertyBlock(null);
        }
    }

    private bool ShouldUseEmission(Renderer renderer)
    {
        if (hoverMode == HoverMode.BaseColor)
            return false;

        if (!RendererSupportsProperty(renderer, EmissionColorId))
            return false;

        if (hoverMode == HoverMode.Emission)
            return true;

        return RendererHasEmissionEnabled(renderer);
    }

    private bool ShouldUseBaseColor(Renderer renderer)
    {
        if (hoverMode == HoverMode.Emission)
            return !RendererSupportsProperty(renderer, EmissionColorId) && RendererSupportsProperty(renderer, BaseColorId);

        return RendererSupportsProperty(renderer, BaseColorId);
    }

    private bool RendererSupportsProperty(Renderer renderer, int propertyId)
    {
        Material[] sharedMaterials = renderer.sharedMaterials;

        for (int i = 0; i < sharedMaterials.Length; i++)
        {
            Material material = sharedMaterials[i];

            if (material != null && material.HasProperty(propertyId))
                return true;
        }

        return false;
    }

    private bool RendererHasEmissionEnabled(Renderer renderer)
    {
        Material[] sharedMaterials = renderer.sharedMaterials;

        for (int i = 0; i < sharedMaterials.Length; i++)
        {
            Material material = sharedMaterials[i];

            if (material != null && material.HasProperty(EmissionColorId) && material.IsKeywordEnabled(EmissionKeyword))
                return true;
        }

        return false;
    }

    private Color GetSharedMaterialColor(Renderer renderer, int propertyId, Color fallback)
    {
        Material[] sharedMaterials = renderer.sharedMaterials;

        for (int i = 0; i < sharedMaterials.Length; i++)
        {
            Material material = sharedMaterials[i];

            if (material != null && material.HasProperty(propertyId))
                return material.GetColor(propertyId);
        }

        return fallback;
    }

    private Color BrightenColor(Color color, float multiplier)
    {
        return new Color(
            Mathf.Clamp01(color.r * multiplier),
            Mathf.Clamp01(color.g * multiplier),
            Mathf.Clamp01(color.b * multiplier),
            color.a
        );
    }

    public enum HoverMode
    {
        Emission,
        BaseColor,
        Automatic
    }

    private readonly struct RendererHighlightState
    {
        public RendererHighlightState(Renderer renderer, MaterialPropertyBlock originalPropertyBlock, bool hadOriginalPropertyBlock)
        {
            Renderer = renderer;
            OriginalPropertyBlock = originalPropertyBlock;
            HadOriginalPropertyBlock = hadOriginalPropertyBlock;
        }

        public Renderer Renderer { get; }
        public MaterialPropertyBlock OriginalPropertyBlock { get; }
        public bool HadOriginalPropertyBlock { get; }
        public bool HasRenderer => Renderer != null;
    }
}
