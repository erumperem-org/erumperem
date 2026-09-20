using UnityEngine;
using UnityEngine.InputSystem;
using BarSystem.Bars.Stamina;

/// <summary>
/// Controlador do papel "Em Jogo": lê o New Input System e comanda o
/// PhysicsMovementService.
///
/// O estado de movimento do personagem é exposto através do
/// CharacterStateExposed:
/// - Idle: sem input de movimento.
/// - Walk: movimento normal.
/// - Run: sprint ativo.
///
/// As InputActions são compartilhadas entre os personagens. Por isso,
/// este componente nunca chama Disable() nas ações.
/// </summary>
[RequireComponent(typeof(PhysicsMovementService))]
[RequireComponent(typeof(CharacterStateExposed))]
public class PlayerInputMovementController : MonoBehaviour
{
    [Header("Input Actions")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference sprintAction;

    [Header("Câmera (conversão de input para direção world-space)")]
    [SerializeField] private Transform cameraTransform;

    [Header("Stamina (Sprint)")]
    [Tooltip("Opcional - se não for atribuído, o sprint funciona sem nenhuma restrição de stamina.")]
    [SerializeField] private PlayableCharacterStaminaBarInstaller staminaBar;

    public PlayableCharacterStaminaBarInstaller StaminaBar => staminaBar;

    [SerializeField] private float staminaCostPerSecond = 20f;

    [Tooltip("Estamina mínima necessária para retomar o sprint depois de ficar exausto.")]
    [SerializeField] private float minStaminaToResumeSprint = 15f;

    private PhysicsMovementService movement;
    private CharacterStateExposed characterState;

    private Vector2 rawMoveInput;
    private bool isSprintKeyHeld;
    private bool isStaminaExhausted;

    private void Awake()
    {
        movement = GetComponent<PhysicsMovementService>();
        characterState = GetComponent<CharacterStateExposed>();
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
            characterState.SetMovementState(
                CharacterStateExposed.CharacterMovementState.Idle
            );
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
        Vector3 moveDirection = ConvertToWorldDirection(rawMoveInput);

        movement.SetMoveDirection(moveDirection);

        UpdateSprint(moveDirection);
    }

    private void UpdateSprint(Vector3 moveDirection)
    {
        bool hasMovementInput = moveDirection.sqrMagnitude > 0.0001f;

        if (staminaBar == null)
        {
            bool isOnSprinting = isSprintKeyHeld && hasMovementInput;

            movement.SetSprinting(isOnSprinting);

            UpdateMovementState(
                hasMovementInput,
                isOnSprinting
            );

            return;
        }

        float currentStamina = staminaBar.Model.Current;

        // Histerese: só volta a permitir sprint depois de acumular
        // minStaminaToResumeSprint.
        if (isStaminaExhausted &&
            currentStamina >= minStaminaToResumeSprint)
        {
            isStaminaExhausted = false;
        }
        else if (!isStaminaExhausted &&
                 currentStamina <= staminaBar.Model.Min)
        {
            isStaminaExhausted = true;
        }

        bool isSprinting =
            isSprintKeyHeld &&
            !isStaminaExhausted &&
            hasMovementInput;

        movement.SetSprinting(isSprinting);

        if (isSprinting)
        {
            staminaBar.Consume(
                staminaCostPerSecond * Time.deltaTime
            );
        }

        UpdateMovementState(
            hasMovementInput,
            isSprinting
        );
    }

    private void UpdateMovementState(
        bool hasMovementInput,
        bool isSprinting)
    {
        if (!hasMovementInput)
        {
            characterState.SetMovementState(
                CharacterStateExposed.CharacterMovementState.Idle
            );

            return;
        }

        characterState.SetMovementState(
            isSprinting
                ? CharacterStateExposed.CharacterMovementState.Run
                : CharacterStateExposed.CharacterMovementState.Walk
        );
    }

    private Vector3 ConvertToWorldDirection(Vector2 input)
    {
        if (input.sqrMagnitude < 0.0001f)
        {
            return Vector3.zero;
        }

        Vector3 forward =
            cameraTransform != null
                ? cameraTransform.forward
                : Vector3.forward;

        Vector3 right =
            cameraTransform != null
                ? cameraTransform.right
                : Vector3.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        Vector3 direction =
            forward * input.y +
            right * input.x;

        return Vector3.ClampMagnitude(direction, 1f);
    }
}