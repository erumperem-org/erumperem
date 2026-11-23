using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class GraphicsSettingsPanel : MonoBehaviour
{
    private const string OnToggleName = "OnToggle";
    private const string OffToggleName = "OffToggle";
    private const string SimpleQualityToggleName = "SimpleToggle";
    private const string FancyQualityToggleName = "FancyToggle";
    private const int MinimumSettingValue = 1;
    private const int MaximumSettingValue = 100;

    [Header("Controls")]
    [SerializeField] private TMP_Dropdown _resolutionDropdown;
    [SerializeField] private Slider _frameRateSlider;
    [SerializeField] private TMP_Text _frameRateValue;
    [SerializeField] private Slider _brightnessSlider;
    [SerializeField] private TMP_Text _brightnessValue;

    [Header("Toggle Rows")]
    [SerializeField] private GameObject _fullscreenRow;
    [SerializeField] private GameObject _qualityRow;
    [SerializeField] private GameObject _smoothLightingRow;
    [SerializeField] private GameObject _motionBlurRow;
    [SerializeField] private GameObject _screenShakeRow;
    [SerializeField] private GameObject _groundDetailsRow;
    [SerializeField] private GameObject _particleEffectsRow;

    private Toggle _simpleQualityToggle;
    private Toggle _fancyQualityToggle;

    private void Awake()
    {
        ValidateInspectorReferences();

        if (_resolutionDropdown != null)
            _resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);

        if (_frameRateSlider != null)
        {
            _frameRateSlider.minValue = MinimumSettingValue;
            _frameRateSlider.maxValue = MaximumSettingValue;
            _frameRateSlider.wholeNumbers = true;
            _frameRateSlider.onValueChanged.AddListener(OnFrameRateChanged);
        }

        if (_brightnessSlider != null)
        {
            _brightnessSlider.minValue = MinimumSettingValue;
            _brightnessSlider.maxValue = MaximumSettingValue;
            _brightnessSlider.wholeNumbers = true;
            _brightnessSlider.onValueChanged.AddListener(OnBrightnessChanged);
        }

        BindBooleanTogglePair(_fullscreenRow, GraphicsSettingsService.SetFullscreen);
        BindBooleanTogglePair(_smoothLightingRow, GraphicsSettingsService.SetSmoothLighting);
        BindBooleanTogglePair(_motionBlurRow, GraphicsSettingsService.SetMotionBlur);
        BindBooleanTogglePair(_screenShakeRow, GraphicsSettingsService.SetScreenShake);
        BindBooleanTogglePair(_groundDetailsRow, GraphicsSettingsService.SetGroundDetails);
        BindBooleanTogglePair(_particleEffectsRow, GraphicsSettingsService.SetParticleEffects);
        BindQualityTogglePair();
    }

    private void ValidateInspectorReferences()
    {
        ValidateInspectorReference(_resolutionDropdown, nameof(_resolutionDropdown));
        ValidateInspectorReference(_frameRateSlider, nameof(_frameRateSlider));
        ValidateInspectorReference(_frameRateValue, nameof(_frameRateValue));
        ValidateInspectorReference(_brightnessSlider, nameof(_brightnessSlider));
        ValidateInspectorReference(_brightnessValue, nameof(_brightnessValue));
        ValidateInspectorReference(_fullscreenRow, nameof(_fullscreenRow));
        ValidateInspectorReference(_qualityRow, nameof(_qualityRow));
        ValidateInspectorReference(_smoothLightingRow, nameof(_smoothLightingRow));
        ValidateInspectorReference(_motionBlurRow, nameof(_motionBlurRow));
        ValidateInspectorReference(_screenShakeRow, nameof(_screenShakeRow));
        ValidateInspectorReference(_groundDetailsRow, nameof(_groundDetailsRow));
        ValidateInspectorReference(_particleEffectsRow, nameof(_particleEffectsRow));
    }

    private void ValidateInspectorReference(UnityEngine.Object reference, string fieldName)
    {
        if (reference == null)
            Debug.LogWarning($"{nameof(GraphicsSettingsPanel)} on '{name}' is missing Inspector reference '{fieldName}'.", this);
    }

    private void OnEnable()
    {
        RefreshControlsFromPreferences();
    }

    private void Start()
    {
        RefreshControlsFromPreferences();
    }

    private void OnDestroy()
    {
        if (_resolutionDropdown != null)
            _resolutionDropdown.onValueChanged.RemoveListener(OnResolutionChanged);
        if (_frameRateSlider != null)
            _frameRateSlider.onValueChanged.RemoveListener(OnFrameRateChanged);
        if (_brightnessSlider != null)
            _brightnessSlider.onValueChanged.RemoveListener(OnBrightnessChanged);
    }

    private void RefreshControlsFromPreferences()
    {
        RefreshResolutionDropdown();

        if (_frameRateSlider != null)
            _frameRateSlider.SetValueWithoutNotify(GraphicsSettingsService.TargetFrameRate);
        UpdateFrameRateReadout(GraphicsSettingsService.TargetFrameRate);

        if (_brightnessSlider != null)
            _brightnessSlider.SetValueWithoutNotify(GraphicsSettingsService.BrightnessPercent);
        UpdateBrightnessReadout(GraphicsSettingsService.BrightnessPercent);

        SetTogglePair(_fullscreenRow, GraphicsSettingsService.FullscreenEnabled);
        SetTogglePair(_smoothLightingRow, GraphicsSettingsService.SmoothLightingEnabled);
        SetTogglePair(_motionBlurRow, GraphicsSettingsService.MotionBlurEnabled);
        SetTogglePair(_screenShakeRow, GraphicsSettingsService.ScreenShakeEnabled);
        SetTogglePair(_groundDetailsRow, GraphicsSettingsService.GroundDetailsEnabled);
        SetTogglePair(_particleEffectsRow, GraphicsSettingsService.ParticleEffectsEnabled);
        SetQualityToggles(GraphicsSettingsService.FancyQualityEnabled);
    }

    private void RefreshResolutionDropdown()
    {
        if (_resolutionDropdown == null || _resolutionDropdown.options.Count == 0)
            return;

        int selectedIndex = 0;
        for (int index = 0; index < _resolutionDropdown.options.Count; index++)
        {
            if (!TryParseResolution(_resolutionDropdown.options[index].text, out int width, out int height))
                continue;

            if (width == GraphicsSettingsService.ResolutionWidth && height == GraphicsSettingsService.ResolutionHeight)
            {
                selectedIndex = index;
                break;
            }
        }

        _resolutionDropdown.SetValueWithoutNotify(selectedIndex);
        _resolutionDropdown.RefreshShownValue();
    }

    private void OnResolutionChanged(int selectedIndex)
    {
        if (_resolutionDropdown == null || selectedIndex < 0 || selectedIndex >= _resolutionDropdown.options.Count)
            return;

        if (TryParseResolution(_resolutionDropdown.options[selectedIndex].text, out int width, out int height))
            GraphicsSettingsService.SetResolution(width, height);
    }

    private void OnFrameRateChanged(float value)
    {
        int frameRate = Mathf.Clamp(Mathf.RoundToInt(value), MinimumSettingValue, MaximumSettingValue);
        GraphicsSettingsService.SetTargetFrameRate(frameRate);
        UpdateFrameRateReadout(frameRate);
    }

    private void OnBrightnessChanged(float value)
    {
        int brightness = Mathf.Clamp(Mathf.RoundToInt(value), MinimumSettingValue, MaximumSettingValue);
        GraphicsSettingsService.SetBrightness(brightness);
        UpdateBrightnessReadout(brightness);
    }

    private void UpdateFrameRateReadout(int frameRate)
    {
        if (_frameRateValue != null)
            _frameRateValue.text = frameRate.ToString(CultureInfo.InvariantCulture);
    }

    private void UpdateBrightnessReadout(int brightness)
    {
        if (_brightnessValue != null)
            _brightnessValue.text = brightness.ToString(CultureInfo.InvariantCulture) + "%";
    }

    private void BindBooleanTogglePair(GameObject row, Action<bool> onChanged)
    {
        Toggle onToggle = FindToggleInRow(row, OnToggleName);
        Toggle offToggle = FindToggleInRow(row, OffToggleName);

        if (onToggle != null)
            onToggle.onValueChanged.AddListener(isOn => { if (isOn) onChanged(true); });
        if (offToggle != null)
            offToggle.onValueChanged.AddListener(isOn => { if (isOn) onChanged(false); });
    }

    private void BindQualityTogglePair()
    {
        _simpleQualityToggle = FindToggleInRow(_qualityRow, SimpleQualityToggleName);
        _fancyQualityToggle = FindToggleInRow(_qualityRow, FancyQualityToggleName);

        if (_simpleQualityToggle != null)
            _simpleQualityToggle.onValueChanged.AddListener(isOn => { if (isOn) GraphicsSettingsService.SetFancyQuality(false); });
        if (_fancyQualityToggle != null)
            _fancyQualityToggle.onValueChanged.AddListener(isOn => { if (isOn) GraphicsSettingsService.SetFancyQuality(true); });
    }

    private void SetTogglePair(GameObject row, bool enabled)
    {
        Toggle onToggle = FindToggleInRow(row, OnToggleName);
        Toggle offToggle = FindToggleInRow(row, OffToggleName);

        if (onToggle != null)
            onToggle.SetIsOnWithoutNotify(enabled);
        if (offToggle != null)
            offToggle.SetIsOnWithoutNotify(!enabled);
    }

    private void SetQualityToggles(bool fancy)
    {
        if (_simpleQualityToggle != null)
            _simpleQualityToggle.SetIsOnWithoutNotify(!fancy);
        if (_fancyQualityToggle != null)
            _fancyQualityToggle.SetIsOnWithoutNotify(fancy);
    }

    private static Toggle FindToggleInRow(GameObject row, string toggleName)
    {
        if (row == null)
            return null;

        Toggle[] toggles = row.GetComponentsInChildren<Toggle>(true);
        foreach (Toggle toggle in toggles)
        {
            if (toggle.gameObject.name == toggleName)
                return toggle;
        }

        return null;
    }

    private static bool TryParseResolution(string label, out int width, out int height)
    {
        width = 0;
        height = 0;
        string[] dimensions = label.ToLowerInvariant().Split('x');
        if (dimensions.Length != 2)
            return false;

        return int.TryParse(dimensions[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out width)
            && int.TryParse(dimensions[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out height)
            && width > 0 && height > 0;
    }
}
