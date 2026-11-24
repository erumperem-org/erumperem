using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-10000)]
[DisallowMultipleComponent]
public sealed class GraphicsSettingsService : MonoBehaviour
{
    public const string FullscreenPreferenceKey = "Graphics.Fullscreen";
    public const string ResolutionWidthPreferenceKey = "Graphics.ResolutionWidth";
    public const string ResolutionHeightPreferenceKey = "Graphics.ResolutionHeight";
    public const string FrameRatePreferenceKey = "Graphics.FrameRate";
    public const string BrightnessPreferenceKey = "Graphics.Brightness";
    public const string FancyQualityPreferenceKey = "Graphics.FancyQuality";
    public const string SmoothLightingPreferenceKey = "Graphics.SmoothLighting";
    public const string MotionBlurPreferenceKey = "Graphics.MotionBlur";
    public const string ScreenShakePreferenceKey = "Graphics.ScreenShake";
    public const string GroundDetailsPreferenceKey = "Graphics.GroundDetails";
    public const string ParticleEffectsPreferenceKey = "Graphics.ParticleEffects";

    private const int DefaultResolutionWidth = 1280;
    private const int DefaultResolutionHeight = 720;
    private const int DefaultFrameRate = 60;
    private const int DefaultBrightness = 50;
    private const float MinimumBrightnessExposure = -0.55f;
    private const float MaximumBrightnessExposure = 0.4f;
    private const float DisabledGroundWindMultiplier = 0f;
    private const float MotionBlurIntensity = 1f;
    private const float MotionBlurClamp = 0.2f;
    private const float SimpleQualityLodBias = 0.5f;
    private const float FancyQualityLodBias = 2f;
    private const int SimpleTextureMipmapLimit = 1;
    private const int FancyTextureMipmapLimit = 0;
    private const float PreferenceSaveDelaySeconds = 0.35f;
    private const float ParticleScanIntervalSeconds = 0.75f;
    private const float GraphicsScanIntervalSeconds = 2f;
    private const int MaximumFrameRate = 100;
    private const int MinimumFrameRate = 1;
    private const int MaximumBrightness = 100;
    private const int MinimumBrightness = 1;

    private static readonly int GrassWindForceProperty = Shader.PropertyToID("_Wind_Force");
    private static readonly int LegacyGrassWindStrengthProperty = Shader.PropertyToID("_WindStrength");
    private static readonly int LegacyGrassWindSpeedProperty = Shader.PropertyToID("_WindSpeed");

    private static GraphicsSettingsService _instance;

    private readonly Dictionary<ParticleSystem, ParticleSystemState> _particleSystemStates =
        new Dictionary<ParticleSystem, ParticleSystemState>();
    private readonly Dictionary<Light, LightShadows> _originalLightShadowTypes =
        new Dictionary<Light, LightShadows>();
    private readonly Dictionary<Material, GrassWindMaterialState> _grassWindMaterialStates =
        new Dictionary<Material, GrassWindMaterialState>();

    private VolumeProfile _runtimeVolumeProfile;
    private ColorAdjustments _colorAdjustments;
    private MotionBlur _motionBlur;
    private UniversalAdditionalCameraData _managedCameraData;
    private Camera _managedCamera;
    private LayerMask _managedCameraOriginalVolumeMask;
    private bool _managedCameraOriginallyProcessed;
    private bool _enabledPostProcessingForCamera;
    private bool _modifiedCameraVolumeMask;
    private bool _preferencesDirty;
    private float _savePreferencesAt;
    private float _scanParticlesAt;
    private float _scanGraphicsAt;

    /// <summary>Returns whether fullscreen is enabled in the saved graphics preferences.</summary>
    public static bool FullscreenEnabled => ReadBoolean(FullscreenPreferenceKey, false);

    /// <summary>Returns the saved output width.</summary>
    public static int ResolutionWidth => Mathf.Max(1, PlayerPrefs.GetInt(ResolutionWidthPreferenceKey, DefaultResolutionWidth));

    /// <summary>Returns the saved output height.</summary>
    public static int ResolutionHeight => Mathf.Max(1, PlayerPrefs.GetInt(ResolutionHeightPreferenceKey, DefaultResolutionHeight));

    /// <summary>Returns the saved frame-rate limit, clamped to the supported slider range.</summary>
    public static int TargetFrameRate => Mathf.Clamp(PlayerPrefs.GetInt(FrameRatePreferenceKey, DefaultFrameRate), MinimumFrameRate, MaximumFrameRate);

    /// <summary>Returns the saved brightness percentage, clamped to the supported slider range.</summary>
    public static int BrightnessPercent => Mathf.Clamp(PlayerPrefs.GetInt(BrightnessPreferenceKey, DefaultBrightness), MinimumBrightness, MaximumBrightness);

    /// <summary>Returns whether the Fancy quality preset is selected.</summary>
    public static bool FancyQualityEnabled => ReadBoolean(FancyQualityPreferenceKey, false);

    /// <summary>Returns whether soft shadows are enabled.</summary>
    public static bool SmoothLightingEnabled => ReadBoolean(SmoothLightingPreferenceKey, true);

    /// <summary>Returns whether motion blur is enabled.</summary>
    public static bool MotionBlurEnabled => ReadBoolean(MotionBlurPreferenceKey, false);

    /// <summary>Returns whether camera shake is enabled.</summary>
    public static bool ScreenShakeEnabled => ReadBoolean(ScreenShakePreferenceKey, true);

    /// <summary>Returns whether animated ground details are enabled.</summary>
    public static bool GroundDetailsEnabled => ReadBoolean(GroundDetailsPreferenceKey, true);

    /// <summary>Returns whether particle effects are enabled.</summary>
    public static bool ParticleEffectsEnabled => ReadBoolean(ParticleEffectsPreferenceKey, true);

    /// <summary>Returns the wind multiplier used when ground animation details are reduced.</summary>
    public static float GroundWindMultiplier => GroundDetailsEnabled ? 1f : DisabledGroundWindMultiplier;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    private static GraphicsSettingsService EnsureInstance()
    {
        if (_instance != null)
            return _instance;

        GameObject serviceObject = new GameObject("Graphics Settings Service");
        return serviceObject.AddComponent<GraphicsSettingsService>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        InitializeRuntimeVolume();
        ApplyAllSettings();
    }

    private void OnDestroy()
    {
        if (_instance != this)
            return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        RestoreManagedCameraPostProcessing();
        RestoreOriginalLightShadowTypes();
        RestoreGrassWindMaterials();
        if (_runtimeVolumeProfile != null)
            Destroy(_runtimeVolumeProfile);
        _instance = null;
    }

    private void Update()
    {
        if (_preferencesDirty && Time.unscaledTime >= _savePreferencesAt)
            SavePreferences();

        if (MotionBlurEnabled || BrightnessPercent != DefaultBrightness)
            UpdateMainCameraPostProcessing();

        if (Time.unscaledTime >= _scanGraphicsAt)
        {
            _scanGraphicsAt = Time.unscaledTime + GraphicsScanIntervalSeconds;
            ApplySmoothLighting();
            ApplyGroundDetails();
        }

        if (!ParticleEffectsEnabled && Time.unscaledTime >= _scanParticlesAt)
        {
            _scanParticlesAt = Time.unscaledTime + ParticleScanIntervalSeconds;
            ApplyParticleEffects();
        }
    }

    private void OnApplicationPause(bool isPaused)
    {
        if (isPaused)
            SavePreferences();
    }

    private void OnApplicationQuit()
    {
        SavePreferences();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyAllSettings();
    }

    /// <summary>Saves and applies a selected window resolution.</summary>
    public static void SetResolution(int width, int height)
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);
        if (ResolutionWidth == width && ResolutionHeight == height)
            return;

        PlayerPrefs.SetInt(ResolutionWidthPreferenceKey, width);
        PlayerPrefs.SetInt(ResolutionHeightPreferenceKey, height);
        GraphicsSettingsService service = EnsureInstance();
        service.MarkPreferencesDirty();
        service.ApplyDisplaySettings();
    }

    /// <summary>Saves and applies the fullscreen preference.</summary>
    public static void SetFullscreen(bool enabled)
    {
        if (FullscreenEnabled == enabled)
            return;

        PlayerPrefs.SetInt(FullscreenPreferenceKey, enabled ? 1 : 0);
        GraphicsSettingsService service = EnsureInstance();
        service.MarkPreferencesDirty();
        service.ApplyDisplaySettings();
    }

    /// <summary>Saves and applies the target frame rate.</summary>
    public static void SetTargetFrameRate(int frameRate)
    {
        frameRate = Mathf.Clamp(frameRate, MinimumFrameRate, MaximumFrameRate);
        if (TargetFrameRate == frameRate)
            return;

        PlayerPrefs.SetInt(FrameRatePreferenceKey, frameRate);
        GraphicsSettingsService service = EnsureInstance();
        service.MarkPreferencesDirty();
        service.ApplyFrameRate();
    }

    /// <summary>Saves and applies the brightness percentage.</summary>
    public static void SetBrightness(int percentage)
    {
        percentage = Mathf.Clamp(percentage, MinimumBrightness, MaximumBrightness);
        if (BrightnessPercent == percentage)
            return;

        PlayerPrefs.SetInt(BrightnessPreferenceKey, percentage);
        GraphicsSettingsService service = EnsureInstance();
        service.MarkPreferencesDirty();
        service.ApplyPostProcessingSettings();
    }

    /// <summary>Saves and applies the Simple or Fancy quality preference.</summary>
    public static void SetFancyQuality(bool enabled)
    {
        if (FancyQualityEnabled == enabled)
            return;

        PlayerPrefs.SetInt(FancyQualityPreferenceKey, enabled ? 1 : 0);
        GraphicsSettingsService service = EnsureInstance();
        service.MarkPreferencesDirty();
        service.ApplyQualitySettings();
        service.ApplySmoothLighting();
        service.ApplyFrameRate();
    }

    /// <summary>Saves and applies the smooth-lighting preference.</summary>
    public static void SetSmoothLighting(bool enabled)
    {
        if (SmoothLightingEnabled == enabled)
            return;

        PlayerPrefs.SetInt(SmoothLightingPreferenceKey, enabled ? 1 : 0);
        GraphicsSettingsService service = EnsureInstance();
        service.MarkPreferencesDirty();
        service.ApplySmoothLighting();
    }

    /// <summary>Saves and applies the motion-blur preference.</summary>
    public static void SetMotionBlur(bool enabled)
    {
        if (MotionBlurEnabled == enabled)
            return;

        PlayerPrefs.SetInt(MotionBlurPreferenceKey, enabled ? 1 : 0);
        GraphicsSettingsService service = EnsureInstance();
        service.MarkPreferencesDirty();
        service.ApplyPostProcessingSettings();
    }

    /// <summary>Saves the screen-shake preference for camera-shake systems.</summary>
    public static void SetScreenShake(bool enabled)
    {
        if (ScreenShakeEnabled == enabled)
            return;

        PlayerPrefs.SetInt(ScreenShakePreferenceKey, enabled ? 1 : 0);
        EnsureInstance().MarkPreferencesDirty();
    }

    /// <summary>Saves the ground-detail preference used by the grass wind controller.</summary>
    public static void SetGroundDetails(bool enabled)
    {
        if (GroundDetailsEnabled == enabled)
            return;

        PlayerPrefs.SetInt(GroundDetailsPreferenceKey, enabled ? 1 : 0);
        GraphicsSettingsService service = EnsureInstance();
        service.MarkPreferencesDirty();
        service.ApplyGroundDetails();
    }

    /// <summary>Saves the particle-effects preference and updates loaded particle systems.</summary>
    public static void SetParticleEffects(bool enabled)
    {
        if (ParticleEffectsEnabled == enabled)
            return;

        GraphicsSettingsService service = EnsureInstance();
        if (!enabled)
            service.CaptureParticleSystemStates();

        PlayerPrefs.SetInt(ParticleEffectsPreferenceKey, enabled ? 1 : 0);
        service.MarkPreferencesDirty();
        service.ApplyParticleEffects();
    }

    private static bool ReadBoolean(string key, bool defaultValue)
    {
        return PlayerPrefs.GetInt(key, defaultValue ? 1 : 0) == 1;
    }

    private void InitializeRuntimeVolume()
    {
        _runtimeVolumeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        _runtimeVolumeProfile.name = "Runtime Graphics Settings Profile";

        _colorAdjustments = _runtimeVolumeProfile.Add<ColorAdjustments>(false);
        _colorAdjustments.active = true;
        _colorAdjustments.postExposure.overrideState = true;

        _motionBlur = _runtimeVolumeProfile.Add<MotionBlur>(false);
        _motionBlur.active = false;
        _motionBlur.intensity.overrideState = true;
        _motionBlur.mode.overrideState = true;
        _motionBlur.clamp.overrideState = true;
        _motionBlur.mode.value = MotionBlurMode.CameraOnly;
        _motionBlur.clamp.value = MotionBlurClamp;

        Volume runtimeVolume = gameObject.AddComponent<Volume>();
        runtimeVolume.isGlobal = true;
        runtimeVolume.priority = 10000f;
        runtimeVolume.weight = 1f;
        runtimeVolume.sharedProfile = _runtimeVolumeProfile;
    }

    private void ApplyAllSettings()
    {
        ApplyDisplaySettings();
        ApplyQualitySettings();
        ApplySmoothLighting();
        ApplyGroundDetails();
        ApplyFrameRate();
        ApplyPostProcessingSettings();
        ApplyParticleEffects();
    }

    private void ApplyDisplaySettings()
    {
        int width = ResolutionWidth;
        int height = ResolutionHeight;
        FullScreenMode mode = FullscreenEnabled
            ? FullScreenMode.ExclusiveFullScreen
            : FullScreenMode.Windowed;

        if (Screen.width != width || Screen.height != height || Screen.fullScreenMode != mode)
            Screen.SetResolution(width, height, mode);
    }

    private void ApplyFrameRate()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = TargetFrameRate;
    }

    private void ApplyQualitySettings()
    {
        string[] qualityNames = QualitySettings.names;
        if (qualityNames != null && qualityNames.Length > 0)
        {
            int targetQualityIndex = FindQualityIndex(qualityNames, FancyQualityEnabled);
            if (QualitySettings.GetQualityLevel() != targetQualityIndex)
                QualitySettings.SetQualityLevel(targetQualityIndex, true);
        }

        bool fancy = FancyQualityEnabled;
        QualitySettings.globalTextureMipmapLimit = fancy ? FancyTextureMipmapLimit : SimpleTextureMipmapLimit;
        QualitySettings.lodBias = fancy ? FancyQualityLodBias : SimpleQualityLodBias;
        QualitySettings.shadowResolution = fancy
    ? UnityEngine.ShadowResolution.VeryHigh
    : UnityEngine.ShadowResolution.Low;
        QualitySettings.anisotropicFiltering = fancy
            ? AnisotropicFiltering.ForceEnable
            : AnisotropicFiltering.Disable;
    }

    private static int FindQualityIndex(string[] qualityNames, bool fancy)
    {
        string preferredName = fancy ? "pc" : "mobile";
        for (int index = 0; index < qualityNames.Length; index++)
        {
            if (qualityNames[index].ToLowerInvariant().Contains(preferredName))
                return index;
        }

        return fancy ? qualityNames.Length - 1 : 0;
    }

    private void ApplySmoothLighting()
    {
        bool enabled = SmoothLightingEnabled;
        QualitySettings.shadows = enabled
            ? UnityEngine.ShadowQuality.All
            : UnityEngine.ShadowQuality.HardOnly;

        Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (Light light in lights)
        {
            if (light == null)
                continue;

            if (!_originalLightShadowTypes.TryGetValue(light, out LightShadows originalShadowType))
            {
                originalShadowType = light.shadows;
                _originalLightShadowTypes.Add(light, originalShadowType);
            }

            if (originalShadowType == LightShadows.None)
                continue;

            light.shadows = enabled ? LightShadows.Soft : LightShadows.Hard;
        }

        RemoveDestroyedLights();
    }

    private void RestoreOriginalLightShadowTypes()
    {
        foreach (KeyValuePair<Light, LightShadows> pair in _originalLightShadowTypes)
        {
            if (pair.Key != null)
                pair.Key.shadows = pair.Value;
        }

        _originalLightShadowTypes.Clear();
    }

    private void RemoveDestroyedLights()
    {
        List<Light> destroyedLights = null;
        foreach (KeyValuePair<Light, LightShadows> pair in _originalLightShadowTypes)
        {
            if (pair.Key != null)
                continue;

            if (destroyedLights == null)
                destroyedLights = new List<Light>();
            destroyedLights.Add(pair.Key);
        }

        if (destroyedLights == null)
            return;

        foreach (Light light in destroyedLights)
            _originalLightShadowTypes.Remove(light);
    }

    private void ApplyGroundDetails()
    {
        Material[] materials = Resources.FindObjectsOfTypeAll<Material>();
        bool enabled = GroundDetailsEnabled;

        foreach (Material material in materials)
        {
            if (material == null || material.name.IndexOf("grass", StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            if (!_grassWindMaterialStates.TryGetValue(material, out GrassWindMaterialState state))
            {
                state = CaptureGrassWindMaterialState(material);
                if (!state.HasWindProperties)
                    continue;

                _grassWindMaterialStates.Add(material, state);
            }

            ApplyGrassWindMaterialState(material, state, enabled);
        }

        RemoveDestroyedGrassMaterials();
    }

    private static GrassWindMaterialState CaptureGrassWindMaterialState(Material material)
    {
        bool hasWindForce = material.HasProperty(GrassWindForceProperty);
        bool hasWindStrength = material.HasProperty(LegacyGrassWindStrengthProperty);
        bool hasWindSpeed = material.HasProperty(LegacyGrassWindSpeedProperty);

        return new GrassWindMaterialState(
            hasWindForce,
            hasWindForce ? material.GetFloat(GrassWindForceProperty) : 0f,
            hasWindStrength,
            hasWindStrength ? material.GetFloat(LegacyGrassWindStrengthProperty) : 0f,
            hasWindSpeed,
            hasWindSpeed ? material.GetFloat(LegacyGrassWindSpeedProperty) : 0f);
    }

    private static void ApplyGrassWindMaterialState(Material material, GrassWindMaterialState state, bool enabled)
    {
        if (state.HasWindForce)
            material.SetFloat(GrassWindForceProperty, enabled ? state.WindForce : 0f);
        if (state.HasWindStrength)
            material.SetFloat(LegacyGrassWindStrengthProperty, enabled ? state.WindStrength : 0f);
        if (state.HasWindSpeed)
            material.SetFloat(LegacyGrassWindSpeedProperty, enabled ? state.WindSpeed : 0f);
    }

    private void RestoreGrassWindMaterials()
    {
        foreach (KeyValuePair<Material, GrassWindMaterialState> pair in _grassWindMaterialStates)
        {
            if (pair.Key != null)
                ApplyGrassWindMaterialState(pair.Key, pair.Value, true);
        }

        _grassWindMaterialStates.Clear();
    }

    private void RemoveDestroyedGrassMaterials()
    {
        List<Material> destroyedMaterials = null;
        foreach (KeyValuePair<Material, GrassWindMaterialState> pair in _grassWindMaterialStates)
        {
            if (pair.Key != null)
                continue;

            if (destroyedMaterials == null)
                destroyedMaterials = new List<Material>();
            destroyedMaterials.Add(pair.Key);
        }

        if (destroyedMaterials == null)
            return;

        foreach (Material material in destroyedMaterials)
            _grassWindMaterialStates.Remove(material);
    }

    private void ApplyPostProcessingSettings()
    {
        if (_colorAdjustments != null)
        {
            _colorAdjustments.active = BrightnessPercent != DefaultBrightness;
            _colorAdjustments.postExposure.value = GetBrightnessExposure(BrightnessPercent);
        }

        if (_motionBlur != null)
        {
            _motionBlur.active = MotionBlurEnabled;
            _motionBlur.intensity.value = MotionBlurEnabled ? MotionBlurIntensity : 0f;
            _motionBlur.clamp.value = MotionBlurClamp;
        }

        UpdateMainCameraPostProcessing();
    }

    private void UpdateMainCameraPostProcessing()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != _managedCamera)
        {
            RestoreManagedCameraPostProcessing();
            _managedCamera = mainCamera;
            _managedCameraData = mainCamera != null
                ? mainCamera.GetComponent<UniversalAdditionalCameraData>()
                : null;
            _managedCameraOriginalVolumeMask = default;
            if (_managedCameraData != null)
            {
                _managedCameraOriginallyProcessed = _managedCameraData.renderPostProcessing;
                _managedCameraOriginalVolumeMask = _managedCameraData.volumeLayerMask;
            }
            _enabledPostProcessingForCamera = false;
        }

        bool needsPostProcessing = MotionBlurEnabled || BrightnessPercent != DefaultBrightness;
        if (_managedCamera == null)
            return;

        if (_managedCameraData == null && needsPostProcessing)
        {
            _managedCameraData = _managedCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            _managedCameraOriginallyProcessed = _managedCameraData.renderPostProcessing;
            _managedCameraOriginalVolumeMask = _managedCameraData.volumeLayerMask;
        }

        if (_managedCameraData == null)
            return;

        int runtimeVolumeLayer = 1 << gameObject.layer;
        LayerMask currentVolumeMask = _managedCameraData.volumeLayerMask;
        bool runtimeVolumeLayerIncluded = (currentVolumeMask.value & runtimeVolumeLayer) != 0;
        if (needsPostProcessing && !runtimeVolumeLayerIncluded)
        {
            _managedCameraOriginalVolumeMask = currentVolumeMask;
            LayerMask expandedVolumeMask = currentVolumeMask;
            expandedVolumeMask.value |= runtimeVolumeLayer;
            _managedCameraData.volumeLayerMask = expandedVolumeMask;
            _modifiedCameraVolumeMask = true;
        }
        else if (!needsPostProcessing && _modifiedCameraVolumeMask)
        {
            _managedCameraData.volumeLayerMask = _managedCameraOriginalVolumeMask;
            _modifiedCameraVolumeMask = false;
        }

        if (needsPostProcessing && !_managedCameraData.renderPostProcessing)
        {
            _managedCameraData.renderPostProcessing = true;
            _enabledPostProcessingForCamera = true;
        }
        else if (!needsPostProcessing && _enabledPostProcessingForCamera)
        {
            _managedCameraData.renderPostProcessing = _managedCameraOriginallyProcessed;
            _enabledPostProcessingForCamera = false;
        }
    }

    private void RestoreManagedCameraPostProcessing()
    {
        if (_managedCameraData != null)
        {
            if (_enabledPostProcessingForCamera)
                _managedCameraData.renderPostProcessing = _managedCameraOriginallyProcessed;
            if (_modifiedCameraVolumeMask)
                _managedCameraData.volumeLayerMask = _managedCameraOriginalVolumeMask;
        }

        _managedCamera = null;
        _managedCameraData = null;
        _enabledPostProcessingForCamera = false;
        _modifiedCameraVolumeMask = false;
    }

    private static float GetBrightnessExposure(int percentage)
    {
        float normalized = Mathf.InverseLerp(MinimumBrightness, MaximumBrightness, percentage);
        float neutralPoint = Mathf.InverseLerp(MinimumBrightness, MaximumBrightness, DefaultBrightness);

        if (normalized <= neutralPoint)
            return Mathf.Lerp(MinimumBrightnessExposure, 0f, normalized / neutralPoint);

        return Mathf.Lerp(0f, MaximumBrightnessExposure,
            (normalized - neutralPoint) / (1f - neutralPoint));
    }



    private void ApplyParticleEffects()
    {
        ParticleSystem[] particleSystems = FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
        foreach (ParticleSystem particleSystem in particleSystems)
        {
            if (particleSystem == null)
                continue;

            if (!_particleSystemStates.TryGetValue(particleSystem, out ParticleSystemState originalState))
            {
                originalState = CaptureParticleSystemState(particleSystem);
                _particleSystemStates.Add(particleSystem, originalState);
            }
            else if (!ParticleEffectsEnabled && particleSystem.isPlaying && !originalState.WasPlaying)
            {
                originalState = CaptureParticleSystemState(particleSystem);
                _particleSystemStates[particleSystem] = originalState;
            }

            ApplyParticleState(particleSystem, originalState, ParticleEffectsEnabled);
        }

        RemoveDestroyedParticleSystems();
        _scanParticlesAt = Time.unscaledTime + ParticleScanIntervalSeconds;
    }

    private void CaptureParticleSystemStates()
    {
        ParticleSystem[] particleSystems = FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
        foreach (ParticleSystem particleSystem in particleSystems)
        {
            if (particleSystem != null)
                _particleSystemStates[particleSystem] = CaptureParticleSystemState(particleSystem);
        }
    }

    private static ParticleSystemState CaptureParticleSystemState(ParticleSystem particleSystem)
    {
        ParticleSystemRenderer particleRenderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        return new ParticleSystemState(
            particleSystem.emission.enabled,
            particleRenderer == null || particleRenderer.enabled,
            particleSystem.isPlaying);
    }

    private static void ApplyParticleState(ParticleSystem particleSystem, ParticleSystemState originalState, bool enabled)
    {
        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.enabled = enabled && originalState.EmissionEnabled;

        ParticleSystemRenderer particleRenderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        if (particleRenderer != null)
            particleRenderer.enabled = enabled && originalState.RendererEnabled;

        if (!enabled)
        {
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        else if (originalState.WasPlaying && !particleSystem.isPlaying)
        {
            particleSystem.Play(true);
        }
    }

    private void RemoveDestroyedParticleSystems()
    {
        List<ParticleSystem> destroyedSystems = null;
        foreach (KeyValuePair<ParticleSystem, ParticleSystemState> pair in _particleSystemStates)
        {
            if (pair.Key != null)
                continue;

            if (destroyedSystems == null)
                destroyedSystems = new List<ParticleSystem>();
            destroyedSystems.Add(pair.Key);
        }

        if (destroyedSystems == null)
            return;

        foreach (ParticleSystem particleSystem in destroyedSystems)
            _particleSystemStates.Remove(particleSystem);
    }

    private void MarkPreferencesDirty()
    {
        _preferencesDirty = true;
        _savePreferencesAt = Time.unscaledTime + PreferenceSaveDelaySeconds;
    }

    private void SavePreferences()
    {
        if (!_preferencesDirty)
            return;

        PlayerPrefs.Save();
        _preferencesDirty = false;
    }

    private sealed class GrassWindMaterialState
    {
        public readonly bool HasWindForce;
        public readonly float WindForce;
        public readonly bool HasWindStrength;
        public readonly float WindStrength;
        public readonly bool HasWindSpeed;
        public readonly float WindSpeed;
        public bool HasWindProperties => HasWindForce || HasWindStrength || HasWindSpeed;

        public GrassWindMaterialState(bool hasWindForce, float windForce, bool hasWindStrength,
            float windStrength, bool hasWindSpeed, float windSpeed)
        {
            HasWindForce = hasWindForce;
            WindForce = windForce;
            HasWindStrength = hasWindStrength;
            WindStrength = windStrength;
            HasWindSpeed = hasWindSpeed;
            WindSpeed = windSpeed;
        }
    }

    private readonly struct ParticleSystemState
    {
        public readonly bool EmissionEnabled;
        public readonly bool RendererEnabled;
        public readonly bool WasPlaying;

        public ParticleSystemState(bool emissionEnabled, bool rendererEnabled, bool wasPlaying)
        {
            EmissionEnabled = emissionEnabled;
            RendererEnabled = rendererEnabled;
            WasPlaying = wasPlaying;
        }
    }
}
