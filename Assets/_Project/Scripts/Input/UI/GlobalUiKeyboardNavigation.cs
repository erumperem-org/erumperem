
using System;
using System.Collections.Generic;
using Core.UI.Panels;
using Erumperem.Progression;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public sealed class GlobalUiKeyboardNavigation : MonoBehaviour
{
    private static GlobalUiKeyboardNavigation _instance;

    private static readonly HashSet<string> AdditionalMenuNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "PausePanel",
        "InventoryPanel",
        "NewInventoryPanel",
        "MarketPanel",
        "NpcPanel",
        "KnightSkillTreePanel",
        "SkillTreePanel",
        "SkillTreeNonInteractPanel",
        "CharacterSelectionPanel",
        "DialoguePanel",
        "PurchasePopup",
        "PurchasePopupPanel",
        "ItemOptionsPanel",
        "ItemPopUp",
        "RewardPanel",
        "SafePanel",
        "KeyBindingsPanel"
    };

    [Header("Navigation")]
    [SerializeField, Range(0.1f, 1f)] private float navigationThreshold = 0.55f;
    [SerializeField, Min(0f)] private float firstRepeatDelay = 0.35f;
    [SerializeField, Min(0.02f)] private float repeatInterval = 0.12f;

    [Header("Debug")]
    [SerializeField] private bool debugActivation;

    private UiKeyboardInputReader _inputReader;
    private UiNavigationSession _navigationSession;
    private float _nextScanTime;
    private KeyboardNavigablePanel _pendingCancelPanel;
    private int _pendingCancelFrame = -1;

    public static GlobalUiKeyboardNavigation Instance => _instance;
    public UiKeyboardInputReader Reader => _inputReader;

    public static bool IsAnyUiMenuOpen => KeyboardNavigablePanel.Current != null || (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive);

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

        SceneManager.sceneLoaded += HandleSceneLoaded;
        PanelVisibilityController.OnPanelVisibilityChanged += HandlePanelVisibilityChanged;

        _nextScanTime = 0f;
    }

    private void OnDisable()
    {
        _pendingCancelPanel = null;
        _pendingCancelFrame = -1;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        PanelVisibilityController.OnPanelVisibilityChanged -= HandlePanelVisibilityChanged;

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

    private void Update()
    {
        if (_inputReader == null || _navigationSession == null)
        {
            return;
        }

        if (Time.unscaledTime >= _nextScanTime)
        {
            _nextScanTime = Time.unscaledTime + 0.5f;
            RegisterMenus();
        }

        _navigationSession.Tick();
        _inputReader.Tick(_navigationSession.HasActivePanel);
    }

    private void LateUpdate()
    {
        if (_pendingCancelFrame < 0)
        {
            return;
        }

        KeyboardNavigablePanel requestedPanel = _pendingCancelPanel;

        _pendingCancelPanel = null;
        _pendingCancelFrame = -1;

        // Player/Pause also uses Escape. If its action already closed a menu
        // this frame, do not cancel another panel underneath or reopen it.
        if (requestedPanel == null || KeyboardNavigablePanel.Current != requestedPanel)
        {
            return;
        }

        InputDeviceModeManager.UseKeyboardMode();
        _navigationSession?.ActivateKeyboardMode();
        _navigationSession?.Cancel();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _nextScanTime = 0f;
    }

    private void HandlePanelVisibilityChanged(GameObject panel, bool visible)
    {
        if (visible)
        {
            RegisterMenu(panel);
        }
    }

    private static void RegisterMenus()
    {
        Panel[] panels = FindObjectsByType<Panel>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (Panel panel in panels)
        {
            if (panel == null || !panel.gameObject.scene.IsValid())
            {
                continue;
            }

            string name = CleanName(panel.name);

            // Do not register a decorative child literally named "Panel".
            if (name != "Panel" && name.EndsWith("Panel", StringComparison.OrdinalIgnoreCase))
            {
                RegisterMenu(panel.gameObject);
            }
        }

        SkillTreeView[] skillTreeViews = FindObjectsByType<SkillTreeView>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (SkillTreeView view in skillTreeViews)
        {
            if (view != null && view.gameObject.scene.IsValid())
            {
                RegisterMenu(view.gameObject);
            }
        }

        RectTransform[] rects = FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (RectTransform rect in rects)
        {
            if (rect != null && rect.gameObject.scene.IsValid() && AdditionalMenuNames.Contains(CleanName(rect.name)))
            {
                RegisterMenu(rect.gameObject);
            }
        }
    }

    private static void RegisterMenu(GameObject menu)
    {
        if (menu == null || !menu.scene.IsValid() || menu.GetComponent<RectTransform>() == null || menu.GetComponent<KeyboardNavigablePanel>() != null)
        {
            return;
        }

        if (menu.GetComponentInParent<Canvas>(true) == null)
        {
            return;
        }

        string name = CleanName(menu.name);

        if (!AdditionalMenuNames.Contains(name) && (menu.GetComponent<Panel>() == null || name == "Panel") && menu.GetComponent<SkillTreeView>() == null)
        {
            return;
        }

        if (!AdditionalMenuNames.Contains(name) && menu.GetComponentInParent<KeyboardNavigablePanel>(true) != null)
        {
            return;
        }

        if (menu.GetComponentInChildren<Selectable>(true) == null && menu.GetComponentInChildren<ScrollRect>(true) == null && !HasCustomButton(menu))
        {
            return;
        }

        menu.AddComponent<KeyboardNavigablePanel>();
    }

    private static string CleanName(string name)
    {
        return name == null ? string.Empty : name.Replace("(Clone)", string.Empty).Trim();
    }

    private static bool HasCustomButton(GameObject menu)
    {
        MonoBehaviour[] behaviours = menu.GetComponentsInChildren<MonoBehaviour>(true);

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
        // Resolve Escape after all InputAction callbacks (including Pause).
        _pendingCancelPanel = KeyboardNavigablePanel.Current;
        _pendingCancelFrame = Time.frameCount;
    }
}
