using System;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class UiKeyboardInputReader : IDisposable
{
    public event Action<UiNavigationDirection> NavigateRequested;
    public event Action SubmitRequested;
    public event Action CancelRequested;

    private readonly float _navigationThreshold;
    private readonly float _firstRepeatDelay;
    private readonly float _repeatInterval;

    private InputActionMap _actionMap;
    private InputAction _navigateAction;
    private InputAction _submitAction;
    private InputAction _cancelAction;
    private UiNavigationDirection? _heldDirection;
    private float _nextRepeatTime;

    public UiKeyboardInputReader(float navigationThreshold, float firstRepeatDelay, float repeatInterval)
    {
        _navigationThreshold = navigationThreshold;
        _firstRepeatDelay = firstRepeatDelay;
        _repeatInterval = repeatInterval;
        CreateActions();
    }

    public void Enable()
    {
        _submitAction.performed += OnSubmitPerformed;
        _cancelAction.performed += OnCancelPerformed;
        _actionMap.Enable();
    }

    public void Disable()
    {
        _actionMap.Disable();
        _submitAction.performed -= OnSubmitPerformed;
        _cancelAction.performed -= OnCancelPerformed;
        _heldDirection = null;
    }

    public void Tick(bool navigationEnabled)
    {
        if (!navigationEnabled)
        {
            _heldDirection = null;
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
            _nextRepeatTime = Time.unscaledTime + _firstRepeatDelay;
            NavigateRequested?.Invoke(direction);
            return;
        }

        if (Time.unscaledTime < _nextRepeatTime)
        {
            return;
        }

        _nextRepeatTime = Time.unscaledTime + _repeatInterval;
        NavigateRequested?.Invoke(direction);
    }

    public void Dispose()
    {
        Disable();
        _actionMap?.Dispose();
        _actionMap = null;
    }

    private void CreateActions()
    {
        _actionMap = new InputActionMap("GlobalUiKeyboard");

        _navigateAction = _actionMap.AddAction("Navigate", InputActionType.Value);
        _navigateAction.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
        _navigateAction.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow").With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");

        _submitAction = _actionMap.AddAction("Submit", InputActionType.Button);
        _submitAction.AddBinding("<Keyboard>/enter");
        _submitAction.AddBinding("<Keyboard>/numpadEnter");
        _submitAction.AddBinding("<Keyboard>/space");

        _cancelAction = _actionMap.AddAction("Cancel", InputActionType.Button);
        _cancelAction.AddBinding("<Keyboard>/escape");
        _cancelAction.AddBinding("<Keyboard>/q");
    }

    private bool TryResolveDirection(Vector2 navigation, out UiNavigationDirection direction)
    {
        direction = default;

        if (navigation.sqrMagnitude < _navigationThreshold * _navigationThreshold)
        {
            return false;
        }

        if (Mathf.Abs(navigation.x) > Mathf.Abs(navigation.y))
        {
            direction = navigation.x >= 0f ? UiNavigationDirection.Right : UiNavigationDirection.Left;
            return true;
        }

        direction = navigation.y >= 0f ? UiNavigationDirection.Up : UiNavigationDirection.Down;
        return true;
    }

    private void OnSubmitPerformed(InputAction.CallbackContext context)
    {
        SubmitRequested?.Invoke();
    }

    private void OnCancelPerformed(InputAction.CallbackContext context)
    {
        CancelRequested?.Invoke();
    }
}
