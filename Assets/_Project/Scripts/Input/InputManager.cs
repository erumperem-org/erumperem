using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Singleton central de input. Outros scripts não leem Keyboard/Mouse diretamente;
/// só se inscrevem nestes eventos.
/// </summary>
public sealed class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    public event Action<Vector2> OnMoveChanged;
    public event Action<Vector2> OnPointerPositionChanged;
    public event Action OnLeftClickPressed;
    public event Action OnRightClickPressed;
    public event Action<int> OnSkillSlotPressed;
    public event Action<int> OnSceneCheatPressed;

    private DefaultInputActions _defaultInputActions;
    private Vector2 _lastPointerScreenPosition;
    private bool _hasPointerScreenPosition;

    public bool TryGetPointerScreenPosition(out Vector2 pointerScreenPosition)
    {
        pointerScreenPosition = _lastPointerScreenPosition;
        return _hasPointerScreenPosition;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureInputManagerExists()
    {
        if (Instance != null)
        {
            return;
        }

        var inputManagerGameObject = new GameObject(nameof(InputManager));
        inputManagerGameObject.AddComponent<InputManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        _defaultInputActions = new DefaultInputActions();
    }

    private void OnEnable()
    {
        if (_defaultInputActions == null)
        {
            _defaultInputActions = new DefaultInputActions();
        }

        SubscribeGameplayInputEvents();
        _defaultInputActions.Enable();
    }

    private void OnDisable()
    {
        if (_defaultInputActions == null)
        {
            return;
        }

        UnsubscribeGameplayInputEvents();
        _defaultInputActions.Disable();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (_defaultInputActions != null)
        {
            UnsubscribeGameplayInputEvents();
            _defaultInputActions.Dispose();
            _defaultInputActions = null;
        }
    }

    private void SubscribeGameplayInputEvents()
    {
        var gameplay = _defaultInputActions.Gameplay;
        gameplay.Move.performed += OnMovePerformedOrCanceled;
        gameplay.Move.canceled += OnMovePerformedOrCanceled;
        gameplay.PointerPosition.performed += OnPointerPositionPerformedOrCanceled;
        gameplay.PointerPosition.canceled += OnPointerPositionPerformedOrCanceled;
        gameplay.LeftClick.performed += OnLeftClickPerformed;
        gameplay.RightClick.performed += OnRightClickPerformed;
        gameplay.SkillSlot1.performed += OnSkillSlot1Performed;
        gameplay.SkillSlot2.performed += OnSkillSlot2Performed;
        gameplay.SkillSlot3.performed += OnSkillSlot3Performed;
        gameplay.SkillSlot4.performed += OnSkillSlot4Performed;
        gameplay.SkillSlot5.performed += OnSkillSlot5Performed;
        gameplay.SkillSlot6.performed += OnSkillSlot6Performed;
        gameplay.SkillSlot7.performed += OnSkillSlot7Performed;
        gameplay.SceneCheat1.performed += OnSceneCheat1Performed;
        gameplay.SceneCheat2.performed += OnSceneCheat2Performed;
        gameplay.SceneCheat3.performed += OnSceneCheat3Performed;
        gameplay.SceneCheat4.performed += OnSceneCheat4Performed;
        gameplay.SceneCheat5.performed += OnSceneCheat5Performed;
    }

    private void UnsubscribeGameplayInputEvents()
    {
        var gameplay = _defaultInputActions.Gameplay;
        gameplay.Move.performed -= OnMovePerformedOrCanceled;
        gameplay.Move.canceled -= OnMovePerformedOrCanceled;
        gameplay.PointerPosition.performed -= OnPointerPositionPerformedOrCanceled;
        gameplay.PointerPosition.canceled -= OnPointerPositionPerformedOrCanceled;
        gameplay.LeftClick.performed -= OnLeftClickPerformed;
        gameplay.RightClick.performed -= OnRightClickPerformed;
        gameplay.SkillSlot1.performed -= OnSkillSlot1Performed;
        gameplay.SkillSlot2.performed -= OnSkillSlot2Performed;
        gameplay.SkillSlot3.performed -= OnSkillSlot3Performed;
        gameplay.SkillSlot4.performed -= OnSkillSlot4Performed;
        gameplay.SkillSlot5.performed -= OnSkillSlot5Performed;
        gameplay.SkillSlot6.performed -= OnSkillSlot6Performed;
        gameplay.SkillSlot7.performed -= OnSkillSlot7Performed;
        gameplay.SceneCheat1.performed -= OnSceneCheat1Performed;
        gameplay.SceneCheat2.performed -= OnSceneCheat2Performed;
        gameplay.SceneCheat3.performed -= OnSceneCheat3Performed;
        gameplay.SceneCheat4.performed -= OnSceneCheat4Performed;
        gameplay.SceneCheat5.performed -= OnSceneCheat5Performed;
    }

    private void OnMovePerformedOrCanceled(InputAction.CallbackContext callbackContext)
    {
        OnMoveChanged?.Invoke(callbackContext.ReadValue<Vector2>());
    }

    private void OnPointerPositionPerformedOrCanceled(InputAction.CallbackContext callbackContext)
    {
        _lastPointerScreenPosition = callbackContext.ReadValue<Vector2>();
        _hasPointerScreenPosition = true;
        OnPointerPositionChanged?.Invoke(_lastPointerScreenPosition);
    }

    private void OnLeftClickPerformed(InputAction.CallbackContext callbackContext) => OnLeftClickPressed?.Invoke();
    private void OnRightClickPerformed(InputAction.CallbackContext callbackContext) => OnRightClickPressed?.Invoke();

    private void OnSkillSlot1Performed(InputAction.CallbackContext callbackContext) => OnSkillSlotPressed?.Invoke(0);
    private void OnSkillSlot2Performed(InputAction.CallbackContext callbackContext) => OnSkillSlotPressed?.Invoke(1);
    private void OnSkillSlot3Performed(InputAction.CallbackContext callbackContext) => OnSkillSlotPressed?.Invoke(2);
    private void OnSkillSlot4Performed(InputAction.CallbackContext callbackContext) => OnSkillSlotPressed?.Invoke(3);
    private void OnSkillSlot5Performed(InputAction.CallbackContext callbackContext) => OnSkillSlotPressed?.Invoke(4);
    private void OnSkillSlot6Performed(InputAction.CallbackContext callbackContext) => OnSkillSlotPressed?.Invoke(5);
    private void OnSkillSlot7Performed(InputAction.CallbackContext callbackContext) => OnSkillSlotPressed?.Invoke(6);

    private void OnSceneCheat1Performed(InputAction.CallbackContext callbackContext) => OnSceneCheatPressed?.Invoke(0);
    private void OnSceneCheat2Performed(InputAction.CallbackContext callbackContext) => OnSceneCheatPressed?.Invoke(1);
    private void OnSceneCheat3Performed(InputAction.CallbackContext callbackContext) => OnSceneCheatPressed?.Invoke(2);
    private void OnSceneCheat4Performed(InputAction.CallbackContext callbackContext) => OnSceneCheatPressed?.Invoke(3);
    private void OnSceneCheat5Performed(InputAction.CallbackContext callbackContext) => OnSceneCheatPressed?.Invoke(4);
}
