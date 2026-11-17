
using UnityEngine;
using UnityEngine.InputSystem;
using BarSystem.Bars.Stamina;
using Player;

[RequireComponent(typeof(PhysicsMovementService))]
[RequireComponent(typeof(CharacterStateExposed))]
public class PlayerInputMovementController : MonoBehaviour
{
    [Header("Input Actions")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference sprintAction;

    [Header("Camera")]
    [SerializeField] private Transform cameraTransform;

    [Header("Stamina (Sprint)")]
    [SerializeField] private PlayableCharacterStaminaBarInstaller staminaBar;

    public PlayableCharacterStaminaBarInstaller StaminaBar => staminaBar;

    [SerializeField] private float staminaCostPerSecond = 20f;
    [SerializeField] private float minStaminaToResumeSprint = 15f;

    private PhysicsMovementService movement;
    private CharacterStateExposed characterState;
    private Rigidbody playerRigidbody;
    private PlayerInputReader inputReader;
    private Panel pausePanel;

    private Vector2 rawMoveInput;
    private bool isSprintKeyHeld;
    private bool isStaminaExhausted;

    private void Awake()
    {
        movement = GetComponent<PhysicsMovementService>();
        characterState = GetComponent<CharacterStateExposed>();
        playerRigidbody = GetComponent<Rigidbody>();
        inputReader = GetComponentInParent<PlayerInputReader>();
    }

    private void OnEnable()
    {
        if (moveAction != null)
        {
            moveAction.action.Enable();
            moveAction.action.performed += OnMovePerformed;
            moveAction.action.canceled += OnMoveCanceled;
        }

        if (sprintAction != null)
        {
            sprintAction.action.Enable();
            sprintAction.action.performed += OnSprintPerformed;
            sprintAction.action.canceled += OnSprintCanceled;
        }
    }

    private void OnDisable()
    {
        if (moveAction != null)
        {
            moveAction.action.performed -= OnMovePerformed;
            moveAction.action.canceled -= OnMoveCanceled;
        }

        if (sprintAction != null)
        {
            sprintAction.action.performed -= OnSprintPerformed;
            sprintAction.action.canceled -= OnSprintCanceled;
        }

        rawMoveInput = Vector2.zero;
        isSprintKeyHeld = false;

        if (movement != null)
        {
            movement.SetMoveDirection(Vector3.zero);
            movement.SetSprinting(false);
        }

        if (characterState != null)
        {
            characterState.SetMovementState(CharacterStateExposed.CharacterMovementState.Idle);
        }
    }

    private void OnMovePerformed(InputAction.CallbackContext context)
    {
        rawMoveInput = context.ReadValue<Vector2>();
    }

    private void OnMoveCanceled(InputAction.CallbackContext context)
    {
        rawMoveInput = Vector2.zero;
    }

    private void OnSprintPerformed(InputAction.CallbackContext context)
    {
        isSprintKeyHeld = true;
    }

    private void OnSprintCanceled(InputAction.CallbackContext context)
    {
        isSprintKeyHeld = false;
    }

    private void Update()
    {
        if (IsWorldInputBlocked())
        {
            StopMovementForUi();
            return;
        }

        Vector3 moveDirection = ConvertToWorldDirection(rawMoveInput);

        movement.SetMoveDirection(moveDirection);
        UpdateSprint(moveDirection);
    }

    private bool IsWorldInputBlocked()
    {
        if (GlobalUiKeyboardNavigation.IsAnyUiMenuOpen || InputBindingsService.IsRebinding)
        {
            return true;
        }

        if (inputReader == null)
        {
            inputReader = GetComponentInParent<PlayerInputReader>();
        }

        if (inputReader != null && inputReader.IsBlocked)
        {
            return true;
        }

        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive)
        {
            return true;
        }

        if (FindFirstObjectByType<CharacterSelectionPanel>() != null)
        {
            return true;
        }

        return IsPauseOpen();
    }

    private bool IsPauseOpen()
    {
        if (pausePanel == null)
        {
            Panel[] panels = FindObjectsByType<Panel>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (Panel panel in panels)
            {
                if (panel != null && panel.gameObject.scene.IsValid() && panel.gameObject.name.StartsWith("PausePanel", System.StringComparison.Ordinal))
                {
                    pausePanel = panel;
                    break;
                }
            }
        }

        return pausePanel != null && pausePanel.gameObject.activeInHierarchy;
    }

    private void StopMovementForUi()
    {
        movement.SetMoveDirection(Vector3.zero);
        movement.SetSprinting(false);
        characterState.SetMovementState(CharacterStateExposed.CharacterMovementState.Idle);

        if (playerRigidbody != null)
        {
            Vector3 velocity = playerRigidbody.linearVelocity;
            playerRigidbody.linearVelocity = new Vector3(0f, velocity.y, 0f);
        }
    }

    private void UpdateSprint(Vector3 moveDirection)
    {
        bool hasMovementInput = moveDirection.sqrMagnitude > 0.0001f;

        if (staminaBar == null)
        {
            bool shouldSprintWithoutStamina = isSprintKeyHeld && hasMovementInput;

            movement.SetSprinting(shouldSprintWithoutStamina);
            UpdateMovementState(hasMovementInput, shouldSprintWithoutStamina);
            return;
        }

        float currentStamina = staminaBar.Model.Current;

        if (isStaminaExhausted && currentStamina >= minStaminaToResumeSprint)
        {
            isStaminaExhausted = false;
        }
        else if (!isStaminaExhausted && currentStamina <= staminaBar.Model.Min)
        {
            isStaminaExhausted = true;
        }

        bool isSprinting = isSprintKeyHeld && !isStaminaExhausted && hasMovementInput;

        movement.SetSprinting(isSprinting);

        if (isSprinting)
        {
            staminaBar.Consume(staminaCostPerSecond * Time.deltaTime);
        }

        UpdateMovementState(hasMovementInput, isSprinting);
    }

    private void UpdateMovementState(bool hasMovementInput, bool isSprinting)
    {
        if (!hasMovementInput)
        {
            characterState.SetMovementState(CharacterStateExposed.CharacterMovementState.Idle);
            return;
        }

        characterState.SetMovementState(isSprinting
            ? CharacterStateExposed.CharacterMovementState.Run
            : CharacterStateExposed.CharacterMovementState.Walk);
    }

    private Vector3 ConvertToWorldDirection(Vector2 input)
    {
        if (input.sqrMagnitude < 0.0001f)
        {
            return Vector3.zero;
        }

        Vector3 forward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
        Vector3 right = cameraTransform != null ? cameraTransform.right : Vector3.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        Vector3 direction = forward * input.y + right * input.x;

        return Vector3.ClampMagnitude(direction, 1f);
    }
}
