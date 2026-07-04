using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Applies a simple transparent darkness veil material to a wall-facing MeshRenderer.
/// Intended for Quad or Plane objects placed near room walls.
/// </summary>
[DisallowMultipleComponent]
public class WallFogPanelPreset : MonoBehaviour
{
    private const string RuntimeMaterialName = "Runtime Wall Fog Panel Material";

    private static readonly string[] ShaderNames =
    {
        "Universal Render Pipeline/Unlit",
        "Universal Render Pipeline/Lit",
        "Unlit/Color",
        "Sprites/Default"
    };

    [SerializeField] private Color fogColor = new Color(0.22f, 0.2f, 0.18f, 1f);
    [Range(0f, 1f)]
    [SerializeField] private float alpha = 0.22f;
    [SerializeField] private bool applyOnStart = true;

    private MeshRenderer meshRenderer;
    private Material runtimeMaterial;

    private void Start()
    {
        if (applyOnStart)
            ApplyPanelPreset();
    }

    private void OnDestroy()
    {
        DestroyRuntimeMaterial();
    }

    private void OnValidate()
    {
        alpha = Mathf.Clamp01(alpha);

        if (runtimeMaterial != null)
            ConfigureMaterial(runtimeMaterial);
    }

    [ContextMenu("Apply Wall Fog Panel Preset")]
    public void ApplyPanelPreset()
    {
        meshRenderer = GetComponent<MeshRenderer>();

        if (meshRenderer == null)
            return;

        if (runtimeMaterial == null)
            runtimeMaterial = CreateRuntimeMaterial();

        if (runtimeMaterial == null)
            return;

        ConfigureMaterial(runtimeMaterial);
        ConfigureRenderer(meshRenderer);
        meshRenderer.sharedMaterial = runtimeMaterial;
    }

    private Material CreateRuntimeMaterial()
    {
        Shader shader = FindCompatibleShader();

        if (shader == null)
        {
            Debug.LogWarning(
                "WallFogPanelPreset could not find a compatible transparent shader. No wall fog material was applied.",
                this);
            return null;
        }

        return new Material(shader)
        {
            name = RuntimeMaterialName,
            hideFlags = HideFlags.DontSave
        };
    }

    private void ConfigureRenderer(MeshRenderer targetRenderer)
    {
        targetRenderer.shadowCastingMode = ShadowCastingMode.Off;
        targetRenderer.receiveShadows = false;
    }

    private void ConfigureMaterial(Material material)
    {
        Color finalColor = fogColor;
        finalColor.a = Mathf.Clamp01(alpha);

        SetColorIfPresent(material, "_BaseColor", finalColor);
        SetColorIfPresent(material, "_Color", finalColor);
        SetFloatIfPresent(material, "_Surface", 1f);
        SetFloatIfPresent(material, "_Blend", 0f);
        SetFloatIfPresent(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
        SetFloatIfPresent(material, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        SetFloatIfPresent(material, "_ZWrite", 0f);
        SetFloatIfPresent(material, "_AlphaClip", 0f);
        SetFloatIfPresent(material, "_Cull", (float)CullMode.Off);
        SetFloatIfPresent(material, "_ReceiveShadows", 0f);

        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetOverrideTag("RenderType", "Transparent");
        material.SetOverrideTag("Queue", "Transparent");
        material.renderQueue = (int)RenderQueue.Transparent;
    }

    private void DestroyRuntimeMaterial()
    {
        if (runtimeMaterial == null)
            return;

        if (Application.isPlaying)
            Destroy(runtimeMaterial);
        else
            DestroyImmediate(runtimeMaterial);
    }

    private static Shader FindCompatibleShader()
    {
        for (int i = 0; i < ShaderNames.Length; i++)
        {
            Shader shader = Shader.Find(ShaderNames[i]);

            if (shader != null && shader.isSupported)
                return shader;
        }

        return null;
    }

    private static void SetColorIfPresent(Material material, string propertyName, Color value)
    {
        if (!material.HasProperty(propertyName))
            return;

        material.SetColor(propertyName, value);
    }

    private static void SetFloatIfPresent(Material material, string propertyName, float value)
    {
        if (!material.HasProperty(propertyName))
            return;

        material.SetFloat(propertyName, value);
    }
}
