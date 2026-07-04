using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Applies a reusable low table fog preset to the attached ParticleSystem.
/// Depends only on the ParticleSystem on the same GameObject and does not own gameplay state.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ParticleSystem))]
public class TableFogPreset : MonoBehaviour
{
    private const float Duration = 20f;
    private const int MaxParticles = 350;
    private const string RuntimeSmokeMaterialName = "Runtime Table Fog Smoke Material";

    private static readonly string[] SmokeShaderNames =
    {
        "Universal Render Pipeline/Particles/Unlit",
        "Universal Render Pipeline/Unlit",
        "Particles/Standard Unlit",
        "Sprites/Default"
    };

    private static readonly Color MinSmokeColor = new Color(0.34f, 0.31f, 0.28f, 0.08f);
    private static readonly Color MaxSmokeColor = new Color(0.47f, 0.43f, 0.38f, 0.14f);

    [Header("Optional Texture")]
    [SerializeField] private Texture2D smokeTexture;

    private ParticleSystem fogParticles;
    private Material runtimeSmokeMaterial;

    private void Reset()
    {
        ApplyTableFogPreset();
    }

    [ContextMenu("Apply Table Fog Preset")]
    public void ApplyTableFogPreset()
    {
        fogParticles = GetComponent<ParticleSystem>();

        ConfigureMain();
        ConfigureEmission();
        ConfigureShape();
        ConfigureVelocityOverLifetime();
        ConfigureNoise();
        ConfigureColorOverLifetime();
        ConfigureSizeOverLifetime();
        DisableUnusedModules();
        ConfigureRenderer();
    }

    private void ConfigureMain()
    {
        ParticleSystem.MainModule main = fogParticles.main;
        main.duration = Duration;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(10f, 16f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
        main.startColor = new ParticleSystem.MinMaxGradient(MinSmokeColor, MaxSmokeColor);
        main.gravityModifier = new ParticleSystem.MinMaxCurve(-0.02f, -0.005f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = MaxParticles;
    }

    private void ConfigureEmission()
    {
        ParticleSystem.EmissionModule emission = fogParticles.emission;
        emission.enabled = true;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(4f, 8f);
        emission.rateOverDistance = 0f;
    }

    private void ConfigureShape()
    {
        ParticleSystem.ShapeModule shape = fogParticles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 1.5f;
        shape.radiusThickness = 0.25f;
        shape.arc = 360f;
        shape.rotation = new Vector3(90f, 0f, 0f);
        shape.randomDirectionAmount = 0.08f;
    }

    private void ConfigureVelocityOverLifetime()
    {
        ParticleSystem.VelocityOverLifetimeModule velocity = fogParticles.velocityOverLifetime;
        velocity.enabled = false;
    }

    private void ConfigureNoise()
    {
        ParticleSystem.NoiseModule noise = fogParticles.noise;
        noise.enabled = true;
        noise.strength = new ParticleSystem.MinMaxCurve(0.05f);
        noise.frequency = 0.16f;
        noise.scrollSpeed = new ParticleSystem.MinMaxCurve(0.05f);
        noise.damping = true;
        noise.octaveCount = 1;
    }

    private void ConfigureColorOverLifetime()
    {
        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = fogParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(CreateFogGradient());
    }

    private void ConfigureSizeOverLifetime()
    {
        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = fogParticles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, CreateSizeCurve());
    }

    private void DisableUnusedModules()
    {
        ParticleSystem.LimitVelocityOverLifetimeModule limitVelocity = fogParticles.limitVelocityOverLifetime;
        limitVelocity.enabled = false;

        ParticleSystem.InheritVelocityModule inheritVelocity = fogParticles.inheritVelocity;
        inheritVelocity.enabled = false;

        ParticleSystem.ForceOverLifetimeModule forceOverLifetime = fogParticles.forceOverLifetime;
        forceOverLifetime.enabled = false;

        ParticleSystem.ColorBySpeedModule colorBySpeed = fogParticles.colorBySpeed;
        colorBySpeed.enabled = false;

        ParticleSystem.SizeBySpeedModule sizeBySpeed = fogParticles.sizeBySpeed;
        sizeBySpeed.enabled = false;

        ParticleSystem.RotationOverLifetimeModule rotationOverLifetime = fogParticles.rotationOverLifetime;
        rotationOverLifetime.enabled = false;

        ParticleSystem.RotationBySpeedModule rotationBySpeed = fogParticles.rotationBySpeed;
        rotationBySpeed.enabled = false;

        ParticleSystem.ExternalForcesModule externalForces = fogParticles.externalForces;
        externalForces.enabled = false;

        ParticleSystem.CollisionModule collision = fogParticles.collision;
        collision.enabled = false;

        ParticleSystem.TriggerModule trigger = fogParticles.trigger;
        trigger.enabled = false;

        ParticleSystem.TextureSheetAnimationModule textureSheetAnimation = fogParticles.textureSheetAnimation;
        textureSheetAnimation.enabled = false;

        ParticleSystem.LightsModule lights = fogParticles.lights;
        lights.enabled = false;

        ParticleSystem.TrailModule trails = fogParticles.trails;
        trails.enabled = false;

        ParticleSystem.CustomDataModule customData = fogParticles.customData;
        customData.enabled = false;
    }

    private void ConfigureRenderer()
    {
        ParticleSystemRenderer particleRenderer = fogParticles.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.alignment = ParticleSystemRenderSpace.View;
        particleRenderer.sortMode = ParticleSystemSortMode.Distance;
        particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;
        particleRenderer.allowRoll = true;

        bool hasGeneratedMaterial = IsGeneratedSmokeMaterial(particleRenderer.sharedMaterial);

        if (smokeTexture != null && TryAssignSmokeTextureMaterial(particleRenderer))
            return;

        if (particleRenderer.sharedMaterial == null || hasGeneratedMaterial)
            particleRenderer.sharedMaterial = FindBuiltinParticleMaterial();
    }

    private bool TryAssignSmokeTextureMaterial(ParticleSystemRenderer particleRenderer)
    {
        Shader smokeShader = FindCompatibleSmokeShader();

        if (smokeShader == null)
        {
            Debug.LogWarning(
                "TableFogPreset could not find a compatible smoke particle shader. Keeping the existing particle material.",
                this);
            return false;
        }

        DestroyGeneratedMaterial(particleRenderer.sharedMaterial);

        runtimeSmokeMaterial = new Material(smokeShader)
        {
            name = RuntimeSmokeMaterialName,
            hideFlags = HideFlags.DontSave
        };

        ConfigureSmokeMaterial(runtimeSmokeMaterial);
        particleRenderer.sharedMaterial = runtimeSmokeMaterial;
        return true;
    }

    private static Shader FindCompatibleSmokeShader()
    {
        for (int i = 0; i < SmokeShaderNames.Length; i++)
        {
            Shader shader = Shader.Find(SmokeShaderNames[i]);

            if (shader != null && shader.isSupported)
                return shader;
        }

        return null;
    }

    private static bool IsGeneratedSmokeMaterial(Material material)
    {
        return material != null
            && material.name == RuntimeSmokeMaterialName
            && (material.hideFlags & HideFlags.DontSave) != 0;
    }

    private static void DestroyGeneratedMaterial(Material material)
    {
        if (!IsGeneratedSmokeMaterial(material))
            return;

        if (Application.isPlaying)
            Object.Destroy(material);
        else
            Object.DestroyImmediate(material);
    }

    private void ConfigureSmokeMaterial(Material material)
    {
        SetTextureIfPresent(material, "_BaseMap", smokeTexture);
        SetTextureIfPresent(material, "_MainTex", smokeTexture);
        SetColorIfPresent(material, "_BaseColor", Color.white);
        SetColorIfPresent(material, "_Color", Color.white);
        SetFloatIfPresent(material, "_Surface", 1f);
        SetFloatIfPresent(material, "_Blend", 0f);
        SetFloatIfPresent(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
        SetFloatIfPresent(material, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        SetFloatIfPresent(material, "_ZWrite", 0f);
        SetFloatIfPresent(material, "_ReceiveShadows", 0f);

        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetOverrideTag("RenderType", "Transparent");
        material.SetOverrideTag("Queue", "Transparent");
        material.renderQueue = (int)RenderQueue.Transparent;
    }

    private static void SetTextureIfPresent(Material material, string propertyName, Texture texture)
    {
        if (!material.HasProperty(propertyName))
            return;

        material.SetTexture(propertyName, texture);
    }

    private static void SetColorIfPresent(Material material, string propertyName, Color color)
    {
        if (!material.HasProperty(propertyName))
            return;

        material.SetColor(propertyName, color);
    }

    private static void SetFloatIfPresent(Material material, string propertyName, float value)
    {
        if (!material.HasProperty(propertyName))
            return;

        material.SetFloat(propertyName, value);
    }

    private static Gradient CreateFogGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.34f, 0.31f, 0.28f), 0f),
                new GradientColorKey(new Color(0.46f, 0.42f, 0.37f), 0.45f),
                new GradientColorKey(new Color(0.34f, 0.31f, 0.28f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.08f, 0.18f),
                new GradientAlphaKey(0.13f, 0.55f),
                new GradientAlphaKey(0f, 1f)
            });

        return gradient;
    }

    private static AnimationCurve CreateSizeCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, 0.65f),
            new Keyframe(0.45f, 1.08f),
            new Keyframe(1f, 0.9f));
    }

    private static Material FindBuiltinParticleMaterial()
    {
        Material material = Resources.GetBuiltinResource<Material>("Default-ParticleSystem.mat");

        if (material != null)
            return material;

        return Resources.GetBuiltinResource<Material>("Default-Particle.mat");
    }
}
