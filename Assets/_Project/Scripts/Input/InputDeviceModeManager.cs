using System;
using System.Collections.Generic;
using System.Reflection;
using Erumperem.Combat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public enum InputControlMode
{
    Mouse,
    Keyboard
}

[DefaultExecutionOrder(-32000)]
[DisallowMultipleComponent]
public sealed class InputDeviceModeManager : MonoBehaviour
{
    private static readonly FieldInfo InputManagerHasPointerField = typeof(InputManager).GetField("_hasPointerScreenPosition", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo CombatLeftClickField = typeof(CombatPrototypeController).GetField("_leftClickPressedThisFrame", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo CombatRightClickField = typeof(CombatPrototypeController).GetField("_rightClickPressedThisFrame", BindingFlags.Instance | BindingFlags.NonPublic);

    private static InputDeviceModeManager _instance;

    [SerializeField, Min(0f)] private float mouseMovementThreshold = 1.5f;

    private readonly Dictionary<BaseInputModule, bool> _inputModulePreviousStates = new();
    private InputControlMode _currentMode = InputControlMode.Mouse;
    private bool _mouseBlockingApplied;

    public static InputDeviceModeManager Instance => _instance;
    public static InputControlMode CurrentMode => _instance != null ? _instance._currentMode : InputControlMode.Mouse;
    public static bool IsKeyboardMode => CurrentMode == InputControlMode.Keyboard;
    public static bool IsMouseMode => CurrentMode == InputControlMode.Mouse;

    public event Action<InputControlMode> ModeChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureInstanceExists()
    {
        if (FindFirstObjectByType<InputDeviceModeManager>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        var managerObject = new GameObject(nameof(InputDeviceModeManager));
        managerObject.AddComponent<InputDeviceModeManager>();
    }

    public static void UseKeyboardMode()
    {
        _instance?.SetMode(InputControlMode.Keyboard);
    }

    public static void UseMouseMode()
    {
        _instance?.SetMode(InputControlMode.Mouse);
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
    }

    private void OnDisable()
    {
        ReleaseMouseBlocking();
    }

    private void OnDestroy()
    {
        ReleaseMouseBlocking();

        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void Update()
    {
        DetectLastUsedDevice();

        if (!IsExclusiveInputContextActive())
        {
            ReleaseMouseBlocking();
            return;
        }

        if (_currentMode == InputControlMode.Keyboard)
        {
            ApplyMouseBlocking();
            return;
        }

        ReleaseMouseBlocking();
    }

    private void DetectLastUsedDevice()
    {
        if (WasKeyboardUsedThisFrame())
        {
            SetMode(InputControlMode.Keyboard);
            return;
        }

        if (WasMouseUsedThisFrame())
        {
            SetMode(InputControlMode.Mouse);
        }
    }

    private bool WasKeyboardUsedThisFrame()
    {
        var keyboard = Keyboard.current;

        if (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
        {
            return true;
        }

        var gamepad = Gamepad.current;

        if (gamepad == null)
        {
            return false;
        }

        if (gamepad.buttonSouth.wasPressedThisFrame || gamepad.buttonEast.wasPressedThisFrame || gamepad.buttonWest.wasPressedThisFrame || gamepad.buttonNorth.wasPressedThisFrame)
        {
            return true;
        }

        if (gamepad.dpad.up.wasPressedThisFrame || gamepad.dpad.down.wasPressedThisFrame || gamepad.dpad.left.wasPressedThisFrame || gamepad.dpad.right.wasPressedThisFrame)
        {
            return true;
        }

        return gamepad.leftStick.ReadValue().sqrMagnitude >= 0.25f;
    }

    private bool WasMouseUsedThisFrame()
    {
        var mouse = Mouse.current;

        if (mouse == null)
        {
            return false;
        }

        if (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame)
        {
            return true;
        }

        if (mouse.scroll.ReadValue().sqrMagnitude > 0f)
        {
            return true;
        }

        var delta = mouse.delta.ReadValue();
        return delta.sqrMagnitude >= mouseMovementThreshold * mouseMovementThreshold;
    }

    private void SetMode(InputControlMode mode)
    {
        if (_currentMode == mode)
        {
            return;
        }

        _currentMode = mode;

        if (_currentMode == InputControlMode.Keyboard)
        {
            ApplyMouseBlocking();
        }
        else
        {
            ReleaseMouseBlocking();
        }

        ModeChanged?.Invoke(_currentMode);
    }

    private bool IsExclusiveInputContextActive()
    {
        if (KeyboardNavigablePanel.Current != null)
        {
            return true;
        }

        return FindFirstObjectByType<CombatInputController>() != null && FindFirstObjectByType<CombatPrototypeController>() != null;
    }

    private void ApplyMouseBlocking()
    {
        InvalidateCombatPointer();
        ClearCombatMouseClickFlags();
        DisableEventSystemInputModules();
        _mouseBlockingApplied = true;
    }

    private void ReleaseMouseBlocking()
    {
        if (!_mouseBlockingApplied && _inputModulePreviousStates.Count == 0)
        {
            return;
        }

        foreach (var entry in _inputModulePreviousStates)
        {
            if (entry.Key != null)
            {
                entry.Key.enabled = entry.Value;
            }
        }

        _inputModulePreviousStates.Clear();
        _mouseBlockingApplied = false;
    }

    private void InvalidateCombatPointer()
    {
        var inputManager = InputManager.Instance;

        if (inputManager == null || InputManagerHasPointerField == null)
        {
            return;
        }

        InputManagerHasPointerField.SetValue(inputManager, false);
    }

    private void ClearCombatMouseClickFlags()
    {
        var combatControllers = FindObjectsByType<CombatPrototypeController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (var controllerIndex = 0; controllerIndex < combatControllers.Length; controllerIndex++)
        {
            var combatController = combatControllers[controllerIndex];

            if (combatController == null)
            {
                continue;
            }

            CombatLeftClickField?.SetValue(combatController, false);
            CombatRightClickField?.SetValue(combatController, false);
        }
    }

    private void DisableEventSystemInputModules()
    {
        var inputModules = FindObjectsByType<BaseInputModule>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (var moduleIndex = 0; moduleIndex < inputModules.Length; moduleIndex++)
        {
            var inputModule = inputModules[moduleIndex];

            if (inputModule == null)
            {
                continue;
            }

            if (!_inputModulePreviousStates.ContainsKey(inputModule))
            {
                _inputModulePreviousStates[inputModule] = inputModule.enabled;
            }

            if (inputModule.enabled)
            {
                inputModule.enabled = false;
            }
        }
    }
}