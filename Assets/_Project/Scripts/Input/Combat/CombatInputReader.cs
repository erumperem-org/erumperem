using System;
using UnityEngine;
using UnityEngine.InputSystem;

public enum CombatInputDirection
{
    Up,
    Down,
    Left,
    Right
}

[DisallowMultipleComponent]
public sealed class CombatInputReader : MonoBehaviour
{
    private const string InvertGamepadConfirmCancelPrefKey = "CombatInputInvertGamepadConfirmCancel";

    [Header("Navigation")]
    [SerializeField, Range(0.1f, 1f)] private float navigationThreshold = 0.55f;
    [SerializeField, Min(0f)] private float firstRepeatDelay = 0.35f;
    [SerializeField, Min(0.02f)] private float repeatInterval = 0.12f;

    [Header("Gamepad")]
    [SerializeField] private bool invertGamepadConfirmCancelByDefault;

    private InputActionMap _combatActionMap;
    private InputAction _navigateAction;
    private InputAction _confirmAction;
    private InputAction _cancelAction;
    private CombatInputDirection? _heldDirection;
    private float _nextRepeatTime;
    private bool _invertGamepadConfirmCancel;

    public event Action<CombatInputDirection> NavigateRequested;
    public event Action ConfirmRequested;
    public event Action CancelRequested;

    public bool InvertGamepadConfirmCancel => _invertGamepadConfirmCancel;

    private void Awake()
    {
        CreateActions();
        var defaultValue = invertGamepadConfirmCancelByDefault ? 1 : 0;
        _invertGamepadConfirmCancel = PlayerPrefs.GetInt(InvertGamepadConfirmCancelPrefKey, defaultValue) == 1;
    }

    private void OnEnable()
    {
        _confirmAction.performed += HandleConfirmPerformed;
        _cancelAction.performed += HandleCancelPerformed;
        _combatActionMap.Enable();
    }

    private void OnDisable()
    {
        if (_combatActionMap != null)
        {
            _combatActionMap.Disable();
        }

        if (_confirmAction != null)
        {
            _confirmAction.performed -= HandleConfirmPerformed;
        }

        if (_cancelAction != null)
        {
            _cancelAction.performed -= HandleCancelPerformed;
        }

        _heldDirection = null;
    }

    private void Update()
    {
        ReadNavigation();
    }

    public void SetInvertGamepadConfirmCancel(bool inverted)
    {
        _invertGamepadConfirmCancel = inverted;
        PlayerPrefs.SetInt(InvertGamepadConfirmCancelPrefKey, inverted ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void CreateActions()
    {
        _combatActionMap = new InputActionMap("CombatNavigation");

        _navigateAction = _combatActionMap.AddAction("Navigate", InputActionType.Value);
        _navigateAction.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
        _navigateAction.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow").With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
        _navigateAction.AddBinding("<Gamepad>/dpad");
        _navigateAction.AddBinding("<Gamepad>/leftStick");

        _confirmAction = _combatActionMap.AddAction("Confirm", InputActionType.Button);
        _confirmAction.AddBinding("<Keyboard>/space");
        _confirmAction.AddBinding("<Gamepad>/buttonSouth");

        _cancelAction = _combatActionMap.AddAction("Cancel", InputActionType.Button);
        _cancelAction.AddBinding("<Keyboard>/q");
        _cancelAction.AddBinding("<Gamepad>/buttonEast");
    }

    private void ReadNavigation()
    {
        if (_navigateAction == null)
        {
            return;
        }

        var navigation = _navigateAction.ReadValue<Vector2>();

        if (!TryResolveDirection(navigation, out var direction))
        {
            _heldDirection = null;
            return;
        }

        if (!_heldDirection.HasValue || _heldDirection.Value != direction)
        {
            _heldDirection = direction;
            _nextRepeatTime = Time.unscaledTime + firstRepeatDelay;
            NavigateRequested?.Invoke(direction);
            return;
        }

        if (Time.unscaledTime < _nextRepeatTime)
        {
            return;
        }

        _nextRepeatTime = Time.unscaledTime + repeatInterval;
        NavigateRequested?.Invoke(direction);
    }

    private bool TryResolveDirection(Vector2 navigation, out CombatInputDirection direction)
    {
        direction = default;

        if (navigation.sqrMagnitude < navigationThreshold * navigationThreshold)
        {
            return false;
        }

        if (Mathf.Abs(navigation.x) > Mathf.Abs(navigation.y))
        {
            direction = navigation.x >= 0f ? CombatInputDirection.Right : CombatInputDirection.Left;
            return true;
        }

        direction = navigation.y >= 0f ? CombatInputDirection.Up : CombatInputDirection.Down;
        return true;
    }

    private void HandleConfirmPerformed(InputAction.CallbackContext context)
    {
        if (_invertGamepadConfirmCancel && context.control.device is Gamepad)
        {
            CancelRequested?.Invoke();
            return;
        }

        ConfirmRequested?.Invoke();
    }

    private void HandleCancelPerformed(InputAction.CallbackContext context)
    {
        if (_invertGamepadConfirmCancel && context.control.device is Gamepad)
        {
            ConfirmRequested?.Invoke();
            return;
        }

        CancelRequested?.Invoke();
    }
}
