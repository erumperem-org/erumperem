
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public static class InputBindingsService
{
    private const string Prefix = "Erumperem.Input.";
    private const string StablePrefix = "Erumperem.Input.Paths.";
    private const string SharedMoveKey = "Erumperem.Input.SharedMove";

    private static readonly Dictionary<string, List<InputActionMap>> Maps = new Dictionary<string, List<InputActionMap>>(StringComparer.Ordinal);
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

    [Serializable]
    private sealed class MoveRecord
    {
        public string originalPath;
        public string overridePath;
    }

    [Serializable]
    private sealed class MoveFile
    {
        public List<MoveRecord> entries = new List<MoveRecord>();
    }

    public static bool IsRebinding { get; private set; }

    public static IEnumerable<InputActionMap> RegisteredMaps
    {
        get
        {
            foreach (List<InputActionMap> group in Maps.Values)
            {
                InputActionMap selected = null;

                foreach (InputActionMap map in group)
                {
                    if (map != null && (selected == null || !selected.enabled && map.enabled))
                    {
                        selected = map;
                    }
                }

                if (selected != null)
                {
                    yield return selected;
                }
            }
        }
    }

    public static void Register(InputActionMap map)
    {
        if (map == null)
        {
            return;
        }

        if (!Maps.TryGetValue(map.name, out List<InputActionMap> group))
        {
            group = new List<InputActionMap>();
            Maps.Add(map.name, group);
        }

        if (group.Contains(map))
        {
            return;
        }

        group.Add(map);
        LoadSavedOverrides(map);

        if (IsMovementMap(map))
        {
            if (PlayerPrefs.HasKey(SharedMoveKey))
            {
                ApplySharedMoveOverrides(map);
            }
            else if (HasMovementOverrides(map))
            {
                SynchronizeMove(map.FindAction("Move", false));
            }
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

    public static void Unregister(InputActionMap map)
    {
        if (map == null || !Maps.TryGetValue(map.name, out List<InputActionMap> group))
        {
            return;
        }

        group.Remove(map);

        if (group.Count == 0)
        {
            Maps.Remove(map.name);
        }
    }

    public static bool IsKeyboardPath(string path)
    {
        return !string.IsNullOrEmpty(path) && path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsKeyboardBinding(InputBinding binding)
    {
        return IsKeyboardPath(binding.path);
    }

    public static bool BeginRebinding(InputAction action, int index, Action<bool> callback)
    {
        return Begin(action, index, callback);
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

    public static void CancelRebinding()
    {
        Cancel();
    }

    public static void Cancel()
    {
        if (IsRebinding)
        {
            operation?.Cancel();
        }
    }

    public static void SaveOverrides(InputActionMap map)
    {
        Save(map);
    }

    public static void RestoreDefaults()
    {
        Cancel();
        PlayerPrefs.DeleteKey(SharedMoveKey);

        // Clear saves for maps that may not be loaded in the current scene.
        foreach (string mapName in new[] { "Player", "Gameplay", "GlobalUiKeyboard", "UI" })
        {
            PlayerPrefs.DeleteKey(Prefix + mapName);
            PlayerPrefs.DeleteKey(StablePrefix + mapName);
        }

        foreach (List<InputActionMap> group in Maps.Values)
        {
            foreach (InputActionMap map in group)
            {
                if (map != null)
                {
                    map.RemoveAllBindingOverrides();
                }
            }
        }

        foreach (string mapName in Maps.Keys)
        {
            PlayerPrefs.DeleteKey(Prefix + mapName);
            PlayerPrefs.DeleteKey(StablePrefix + mapName);
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

        operation?.Dispose();
        operation = null;
        IsRebinding = false;

        if (success && action != null)
        {
            if (action.name == "Move" && IsMovementMap(action.actionMap))
            {
                SynchronizeMove(action);
            }
            else
            {
                Save(action.actionMap);
            }
        }

        if (reenable)
        {
            action?.Enable();
        }

        callback?.Invoke(success);
    }

    private static bool IsMovementMap(InputActionMap map)
    {
        return map != null && (map.name == "Player" || map.name == "Gameplay") && map.FindAction("Move", false) != null;
    }

    private static bool HasMovementOverrides(InputActionMap map)
    {
        InputAction move = map.FindAction("Move", false);

        for (int i = 0; i < move.bindings.Count; i++)
        {
            if (IsKeyboardBinding(move.bindings[i]) && !string.IsNullOrEmpty(move.bindings[i].overridePath))
            {
                return true;
            }
        }

        return false;
    }

    private static void SynchronizeMove(InputAction source)
    {
        if (source == null)
        {
            return;
        }

        MoveFile file = new MoveFile();

        for (int index = 0; index < source.bindings.Count; index++)
        {
            InputBinding binding = source.bindings[index];

            if (IsKeyboardBinding(binding) && !string.IsNullOrEmpty(binding.overridePath))
            {
                file.entries.Add(new MoveRecord { originalPath = binding.path, overridePath = binding.overridePath });
            }
        }

        PlayerPrefs.SetString(SharedMoveKey, JsonUtility.ToJson(file));

        foreach (List<InputActionMap> group in Maps.Values)
        {
            foreach (InputActionMap map in group)
            {
                if (!IsMovementMap(map))
                {
                    continue;
                }

                ApplyMoveFile(map, file);
                Save(map);
            }
        }

        PlayerPrefs.Save();
    }

    private static void ApplySharedMoveOverrides(InputActionMap map)
    {
        string json = PlayerPrefs.GetString(SharedMoveKey, string.Empty);

        if (string.IsNullOrEmpty(json))
        {
            return;
        }

        try
        {
            MoveFile file = JsonUtility.FromJson<MoveFile>(json);
            ApplyMoveFile(map, file);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[InputBindingsService] Failed to apply shared movement bindings: " + exception.Message);
        }
    }

    private static void ApplyMoveFile(InputActionMap map, MoveFile file)
    {
        if (file == null || file.entries == null || !IsMovementMap(map))
        {
            return;
        }

        InputAction move = map.FindAction("Move", false);

        for (int index = 0; index < move.bindings.Count; index++)
        {
            InputBinding binding = move.bindings[index];

            if (!IsKeyboardBinding(binding))
            {
                continue;
            }

            string replacement = null;

            foreach (MoveRecord record in file.entries)
            {
                if (record != null && string.Equals(record.originalPath, binding.path, StringComparison.OrdinalIgnoreCase))
                {
                    replacement = record.overridePath;
                    break;
                }
            }

            if (string.IsNullOrEmpty(replacement))
            {
                move.RemoveBindingOverride(index);
            }
            else
            {
                move.ApplyBindingOverride(index, replacement);
            }
        }
    }

    private static void LoadSavedOverrides(InputActionMap map)
    {
        try
        {
            string json = PlayerPrefs.GetString(StablePrefix + map.name, string.Empty);

            if (string.IsNullOrWhiteSpace(json))
            {
                string legacy = PlayerPrefs.GetString(Prefix + map.name, string.Empty);

                if (!string.IsNullOrWhiteSpace(legacy))
                {
                    map.LoadBindingOverridesFromJson(legacy);
                }

                return;
            }

            BindingFile file = JsonUtility.FromJson<BindingFile>(json);

            if (file == null || file.entries == null)
            {
                return;
            }

            foreach (BindingRecord entry in file.entries)
            {
                if (entry == null)
                {
                    continue;
                }

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

    private static void Save(InputActionMap map)
    {
        if (map == null)
        {
            return;
        }

        BindingFile file = new BindingFile();

        foreach (InputAction action in map.actions)
        {
            for (int index = 0; index < action.bindings.Count; index++)
            {
                InputBinding binding = action.bindings[index];

                if (!string.IsNullOrEmpty(binding.overridePath))
                {
                    file.entries.Add(new BindingRecord { action = action.name, index = index, path = binding.overridePath });
                }
            }
        }

        PlayerPrefs.SetString(StablePrefix + map.name, JsonUtility.ToJson(file));
        PlayerPrefs.Save();
    }
}
