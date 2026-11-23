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
    private const float DisabledGroundWindMultiplier = 0.15f;
    private const float MotionBlurIntensity = 0.35f;
    private const float PreferenceSaveDelaySeconds = 0.35f;
    private const float ParticleScanIntervalSeconds = 0.75f;
    private const int MaximumFrameRate = 100;
    private const int MinimumFrameRate = 1;
    private const int MaximumBrightness = 100;
    private const int MinimumBrightness = 1;

    private static GraphicsSettingsService _instance;

    private readonly Dictionary<ParticleSystem, ParticleSystemState> _particleSystemStates =
        new Dictionary<ParticleSystem, ParticleSystemState>();

    private VolumeProfile _runtimeVolumeProfile;
    private ColorAdjustments _colorAdjustments;
    private MotionBlur _motionBlur;
    private UniversalAdditionalCameraData _managedCameraData;
    private Camera _managedCamera;
    private bool _managedCameraOriginallyProcessed;
    private bool _enabledPostProcessingForCamera;
    private bool _preferencesDirty;
    private float _savePreferencesAt;
    private float _scanParticlesAt;

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
        if (_runtimeVolumeProfile != null)
            Destroy(_runtimeVolumeProfile);
        _instance = null;
    }

    private void Update()
    {
        if (_preferencesDirty && Time.unscaledTime >= _savePreferencesAt)
            SavePreferences();

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
        EnsureInstance().MarkPreferencesDirty();
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
        _motionBlur.mode.value = MotionBlurMode.CameraOnly;

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
        if (qualityNames == null || qualityNames.Length == 0)
            return;

        int targetQualityIndex = FindQualityIndex(qualityNames, FancyQualityEnabled);
        if (QualitySettings.GetQualityLevel() != targetQualityIndex)
            QualitySettings.SetQualityLevel(targetQualityIndex, true);
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
        QualitySettings.shadows = SmoothLightingEnabled
            ? UnityEngine.ShadowQuality.All
            : UnityEngine.ShadowQuality.HardOnly;
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
            _managedCameraOriginallyProcessed = _managedCameraData != null && _managedCameraData.renderPostProcessing;
            _enabledPostProcessingForCamera = false;
        }

        bool needsPostProcessing = MotionBlurEnabled || BrightnessPercent != DefaultBrightness;
        if (_managedCamera == null)
            return;

        if (_managedCameraData == null && needsPostProcessing)
            _managedCameraData = _managedCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();

        if (_managedCameraData == null)
            return;

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
        if (_managedCameraData != null && _enabledPostProcessingForCamera)
            _managedCameraData.renderPostProcessing = _managedCameraOriginallyProcessed;

        _managedCamera = null;
        _managedCameraData = null;
        _enabledPostProcessingForCamera = false;
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
