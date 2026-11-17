
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public static class InputBindingsService
{
    private const string Prefix = "Erumperem.Input.";
    private const string StablePrefix = "Erumperem.Input.Paths.";

    private static readonly Dictionary<string, InputActionMap> Maps = new Dictionary<string, InputActionMap>(StringComparer.Ordinal);

    private static InputActionRebindingExtensions.RebindingOperation operation;
    private static InputAction currentAction;
    private static Action<bool> completed;
    private static bool wasEnabled;

    [Serializable]
    private sealed class BindingRecord
    {
        public string action;
        public int index;
        public string path;
    }

    [Serializable]
    private sealed class BindingFile
    {
        public List<BindingRecord> entries = new List<BindingRecord>();
    }

    public static bool IsRebinding { get; private set; }
    public static IEnumerable<InputActionMap> RegisteredMaps => Maps.Values;

    public static void Register(InputActionMap map)
    {
        if (map == null)
        {
            return;
        }

        Maps[map.name] = map;
        string stableJson = PlayerPrefs.GetString(StablePrefix + map.name, string.Empty);

        try
        {
            if (string.IsNullOrWhiteSpace(stableJson))
            {
                string oldJson = PlayerPrefs.GetString(Prefix + map.name, string.Empty);

                if (!string.IsNullOrWhiteSpace(oldJson))
                {
                    map.LoadBindingOverridesFromJson(oldJson);
                }

                return;
            }

            BindingFile file = JsonUtility.FromJson<BindingFile>(stableJson);

            if (file == null || file.entries == null)
            {
                return;
            }

            foreach (BindingRecord entry in file.entries)
            {
                InputAction action = map.FindAction(entry.action, false);

                if (action != null && entry.index >= 0 && entry.index < action.bindings.Count && !string.IsNullOrEmpty(entry.path))
                {
                    action.ApplyBindingOverride(entry.index, entry.path);
                }
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[InputBindingsService] Failed to load key bindings for " + map.name + ": " + exception.Message);
        }
    }

    public static void LoadOverrides(InputActionMap map)
    {
        Register(map);
    }

    public static void LoadOverrides(InputActionAsset asset)
    {
        if (asset == null)
        {
            return;
        }

        foreach (InputActionMap map in asset.actionMaps)
        {
            Register(map);
        }
    }

    public static bool BeginRebinding(InputAction action, int index, Action<bool> callback)
    {
        return Begin(action, index, callback);
    }

    public static void CancelRebinding()
    {
        Cancel();
    }

    public static bool IsKeyboardPath(string path)
    {
        return !string.IsNullOrEmpty(path) && path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase);
    }

    public static void SaveOverrides(InputActionMap map)
    {
        Save(map);
    }

    public static void Unregister(InputActionMap map)
    {
        if (map != null && Maps.TryGetValue(map.name, out InputActionMap existing) && ReferenceEquals(existing, map))
        {
            Maps.Remove(map.name);
        }
    }

    public static bool IsKeyboardBinding(InputBinding binding)
    {
        return !string.IsNullOrEmpty(binding.path) && binding.path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase);
    }

    public static bool Begin(InputAction action, int index, Action<bool> callback)
    {
        if (IsRebinding || action == null || index < 0 || index >= action.bindings.Count || action.bindings[index].isComposite || !IsKeyboardBinding(action.bindings[index]))
        {
            return false;
        }

        currentAction = action;
        completed = callback;
        wasEnabled = action.enabled;
        IsRebinding = true;
        action.Disable();

        try
        {
            operation = action.PerformInteractiveRebinding(index)
                .WithControlsExcluding("<Mouse>")
                .WithControlsExcluding("<Pointer>")
                .WithControlsExcluding("<Gamepad>")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(result => Finish(true))
                .OnCancel(result => Finish(false));

            operation.Start();
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError("[InputBindingsService] Rebinding failed: " + exception.Message);
            Finish(false);
            return false;
        }
    }

    public static void Cancel()
    {
        if (IsRebinding)
        {
            operation?.Cancel();
        }
    }

    public static void RestoreDefaults()
    {
        Cancel();

        foreach (InputActionMap map in Maps.Values)
        {
            if (map == null)
            {
                continue;
            }

            map.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(Prefix + map.name);
            PlayerPrefs.DeleteKey(StablePrefix + map.name);
        }

        PlayerPrefs.Save();
    }

    private static void Finish(bool success)
    {
        if (!IsRebinding)
        {
            return;
        }

        InputAction action = currentAction;
        Action<bool> callback = completed;
        bool reenable = wasEnabled;

        currentAction = null;
        completed = null;
        wasEnabled = false;

        if (success && action != null)
        {
            Save(action.actionMap);
        }

        operation?.Dispose();
        operation = null;
        IsRebinding = false;

        if (reenable)
        {
            action?.Enable();
        }

        callback?.Invoke(success);
    }

    private static void Save(InputActionMap map)
    {
        if (map == null)
        {
            return;
        }

        BindingFile file = new BindingFile();

        foreach (InputAction action in map.actions)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];

                if (!string.IsNullOrEmpty(binding.overridePath))
                {
                    file.entries.Add(new BindingRecord
                    {
                        action = action.name,
                        index = i,
                        path = binding.overridePath
                    });
                }
            }
        }

        PlayerPrefs.SetString(StablePrefix + map.name, JsonUtility.ToJson(file));
        PlayerPrefs.Save();
    }
}
