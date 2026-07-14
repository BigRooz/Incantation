using UnityEngine;

public class BookMenuReturnInteractable : MonoBehaviour
{
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private const string EmissionKeyword = "_EMISSION";

    [SerializeField] private CameraTransitionManager cameraTransitionManager;
    [SerializeField] private Transform bookMenuTarget;
    [SerializeField] private Renderer[] highlightRenderers;
    [SerializeField] private HoverMode hoverMode = HoverMode.Automatic;
    [SerializeField] private Color hoverEmissionColor = Color.white;
    [SerializeField] private float hoverEmissionIntensity = 0.35f;
    [SerializeField] private float hoverBaseColorMultiplier = 1.2f;
    [SerializeField] private bool interactionEnabled = true;

    private MaterialPropertyBlock workingPropertyBlock;
    private RendererHighlightState[] rendererStates;
    private bool isHovered;

    private void Awake()
    {
        workingPropertyBlock = new MaterialPropertyBlock();
        CacheOriginalPropertyBlocks();
    }

    private void OnDisable()
    {
        RestoreOriginalVisualState();
        isHovered = false;
    }

    private void OnMouseEnter()
    {
        if (!interactionEnabled)
            return;

        isHovered = true;
        ApplyHoverVisualState();
    }

    private void OnMouseExit()
    {
        isHovered = false;
        RestoreOriginalVisualState();
    }

    private void OnMouseDown()
    {
        if (!interactionEnabled || cameraTransitionManager == null)
            return;

        cameraTransitionManager.MoveTo(bookMenuTarget);
    }

    public void SetInteractionEnabled(bool enabled)
    {
        interactionEnabled = enabled;

        if (!interactionEnabled && isHovered)
        {
            isHovered = false;
            RestoreOriginalVisualState();
        }
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
