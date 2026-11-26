using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class GameplaySettingsPanel : MonoBehaviour
{
    private const string ExplorationFieldOfViewLabel = "field of view exploration";
    private const string CombatFieldOfViewLabel = "field of view combat";
    private const string HideHudLabel = "hide hud";
    private const string HideCursorLabel = "hide cursor";
    private const string SceneChangeCheatLabel = "switch scenes";
    private const string WinCombatCheatLabel = "win combat";
    private const string LoseCombatCheatLabel = "lose combat";
    private const string InfiniteHealthCheatLabel = "ally health";
    private const string DoubleDamageCheatLabel = "double damage";
    private const string OnToggleName = "OnToggle";
    private const string OffToggleName = "OffToggle";
    private const float MinimumExplorationFieldOfView = 40f;
    private const float MaximumExplorationFieldOfView = 80f;
    private const float MinimumCombatFieldOfView = 65f;
    private const float MaximumCombatFieldOfView = 100f;

    private readonly List<ToggleListener> _toggleListeners = new List<ToggleListener>();
    private readonly List<SliderListener> _sliderListeners = new List<SliderListener>();

    private void OnEnable()
    {
        BindFieldOfView(ExplorationFieldOfViewLabel, MinimumExplorationFieldOfView,
            MaximumExplorationFieldOfView, GameplaySettingsService.ExplorationFieldOfView,
            GameplaySettingsService.SetExplorationFieldOfView);
        BindFieldOfView(CombatFieldOfViewLabel, MinimumCombatFieldOfView,
            MaximumCombatFieldOfView, GameplaySettingsService.CombatFieldOfView,
            GameplaySettingsService.SetCombatFieldOfView);

        BindTogglePreference(HideHudLabel, GameplaySettingsService.HideHudEnabled,
            GameplaySettingsService.SetHideHudEnabled);
        BindTogglePreference(HideCursorLabel, GameplaySettingsService.HideCustomCursorEnabled,
            GameplaySettingsService.SetHideCustomCursorEnabled);
        BindCheatToggle(SceneChangeCheatLabel, GameplaySettingsService.SceneChangeCheatEnabled,
            GameplaySettingsService.SetSceneChangeCheatEnabled);
        BindCheatToggle(WinCombatCheatLabel, GameplaySettingsService.WinCombatCheatEnabled,
            GameplaySettingsService.SetWinCombatCheatEnabled);
        BindCheatToggle(LoseCombatCheatLabel, GameplaySettingsService.LoseCombatCheatEnabled,
            GameplaySettingsService.SetLoseCombatCheatEnabled);
        BindTogglePreference(InfiniteHealthCheatLabel, GameplaySettingsService.InfiniteAllyHealthEnabled,
            GameplaySettingsService.SetInfiniteAllyHealthEnabled);
        BindTogglePreference(DoubleDamageCheatLabel, GameplaySettingsService.DoubleAllyDamageEnabled,
            GameplaySettingsService.SetDoubleAllyDamageEnabled);
    }

    private void OnDisable()
    {
        foreach (ToggleListener listener in _toggleListeners)
            listener.Toggle.onValueChanged.RemoveListener(listener.Callback);
        _toggleListeners.Clear();

        foreach (SliderListener listener in _sliderListeners)
            listener.Slider.onValueChanged.RemoveListener(listener.Callback);
        _sliderListeners.Clear();
    }

    private void BindFieldOfView(string labelFragment, float minimum, float maximum, float value,
        Action<float> onChanged)
    {
        Transform row = FindRow(labelFragment);
        if (row == null)
            return;

        Slider slider = row.GetComponentInChildren<Slider>(true);
        if (slider == null)
        {
            Debug.LogWarning($"{nameof(GameplaySettingsPanel)} could not find a Slider for '{labelFragment}'.", this);
            return;
        }

        slider.minValue = minimum;
        slider.maxValue = maximum;
        slider.wholeNumbers = true;
        slider.SetValueWithoutNotify(value);
        UpdateFieldOfViewReadout(row, value);

        UnityAction<float> callback = changedValue =>
        {
            float roundedValue = Mathf.Clamp(Mathf.Round(changedValue), minimum, maximum);
            onChanged(roundedValue);
            UpdateFieldOfViewReadout(row, roundedValue);
        };
        slider.onValueChanged.AddListener(callback);
        _sliderListeners.Add(new SliderListener(slider, callback));
    }

    private void BindTogglePreference(string labelFragment, bool initialValue, Action<bool> onChanged)
    {
        Transform row = FindRow(labelFragment);
        if (row == null)
            return;

        Toggle[] toggles = row.GetComponentsInChildren<Toggle>(true);
        if (toggles.Length == 0)
        {
            Debug.LogWarning($"{nameof(GameplaySettingsPanel)} could not find a Toggle for '{labelFragment}'.", this);
            return;
        }

        SetToggleValue(toggles, initialValue);
        BindToggleEvents(toggles, onChanged);
    }

    private void BindCheatToggle(string labelFragment, bool initialValue, Action<bool> onChanged)
    {
        Transform row = FindRow(labelFragment);
        if (row == null)
            return;

        Toggle[] toggles = row.GetComponentsInChildren<Toggle>(true);
        if (toggles.Length == 0)
        {
            Debug.LogWarning($"{nameof(GameplaySettingsPanel)} could not find a Toggle for '{labelFragment}'.", this);
            return;
        }

        SetToggleValue(toggles, initialValue);
        BindToggleEvents(toggles, enabled =>
        {
            onChanged(enabled);
            SetButtonsInteractable(row, enabled);
        });
        SetButtonsInteractable(row, initialValue);
    }

    private void BindToggleEvents(Toggle[] toggles, Action<bool> onChanged)
    {
        Toggle onToggle = FindToggle(toggles, OnToggleName);
        Toggle offToggle = FindToggle(toggles, OffToggleName);

        if (onToggle != null && offToggle != null)
        {
            AddToggleListener(onToggle, isOn => { if (isOn) onChanged(true); });
            AddToggleListener(offToggle, isOn => { if (isOn) onChanged(false); });
            return;
        }

        Toggle primaryToggle = toggles[0];
        AddToggleListener(primaryToggle, isOn => onChanged(isOn));
    }

    private void AddToggleListener(Toggle toggle, UnityAction<bool> callback)
    {
        toggle.onValueChanged.AddListener(callback);
        _toggleListeners.Add(new ToggleListener(toggle, callback));
    }

    private static void SetToggleValue(Toggle[] toggles, bool enabled)
    {
        Toggle onToggle = FindToggle(toggles, OnToggleName);
        Toggle offToggle = FindToggle(toggles, OffToggleName);
        if (onToggle != null && offToggle != null)
        {
            onToggle.SetIsOnWithoutNotify(enabled);
            offToggle.SetIsOnWithoutNotify(!enabled);
            return;
        }

        if (toggles.Length > 0)
            toggles[0].SetIsOnWithoutNotify(enabled);
    }

    private static Toggle FindToggle(Toggle[] toggles, string objectName)
    {
        foreach (Toggle toggle in toggles)
        {
            if (toggle != null && toggle.gameObject.name == objectName)
                return toggle;
        }

        return null;
    }

    private static void SetButtonsInteractable(Transform row, bool interactable)
    {
        Button[] buttons = row.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
            button.interactable = interactable;
    }

    private void UpdateFieldOfViewReadout(Transform row, float value)
    {
        TMP_Text[] labels = row.GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text label in labels)
        {
            if (label != null && label.gameObject.name == "FOV Text")
            {
                label.text = Mathf.RoundToInt(value).ToString(CultureInfo.InvariantCulture) + "°";
                return;
            }
        }
    }

    private Transform FindRow(string labelFragment)
    {
        TMP_Text[] labels = GetComponentsInChildren<TMP_Text>(true);
        TMP_Text matchingLabel = null;
        foreach (TMP_Text label in labels)
        {
            if (label == null || !label.text.Trim().ToLowerInvariant().Contains(labelFragment))
                continue;

            matchingLabel = label;
            break;
        }

        if (matchingLabel == null)
        {
            Debug.LogWarning($"{nameof(GameplaySettingsPanel)} could not find a row label containing '{labelFragment}'.", this);
            return null;
        }

        Transform current = matchingLabel.transform;
        while (current != null && current != transform)
        {
            if (current.GetComponentInChildren<Slider>(true) != null
                || current.GetComponentInChildren<Toggle>(true) != null
                || current.GetComponentInChildren<Button>(true) != null)
            {
                return current;
            }

            current = current.parent;
        }

        Debug.LogWarning($"{nameof(GameplaySettingsPanel)} could not find controls for '{labelFragment}'.", matchingLabel);
        return null;
    }

    private readonly struct ToggleListener
    {
        public ToggleListener(Toggle toggle, UnityAction<bool> callback)
        {
            Toggle = toggle;
            Callback = callback;
        }

        public Toggle Toggle { get; }
        public UnityAction<bool> Callback { get; }
    }

    private readonly struct SliderListener
    {
        public SliderListener(Slider slider, UnityAction<float> callback)
        {
            Slider = slider;
            Callback = callback;
        }

        public Slider Slider { get; }
        public UnityAction<float> Callback { get; }
    }
}
