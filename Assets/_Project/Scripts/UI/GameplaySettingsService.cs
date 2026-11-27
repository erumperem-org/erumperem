using Erumperem.Combat;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-10000)]
[DisallowMultipleComponent]
public sealed class GameplaySettingsService : MonoBehaviour
{
    public const string ExplorationFieldOfViewPreferenceKey = "Gameplay.ExplorationFieldOfView";
    public const string CombatFieldOfViewPreferenceKey = "Gameplay.CombatFieldOfView";
    public const string HideHudPreferenceKey = "Gameplay.HideHud";
    public const string SceneChangeCheatPreferenceKey = "Gameplay.SceneChangeCheatEnabled";
    public const string WinCombatCheatPreferenceKey = "Gameplay.WinCombatCheatEnabled";
    public const string LoseCombatCheatPreferenceKey = "Gameplay.LoseCombatCheatEnabled";
    public const string InfiniteAllyHealthPreferenceKey = "Gameplay.InfiniteAllyHealthEnabled";
    public const string DoubleAllyDamagePreferenceKey = "Gameplay.DoubleAllyDamageEnabled";

    private const string ExplorationSceneName = "Overworld";
    private const string CombatSceneName = "CombatScene";
    private const string CombatWideCameraName = "WideBattle_CinemachineCamera";
    private const string HudRootName = "HUD";
    private const string CombatHudRootName = "CombatHUD";
    private const string CombatFlowHudRootName = "CombatFlowHudRoot";
    private const string CombatElementHudRootName = "CombatElementMatchupHudRoot";
    private const string CombatBottomPanelName = "BottonPanel";
    private const string CombatSkillsHudName = "Skills";
    private const string TorchThreatCanvasName = "TorchThreatCanvas";
    private const float DefaultExplorationFieldOfView = 60f;
    private const float DefaultCombatFieldOfView = 50f;
    private const float MinimumExplorationFieldOfView = 30f;
    private const float MaximumExplorationFieldOfView = 65f;
    private const float MinimumCombatFieldOfView = 65f;
    private const float MaximumCombatFieldOfView = 80f;
    private const float RefreshIntervalSeconds = 0.25f;

    private static GameplaySettingsService _instance;

    private readonly System.Collections.Generic.Dictionary<GameObject, bool> _hiddenHudOriginalStates =
        new System.Collections.Generic.Dictionary<GameObject, bool>();

    private float _refreshAt;
    private int _trackedSceneHandle = -1;
    private int _gameplayPanelSetupSceneHandle = -1;
    private bool _warnedMissingCombatSkillsHud;

    public static float ExplorationFieldOfView => Mathf.Clamp(
        PlayerPrefs.GetFloat(ExplorationFieldOfViewPreferenceKey, DefaultExplorationFieldOfView),
        MinimumExplorationFieldOfView, MaximumExplorationFieldOfView);

    public static float CombatFieldOfView => Mathf.Clamp(
        PlayerPrefs.GetFloat(CombatFieldOfViewPreferenceKey, DefaultCombatFieldOfView),
        MinimumCombatFieldOfView, MaximumCombatFieldOfView);

    public static bool HideHudEnabled => ReadBoolean(HideHudPreferenceKey, false);
    public static bool HideCustomCursorEnabled => CursorManager.Instance != null
        ? !CursorManager.Instance.SpecialCursorEnabled
        : !CursorManager.GetSavedSpecialCursorEnabled();
    public static bool SceneChangeCheatEnabled => ReadBoolean(SceneChangeCheatPreferenceKey, true);
    public static bool WinCombatCheatEnabled => ReadBoolean(WinCombatCheatPreferenceKey, true);
    public static bool LoseCombatCheatEnabled => ReadBoolean(LoseCombatCheatPreferenceKey, true);
    public static bool InfiniteAllyHealthEnabled => ReadBoolean(InfiniteAllyHealthPreferenceKey, false);
    public static bool DoubleAllyDamageEnabled => ReadBoolean(DoubleAllyDamagePreferenceKey, false);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    private static GameplaySettingsService EnsureInstance()
    {
        if (_instance != null)
            return _instance;

        GameplaySettingsService existing = FindFirstObjectByType<GameplaySettingsService>(FindObjectsInactive.Include);
        if (existing != null)
            return existing;

        GameObject serviceObject = new GameObject(nameof(GameplaySettingsService));
        return serviceObject.AddComponent<GameplaySettingsService>();
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
        RefreshSettings();
    }

    private void OnDestroy()
    {
        if (_instance != this)
            return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        RestoreHiddenHudObjects();
        _instance = null;
    }

    private void Update()
    {
        if (Time.unscaledTime < _refreshAt)
            return;

        _refreshAt = Time.unscaledTime + RefreshIntervalSeconds;
        RefreshSettings();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RestoreHiddenHudObjects();
        _trackedSceneHandle = scene.handle;
        _gameplayPanelSetupSceneHandle = -1;
        _warnedMissingCombatSkillsHud = false;
        _refreshAt = 0f;
        RefreshSettings();
    }

    private void RefreshSettings()
    {
        EnsureGameplaySettingsPanel();
        ApplyFieldOfView();
        ApplyHudVisibility();
    }

    private void EnsureGameplaySettingsPanel()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (_gameplayPanelSetupSceneHandle == activeScene.handle)
            return;

        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Transform candidate in transforms)
        {
            if (candidate == null || candidate.gameObject.scene != activeScene || candidate.name != "GameplayPanel")
                continue;

            if (candidate.GetComponent<GameplaySettingsPanel>() == null)
                candidate.gameObject.AddComponent<GameplaySettingsPanel>();
            _gameplayPanelSetupSceneHandle = activeScene.handle;
            return;
        }

        _gameplayPanelSetupSceneHandle = activeScene.handle;
    }

    private void ApplyFieldOfView()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        CinemachineCamera[] cameras = FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (activeScene.name == ExplorationSceneName)
        {
            foreach (CinemachineCamera camera in cameras)
            {
                if (camera == null || camera.gameObject.scene != activeScene || !HasExplorationCameraMarker(camera))
                    continue;

                SetCameraFieldOfView(camera, ExplorationFieldOfView);
            }
        }
        else if (activeScene.name == CombatSceneName)
        {
            foreach (CinemachineCamera camera in cameras)
            {
                if (camera == null || camera.gameObject.scene != activeScene || camera.name != CombatWideCameraName)
                    continue;

                SetCameraFieldOfView(camera, CombatFieldOfView);
            }
        }
    }

    private static bool HasExplorationCameraMarker(CinemachineCamera camera)
    {
        return camera.GetComponent<InGameCharacterCamera>() != null;
    }

    private static void SetCameraFieldOfView(CinemachineCamera camera, float fieldOfView)
    {
        var lens = camera.Lens;
        if (Mathf.Approximately(lens.FieldOfView, fieldOfView))
            return;

        lens.FieldOfView = fieldOfView;
        camera.Lens = lens;
    }

    private void ApplyHudVisibility()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (_trackedSceneHandle != activeScene.handle)
        {
            RestoreHiddenHudObjects();
            _trackedSceneHandle = activeScene.handle;
        }

        if (!HideHudEnabled)
        {
            RestoreHiddenHudObjects();
            return;
        }

        if (activeScene.name == ExplorationSceneName)
        {
            GameObject hudRoot = FindSceneObject(activeScene, HudRootName);
            if (hudRoot == null)
                return;

            foreach (Transform child in hudRoot.transform)
                HideHudObject(child.gameObject);
            HideSceneObject(activeScene, "ItemPopUpContainer");
            HideSceneObject(activeScene, TorchThreatCanvasName);
        }
        else if (activeScene.name == CombatSceneName)
        {
            ApplyCombatHudVisibility(activeScene);
            HideCombatNotifications(activeScene);
            HideSceneObject(activeScene, CombatFlowHudRootName);
            HideSceneObject(activeScene, CombatElementHudRootName);
        }
    }

    private void ApplyCombatHudVisibility(Scene activeScene)
    {
        GameObject combatHudRoot = FindSceneObject(activeScene, CombatHudRootName);
        if (combatHudRoot == null)
            return;

        Transform topPanel = combatHudRoot.transform.Find("TopPanel");
        if (topPanel != null)
            HideHudObject(topPanel.gameObject);

        Transform bottomPanel = combatHudRoot.transform.Find(CombatBottomPanelName);
        if (bottomPanel == null)
            return;

        Transform skillsHud = FindDescendant(bottomPanel, CombatSkillsHudName);
        if (skillsHud == null)
        {
            if (!_warnedMissingCombatSkillsHud)
            {
                Debug.LogWarning("Hide HUD is enabled, but the combat Skills HUD was not found; the combat bottom panel remains visible to preserve player attacks.", combatHudRoot);
                _warnedMissingCombatSkillsHud = true;
            }
            return;
        }

        Transform preservedBranch = skillsHud;
        while (preservedBranch.parent != null && preservedBranch.parent != bottomPanel)
            preservedBranch = preservedBranch.parent;

        foreach (Transform child in bottomPanel)
        {
            if (child != preservedBranch)
                HideHudObject(child.gameObject);
        }
    }

    private void HideCombatNotifications(Scene activeScene)
    {
        CombatLogStackView[] notificationStacks = FindObjectsByType<CombatLogStackView>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (CombatLogStackView notificationStack in notificationStacks)
        {
            if (notificationStack == null || notificationStack.gameObject.scene != activeScene)
                continue;

            if (notificationStack.NotificationsRoot != null)
                HideHudObject(notificationStack.NotificationsRoot);
        }
    }

    private void HideSceneObject(Scene scene, string objectName)
    {
        GameObject target = FindSceneObject(scene, objectName);
        if (target != null)
            HideHudObject(target);
    }

    private void HideHudObject(GameObject target)
    {
        if (target == null)
            return;

        if (!_hiddenHudOriginalStates.ContainsKey(target))
            _hiddenHudOriginalStates.Add(target, target.activeSelf);

        if (target.activeSelf)
            target.SetActive(false);
    }

    private void RestoreHiddenHudObjects()
    {
        foreach (var pair in _hiddenHudOriginalStates)
        {
            if (pair.Key != null)
                pair.Key.SetActive(pair.Value);
        }

        _hiddenHudOriginalStates.Clear();
    }

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Transform candidate in transforms)
        {
            if (candidate == null || candidate.gameObject.scene != scene || candidate.name != objectName)
                continue;
            if (objectName == HudRootName && candidate.GetComponent<Panel>() == null)
                continue;

            return candidate.gameObject;
        }

        return null;
    }

    private static Transform FindDescendant(Transform root, string objectName)
    {
        Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform descendant in descendants)
        {
            if (descendant != root && descendant.name == objectName)
                return descendant;
        }

        return null;
    }

    /// <summary>Saves and applies the exploration camera field of view.</summary>
    public static void SetExplorationFieldOfView(float fieldOfView)
    {
        fieldOfView = Mathf.Clamp(fieldOfView, MinimumExplorationFieldOfView, MaximumExplorationFieldOfView);
        if (Mathf.Approximately(ExplorationFieldOfView, fieldOfView))
            return;

        PlayerPrefs.SetFloat(ExplorationFieldOfViewPreferenceKey, fieldOfView);
        PlayerPrefs.Save();
        EnsureInstance().ApplyFieldOfView();
    }

    /// <summary>Saves and applies the combat wide-camera field of view.</summary>
    public static void SetCombatFieldOfView(float fieldOfView)
    {
        fieldOfView = Mathf.Clamp(fieldOfView, MinimumCombatFieldOfView, MaximumCombatFieldOfView);
        if (Mathf.Approximately(CombatFieldOfView, fieldOfView))
            return;

        PlayerPrefs.SetFloat(CombatFieldOfViewPreferenceKey, fieldOfView);
        PlayerPrefs.Save();
        EnsureInstance().ApplyFieldOfView();
    }

    /// <summary>Saves and applies the hide-HUD preference.</summary>
    public static void SetHideHudEnabled(bool enabled)
    {
        if (HideHudEnabled == enabled)
            return;

        WriteBoolean(HideHudPreferenceKey, enabled);
        EnsureInstance().RefreshSettings();
    }

    /// <summary>Saves and applies the hide-custom-cursor preference.</summary>
    public static void SetHideCustomCursorEnabled(bool hidden)
    {
        bool customCursorEnabled = !hidden;
        if (HideCustomCursorEnabled == hidden)
            return;

        if (CursorManager.Instance != null)
            CursorManager.Instance.SetSpecialCursorEnabled(customCursorEnabled);
        else
            CursorManager.SaveSpecialCursorEnabledPreference(customCursorEnabled);
    }

    /// <summary>Saves whether scene-switch cheats are enabled.</summary>
    public static void SetSceneChangeCheatEnabled(bool enabled) => WriteBoolean(SceneChangeCheatPreferenceKey, enabled);

    /// <summary>Saves whether the win-combat cheat is enabled.</summary>
    public static void SetWinCombatCheatEnabled(bool enabled) => WriteBoolean(WinCombatCheatPreferenceKey, enabled);

    /// <summary>Saves whether the lose-combat cheat is enabled.</summary>
    public static void SetLoseCombatCheatEnabled(bool enabled) => WriteBoolean(LoseCombatCheatPreferenceKey, enabled);

    /// <summary>Saves and applies the infinite-ally-health preference.</summary>
    public static void SetInfiniteAllyHealthEnabled(bool enabled)
    {
        if (InfiniteAllyHealthEnabled == enabled)
            return;

        WriteBoolean(InfiniteAllyHealthPreferenceKey, enabled);
        ApplyCombatCheatPreferences();
    }

    /// <summary>Saves and applies the double-ally-damage preference.</summary>
    public static void SetDoubleAllyDamageEnabled(bool enabled)
    {
        if (DoubleAllyDamageEnabled == enabled)
            return;

        WriteBoolean(DoubleAllyDamagePreferenceKey, enabled);
        ApplyCombatCheatPreferences();
    }

    private static void ApplyCombatCheatPreferences()
    {
        CombatPrototypeController[] controllers = FindObjectsByType<CombatPrototypeController>(FindObjectsSortMode.None);
        foreach (CombatPrototypeController controller in controllers)
        {
            if (controller != null)
                controller.ApplyGameplayCheatPreferences(InfiniteAllyHealthEnabled, DoubleAllyDamageEnabled);
        }
    }

    private static bool ReadBoolean(string key, bool defaultValue)
    {
        return PlayerPrefs.GetInt(key, defaultValue ? 1 : 0) == 1;
    }

    private static void WriteBoolean(string key, bool value)
    {
        PlayerPrefs.SetInt(key, value ? 1 : 0);
        PlayerPrefs.Save();
    }
}
