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
    public const string SmoothLightingPreferenceKey = "Graphics.SmoothLighting";
    public const string MotionBlurPreferenceKey = "Graphics.MotionBlur";
    public const string MotionBlurPercentPreferenceKey = "Graphics.MotionBlurPercent";
    public const string ScreenShakePreferenceKey = "Graphics.ScreenShake";
    public const string GroundDetailsPreferenceKey = "Graphics.GroundDetails";
    public const string ParticleEffectsPreferenceKey = "Graphics.ParticleEffects";

    private const int DefaultResolutionWidth = 1280;
    private const int DefaultResolutionHeight = 720;
    private const int DefaultFrameRate = 60;
    private const int DefaultBrightness = 50;
    private const float MinimumBrightnessExposure = -0.75f;
    private const float MaximumBrightnessExposure = 0.55f;
    private const float DisabledGroundWindMultiplier = 0f;
    private const float MotionBlurClamp = 0.2f;
    private const float ReducedTerrainDetailDensity = 0.3f;
    private const float PreferenceSaveDelaySeconds = 0.35f;
    private const float ParticleScanIntervalSeconds = 0.75f;
    private const float GraphicsScanIntervalSeconds = 2f;
    private const int MaximumFrameRate = 240;
    private const int MinimumFrameRate = 30;
    private const int MaximumBrightness = 100;
    private const int MinimumBrightness = 0;
    private const int MinimumMotionBlurPercent = 0;
    private const int MaximumMotionBlurPercent = 100;

    private static GraphicsSettingsService _instance;

    private readonly Dictionary<ParticleSystem, ParticleSystemState> _particleSystemStates =
        new Dictionary<ParticleSystem, ParticleSystemState>();
    private readonly Dictionary<Light, LightShadows> _originalLightShadowTypes =
        new Dictionary<Light, LightShadows>();
    private readonly Dictionary<Terrain, float> _originalTerrainDensities =
        new Dictionary<Terrain, float>();
    private readonly Dictionary<TerrainData, TerrainGrassState> _originalTerrainGrassStates =
        new Dictionary<TerrainData, TerrainGrassState>();

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

    /// <summary>Returns whether soft shadows are enabled.</summary>
    public static bool SmoothLightingEnabled => ReadBoolean(SmoothLightingPreferenceKey, true);

    /// <summary>Returns the saved motion-blur percentage.</summary>
    public static int MotionBlurPercent => Mathf.Clamp(
        PlayerPrefs.GetInt(MotionBlurPercentPreferenceKey,
            ReadBoolean(MotionBlurPreferenceKey, false) ? MaximumMotionBlurPercent : MinimumMotionBlurPercent),
        MinimumMotionBlurPercent, MaximumMotionBlurPercent);

    /// <summary>Returns whether motion blur is enabled.</summary>
    public static bool MotionBlurEnabled => MotionBlurPercent > MinimumMotionBlurPercent;

    /// <summary>Returns whether camera shake is enabled.</summary>
    public static bool ScreenShakeEnabled => ReadBoolean(ScreenShakePreferenceKey, true);

    /// <summary>Returns whether Terrain details and grass wind are enabled.</summary>
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
        RestoreTerrainDetails();
        if (_runtimeVolumeProfile != null)
            Destroy(_runtimeVolumeProfile);
        _instance = null;
    }

    private void Update()
    {
        if (_preferencesDirty && Time.unscaledTime >= _savePreferencesAt)
            SavePreferences();

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

    /// <summary>Saves and applies the motion-blur percentage.</summary>
    public static void SetMotionBlurPercent(int percentage)
    {
        percentage = Mathf.Clamp(percentage, MinimumMotionBlurPercent, MaximumMotionBlurPercent);
        if (MotionBlurPercent == percentage)
            return;

        PlayerPrefs.SetInt(MotionBlurPercentPreferenceKey, percentage);
        PlayerPrefs.SetInt(MotionBlurPreferenceKey, percentage > MinimumMotionBlurPercent ? 1 : 0);
        GraphicsSettingsService service = EnsureInstance();
        service.MarkPreferencesDirty();
        service.ApplyPostProcessingSettings();
    }

    /// <summary>Saves and applies the motion-blur preference as a full-strength switch.</summary>
    public static void SetMotionBlur(bool enabled)
    {
        SetMotionBlurPercent(enabled ? MaximumMotionBlurPercent : MinimumMotionBlurPercent);
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



    /// <summary>Saves the screen-shake preference for camera-shake systems.</summary>
    public static void SetScreenShake(bool enabled)
    {
        if (ScreenShakeEnabled == enabled)
            return;

        PlayerPrefs.SetInt(ScreenShakePreferenceKey, enabled ? 1 : 0);
        EnsureInstance().MarkPreferencesDirty();
    }

    /// <summary>Saves the Terrain detail density and grass wind preference.</summary>
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
        _motionBlur.active = true;
        _motionBlur.intensity.overrideState = true;
        _motionBlur.mode.overrideState = true;
        _motionBlur.clamp.overrideState = true;
        _motionBlur.mode.value = MotionBlurMode.CameraAndObjects;
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
        Terrain[] terrains = FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        bool enabled = GroundDetailsEnabled;

        foreach (Terrain terrain in terrains)
        {
            if (terrain == null)
                continue;

            if (!_originalTerrainDensities.TryGetValue(terrain, out float originalDensity))
            {
                originalDensity = terrain.detailObjectDensity;
                _originalTerrainDensities.Add(terrain, originalDensity);
            }

            float targetDensity = enabled
                ? originalDensity
                : originalDensity * ReducedTerrainDetailDensity;
            if (!Mathf.Approximately(terrain.detailObjectDensity, targetDensity))
                terrain.detailObjectDensity = targetDensity;

            TerrainData terrainData = terrain.terrainData;
            if (terrainData == null)
                continue;

            if (!_originalTerrainGrassStates.TryGetValue(terrainData, out TerrainGrassState grassState))
            {
                grassState = new TerrainGrassState(
                    terrainData.wavingGrassStrength,
                    terrainData.wavingGrassAmount,
                    terrainData.wavingGrassSpeed);
                _originalTerrainGrassStates.Add(terrainData, grassState);
            }

            float targetStrength = enabled ? grassState.Strength : 0f;
            float targetAmount = enabled ? grassState.Amount : 0f;
            float targetSpeed = enabled ? grassState.Speed : 0f;
            if (!Mathf.Approximately(terrainData.wavingGrassStrength, targetStrength))
                terrainData.wavingGrassStrength = targetStrength;
            if (!Mathf.Approximately(terrainData.wavingGrassAmount, targetAmount))
                terrainData.wavingGrassAmount = targetAmount;
            if (!Mathf.Approximately(terrainData.wavingGrassSpeed, targetSpeed))
                terrainData.wavingGrassSpeed = targetSpeed;
        }

        RemoveDestroyedTerrains();
        RemoveDestroyedTerrainData();
    }

    private void RestoreTerrainDetails()
    {
        foreach (KeyValuePair<Terrain, float> pair in _originalTerrainDensities)
        {
            if (pair.Key != null)
                pair.Key.detailObjectDensity = pair.Value;
        }

        foreach (KeyValuePair<TerrainData, TerrainGrassState> pair in _originalTerrainGrassStates)
        {
            if (pair.Key == null)
                continue;

            pair.Key.wavingGrassStrength = pair.Value.Strength;
            pair.Key.wavingGrassAmount = pair.Value.Amount;
            pair.Key.wavingGrassSpeed = pair.Value.Speed;
        }

        _originalTerrainDensities.Clear();
        _originalTerrainGrassStates.Clear();
    }

    private void RemoveDestroyedTerrains()
    {
        List<Terrain> destroyedTerrains = null;
        foreach (KeyValuePair<Terrain, float> pair in _originalTerrainDensities)
        {
            if (pair.Key != null)
                continue;

            if (destroyedTerrains == null)
                destroyedTerrains = new List<Terrain>();
            destroyedTerrains.Add(pair.Key);
        }

        if (destroyedTerrains == null)
            return;

        foreach (Terrain terrain in destroyedTerrains)
            _originalTerrainDensities.Remove(terrain);
    }

    private void RemoveDestroyedTerrainData()
    {
        List<TerrainData> destroyedTerrainData = null;
        foreach (KeyValuePair<TerrainData, TerrainGrassState> pair in _originalTerrainGrassStates)
        {
            if (pair.Key != null)
                continue;

            if (destroyedTerrainData == null)
                destroyedTerrainData = new List<TerrainData>();
            destroyedTerrainData.Add(pair.Key);
        }

        if (destroyedTerrainData == null)
            return;

        foreach (TerrainData terrainData in destroyedTerrainData)
            _originalTerrainGrassStates.Remove(terrainData);
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
            _motionBlur.active = true;
            _motionBlur.intensity.value = MotionBlurPercent / (float)MaximumMotionBlurPercent;
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

        bool needsPostProcessing = _managedCameraOriginallyProcessed
            || MotionBlurEnabled
            || BrightnessPercent != DefaultBrightness;
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

    private readonly struct TerrainGrassState
    {
        public readonly float Strength;
        public readonly float Amount;
        public readonly float Speed;

        public TerrainGrassState(float strength, float amount, float speed)
        {
            Strength = strength;
            Amount = amount;
            Speed = speed;
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
