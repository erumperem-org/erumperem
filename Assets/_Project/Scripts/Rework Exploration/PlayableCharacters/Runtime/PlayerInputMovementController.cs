using UnityEngine;
using UnityEngine.InputSystem;
using BarSystem.Bars.Stamina;

/// <summary>
/// Controlador do papel "Em Jogo": lê o New Input System e comanda o
/// PhysicsMovementService. Segue o padrão de inscrição/remoção explícita de
/// callbacks (OnEnable/OnDisable), em vez de polling constante - o próprio
/// ciclo de vida do Unity já cuida disso sempre que PlayableCharacters liga
/// ou desliga este componente ao trocar de papel.
///
/// IMPORTANTE: moveAction/sprintAction normalmente referenciam a MESMA
/// InputAction do Input Actions Asset do projeto, compartilhada entre todos
/// os PlayableCharacters (só existe um "Move"/"Sprint" no projeto, não uma
/// cópia por personagem). Por isso o Disable() da ação NUNCA é chamado aqui
/// - Disable() afeta a ação inteira, não um componente isolado, e chamá-lo
/// no OnDisable de um personagem pode desabilitar a ação para outro
/// personagem que acabou de habilitá-la (ex: durante um swap). Enable() é
/// idempotente e seguro de chamar repetidamente, então continua no
/// OnEnable; o controle real de "quem responde a input agora" é feito só
/// pela assinatura/remoção dos callbacks.
/// </summary>
[RequireComponent(typeof(PhysicsMovementService))]
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
    [Tooltip("Estamina mínima (valor absoluto, mesma escala do BarConfigSO) necessária para retomar o sprint depois de ficar exausto. Evita ligar/desligar o sprint repetidamente perto do zero.")]
    [SerializeField] private float minStaminaToResumeSprint = 15f;
    private PhysicsMovementService movement;
    private Vector2 rawMoveInput;
    private bool isSprintKeyHeld;
    private bool isStaminaExhausted;

    private void Awake()
    {
        movement = GetComponent<PhysicsMovementService>();
    }

    private void OnEnable()
    {
        if (moveAction != null)
        {
            moveAction.action.Enable(); // idempotente - seguro mesmo se já habilitada por outro personagem
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

        if (movement == null)
        {
            return;
        }

        movement.SetMoveDirection(Vector3.zero);
        movement.SetSprinting(false);
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
        movement.SetMoveDirection(ConvertToWorldDirection(rawMoveInput));
        UpdateSprint();
    }

    private void UpdateSprint()
    {
        if (staminaBar == null)
        {
            movement.SetSprinting(isSprintKeyHeld);
            return;
        }

        float currentStamina = staminaBar.Model.Current;

        // Histerese: só volta a permitir sprint depois de acumular
        // minStaminaToResumeSprint, não assim que sair de zero - evita o
        // sprint ligando/desligando repetidamente perto do fundo da barra.
        if (isStaminaExhausted && currentStamina >= minStaminaToResumeSprint)
        {
            isStaminaExhausted = false;
        }
        else if (!isStaminaExhausted && currentStamina <= staminaBar.Model.Min)
        {
            isStaminaExhausted = true;
        }

        bool isSprinting = isSprintKeyHeld && !isStaminaExhausted;
        movement.SetSprinting(isSprinting);

        if (isSprinting)
        {
            staminaBar.Consume(staminaCostPerSecond * Time.deltaTime);
        }
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