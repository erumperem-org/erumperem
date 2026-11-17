
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Core.UI.Panels;

[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public sealed class GlobalUiKeyboardNavigation : MonoBehaviour
{
    private static GlobalUiKeyboardNavigation _instance;

    private static readonly HashSet<string> KnownMenuNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "MainMenuPanel",
        "PausePanel",
        "SettingsPanel",
        "HelpPanel",
        "PatchNotesPanel",
        "InventoryPanel",
        "NewInventoryPanel",
        "CharactersPanel",
        "CharacterSelectionPanel",
        "MarketPanel",
        "NpcPanel",
        "KnightSkillTreePanel",
        "SkillTreePanel",
        "SkillTreeNonInteractPanel",
        "DialoguePanel",
        "KeyBindingsPanel",
        "SettingsUi",
        "PurchasePopup",
        "PurchasePopupPanel",
        "ItemOptions",
        "ItemOptionsPanel",
        "ItemPopUp",
        "RewardPanel",
        "SafePanel",
        "Dropdown List"
    };

    [Header("Navigation")]
    [SerializeField, Range(0.1f, 1f)] private float navigationThreshold = 0.55f;
    [SerializeField, Min(0f)] private float firstRepeatDelay = 0.35f;
    [SerializeField, Min(0.02f)] private float repeatInterval = 0.12f;

    [Header("Debug")]
    [SerializeField] private bool debugActivation;

    private UiKeyboardInputReader _inputReader;
    private UiNavigationSession _navigationSession;
    private float _nextMenuScan;

    public static GlobalUiKeyboardNavigation Instance => _instance;

    public static bool IsAnyUiMenuOpen => KeyboardNavigablePanel.Current != null || (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive);

    public UiKeyboardInputReader Reader => _inputReader;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureInstanceExists()
    {
        if (FindFirstObjectByType<GlobalUiKeyboardNavigation>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        GameObject navigationObject = new GameObject(nameof(GlobalUiKeyboardNavigation));
        navigationObject.AddComponent<GlobalUiKeyboardNavigation>();
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
        _inputReader = new UiKeyboardInputReader(navigationThreshold, firstRepeatDelay, repeatInterval);
        _navigationSession = new UiNavigationSession(debugActivation);
    }

    private void OnEnable()
    {
        if (_inputReader == null || _navigationSession == null)
        {
            return;
        }

        _inputReader.NavigateRequested += HandleNavigateRequested;
        _inputReader.SubmitRequested += HandleSubmitRequested;
        _inputReader.CancelRequested += HandleCancelRequested;
        _inputReader.Enable();

        SceneManager.sceneLoaded += OnSceneLoaded;
        PanelVisibilityController.OnPanelVisibilityChanged += OnPanelVisibilityChanged;

        _nextMenuScan = 0f;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        PanelVisibilityController.OnPanelVisibilityChanged -= OnPanelVisibilityChanged;

        if (_inputReader != null)
        {
            _inputReader.NavigateRequested -= HandleNavigateRequested;
            _inputReader.SubmitRequested -= HandleSubmitRequested;
            _inputReader.CancelRequested -= HandleCancelRequested;
            _inputReader.Disable();
        }

        _navigationSession?.Shutdown();
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }

        _inputReader?.Dispose();
        _inputReader = null;
        _navigationSession = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _nextMenuScan = 0f;
    }

    private void OnPanelVisibilityChanged(GameObject panel, bool visible)
    {
        if (visible && panel != null)
        {
            RegisterMenu(panel);
        }
    }

    private void Update()
    {
        if (_inputReader == null || _navigationSession == null)
        {
            return;
        }

        if (Time.unscaledTime >= _nextMenuScan)
        {
            _nextMenuScan = Time.unscaledTime + 0.5f;
            RegisterSceneMenus();
        }

        GameObject dropdown = GameObject.Find("Dropdown List");

        if (dropdown != null)
        {
            RegisterMenu(dropdown);
        }

        _navigationSession.Tick();
        _inputReader.Tick(_navigationSession.HasActivePanel);
    }

    private void RegisterSceneMenus()
    {
        RectTransform[] rects = FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        List<RectTransform> candidates = new List<RectTransform>();

        foreach (RectTransform rect in rects)
        {
            if (rect == null || !rect.gameObject.scene.IsValid())
            {
                continue;
            }

            if (IsKnownMenuName(rect.name) || IsIndependentPanel(rect))
            {
                candidates.Add(rect);
            }
        }

        candidates.Sort((a, b) => GetHierarchyDepth(a).CompareTo(GetHierarchyDepth(b)));

        foreach (RectTransform candidate in candidates)
        {
            RegisterMenu(candidate.gameObject);
        }
    }

    private static bool IsKnownMenuName(string name)
    {
        if (name == null)
        {
            return false;
        }

        string normalized = name.Replace("(Clone)", string.Empty).Trim();
        return KnownMenuNames.Contains(normalized);
    }

    private static bool IsIndependentPanel(RectTransform rect)
    {
        if (rect.GetComponent<Panel>() == null)
        {
            return false;
        }

        if (!rect.name.EndsWith("Panel", StringComparison.OrdinalIgnoreCase) || rect.name.IndexOf("HUD", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return false;
        }

        return rect.GetComponentInParent<KeyboardNavigablePanel>(true) == null;
    }

    private static void RegisterMenu(GameObject menu)
    {
        if (menu == null || !menu.scene.IsValid() || menu.GetComponent<RectTransform>() == null || menu.GetComponent<KeyboardNavigablePanel>() != null)
        {
            return;
        }

        bool recognized = IsKnownMenuName(menu.name) || menu.GetComponent<Panel>() != null;

        if (!recognized)
        {
            return;
        }

        if (menu.GetComponentInParent<Canvas>(true) == null)
        {
            return;
        }

        if (!IsKnownMenuName(menu.name) && menu.GetComponentInParent<KeyboardNavigablePanel>(true) != null)
        {
            return;
        }

        if (menu.GetComponentInChildren<UnityEngine.UI.Selectable>(true) == null && menu.GetComponentInChildren<UnityEngine.UI.ScrollRect>(true) == null && !HasCustomButton(menu))
        {
            return;
        }

        menu.AddComponent<KeyboardNavigablePanel>();
    }

    private static bool HasCustomButton(GameObject root)
    {
        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null)
            {
                continue;
            }

            Type type = behaviour.GetType();

            while (type != null)
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(UiButtonController<>))
                {
                    return true;
                }

                type = type.BaseType;
            }
        }

        return false;
    }

    private static int GetHierarchyDepth(Transform transform)
    {
        int depth = 0;

        while (transform.parent != null)
        {
            depth++;
            transform = transform.parent;
        }

        return depth;
    }

    private void HandleNavigateRequested(UiNavigationDirection direction)
    {
        InputDeviceModeManager.UseKeyboardMode();
        _navigationSession?.ActivateKeyboardMode();
        _navigationSession?.Navigate(direction);
    }

    private void HandleSubmitRequested()
    {
        InputDeviceModeManager.UseKeyboardMode();
        _navigationSession?.ActivateKeyboardMode();
        _navigationSession?.Confirm();
    }

    private void HandleCancelRequested()
    {
        InputDeviceModeManager.UseKeyboardMode();
        _navigationSession?.ActivateKeyboardMode();
        _navigationSession?.Cancel();
    }
}
