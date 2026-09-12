using UnityEngine;

/// <summary>
/// Motor de movimentação por física, baseado em Rigidbody.
///
/// Este componente NÃO sabe nada sobre teclado, gamepad, IA, câmera ou
/// animação — ele só entende uma API de comandos (<see cref="SetMoveDirection"/>,
/// <see cref="SetSprinting"/>) e aplica isso ao Rigidbody a cada FixedUpdate.
///
/// Qualquer script pode "plugar" neste serviço chamando os métodos públicos.
/// Veja os exemplos em Controllers/ (KeyboardMovementController,
/// SimpleAIMovementController) para referência de como escrever o seu.
///
/// Também suporta uma camada opcional de validação de movimento
/// (<see cref="IMovementValidator"/>) que corrige a direção desejada antes de
/// virar velocidade, evitando que o personagem fique empurrando
/// indefinidamente contra um colisor. Veja Validation/.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[DisallowMultipleComponent]
public class PhysicsMovementService : MonoBehaviour
{
    /// <summary>Presets prontos para a camada de validação, selecionáveis pelo Inspector.</summary>
    public enum ValidatorPreset
    {
        None,
        WallSlide,
        HardStop,
        /// <summary>Nenhum preset é instanciado automaticamente; use <see cref="SetValidator"/> via código.</summary>
        Custom
    }

    [SerializeField] private MovementSettings settings;

    [Header("Validação de movimento (opcional)")]
    [Tooltip("Preset de validação aplicado automaticamente no Awake, caso nenhum validador seja atribuído via código antes disso.")]
    [SerializeField] private ValidatorPreset validatorPreset = ValidatorPreset.WallSlide;

    // Estratégia de validação atualmente ativa. Pode ser trocada em runtime
    // via SetValidator(...), inclusive por uma implementação customizada que
    // não faça parte dos presets acima.
    [SerializeReference] private IMovementValidator validator;

    private Rigidbody rb;

    // Estado de input, alimentado por quem estiver "plugado" no serviço.
    private Vector3 desiredDirection; // world-space, magnitude 0..1
    private bool sprinting;

    public bool IsGrounded { get; private set; }
    public Vector3 GroundNormal { get; private set; } = Vector3.up;
    public Vector3 HorizontalVelocity => new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
    public float CurrentSpeed => HorizontalVelocity.magnitude;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true; // rotação é controlada manualmente em HandleRotation

        if (validator == null)
            validator = CreateValidatorFromPreset(validatorPreset);
    }

    private IMovementValidator CreateValidatorFromPreset(ValidatorPreset preset)
    {
        switch (preset)
        {
            case ValidatorPreset.WallSlide:
                return new WallSlideValidator();
            case ValidatorPreset.HardStop:
                return new HardStopValidator();
            case ValidatorPreset.None:
            case ValidatorPreset.Custom:
            default:
                return null;
        }
    }

    // ---------------------------------------------------------------
    // API pública — é isso que qualquer controlador (teclado, IA, rede,
    // replay, cutscene...) deve usar para operar o personagem.
    // ---------------------------------------------------------------

    /// <summary>
    /// Define a direção de movimento desejada, em world-space.
    /// A magnitude do vetor (0 a 1) representa a intensidade do movimento,
    /// permitindo que analógicos de gamepad funcionem naturalmente.
    /// O eixo Y é sempre ignorado (zerado internamente).
    /// </summary>
    public void SetMoveDirection(Vector3 worldDirection)
    {
        worldDirection.y = 0f;
        desiredDirection = Vector3.ClampMagnitude(worldDirection, 1f);
    }

    /// <summary>Ativa/desativa o modo sprint (usa sprintSpeed em vez de walkSpeed).</summary>
    public void SetSprinting(bool value) => sprinting = value;

    /// <summary>
    /// Troca a estratégia de validação de movimento em runtime. Passe null
    /// para desativar a validação por completo.
    /// </summary>
    public void SetValidator(IMovementValidator newValidator) => validator = newValidator;

    // ---------------------------------------------------------------
    // Loop de física
    // ---------------------------------------------------------------

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        CheckGround();

        Vector3 validatedDirection = ApplyValidation(desiredDirection);

        HandleHorizontalMovement(validatedDirection, dt);
        HandleRotation(validatedDirection, dt);
    }

    private void CheckGround()
    {
        Vector3 origin = transform.position + Vector3.up * settings.groundCheckRadius;
        bool grounded = Physics.SphereCast(
            origin, settings.groundCheckRadius, Vector3.down,
            out RaycastHit hit, settings.groundCheckDistance, settings.groundMask, QueryTriggerInteraction.Ignore);

        IsGrounded = grounded;
        GroundNormal = grounded ? hit.normal : Vector3.up;
    }

    private Vector3 ApplyValidation(Vector3 direction)
    {
        if (validator == null || direction.sqrMagnitude < 0.0001f)
            return direction;

        Vector3 origin = transform.position + Vector3.up * settings.obstacleCastHeight;
        return validator.Validate(
            direction, origin,
            settings.obstacleCastRadius, settings.obstacleCheckDistance, settings.obstacleMask);
    }

    private void HandleHorizontalMovement(Vector3 direction, float dt)
    {
        bool hasInput = direction.sqrMagnitude > 0.0001f;
        float targetSpeed = (sprinting ? settings.sprintSpeed : settings.walkSpeed) * direction.magnitude;
        Vector3 targetVelocity = hasInput ? direction.normalized * targetSpeed : Vector3.zero;

        Vector3 currentVelocity = HorizontalVelocity;
        float rate = hasInput ? settings.acceleration : settings.deceleration;
        Vector3 newHorizontal = Vector3.MoveTowards(currentVelocity, targetVelocity, rate * dt);

        Vector3 finalVelocity = newHorizontal;
        finalVelocity.y = rb.linearVelocity.y; // eixo vertical fica por conta da gravidade/física padrão do Rigidbody
        rb.linearVelocity = finalVelocity;
    }

    private void HandleRotation(Vector3 direction, float dt)
    {
        if (!settings.rotateTowardsMovement) return;
        if (direction.sqrMagnitude < 0.0001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, settings.rotationSpeed * dt));
    }

    private void OnDrawGizmos()
    {
        if (settings == null) return;

        // Chão
        Gizmos.color = Application.isPlaying && IsGrounded ? Color.green : Color.yellow;
        Vector3 groundOrigin = transform.position + Vector3.up * settings.groundCheckRadius;
        Gizmos.DrawWireSphere(groundOrigin + Vector3.down * settings.groundCheckDistance, settings.groundCheckRadius);

        // Obstáculo
        Gizmos.color = Color.cyan;
        Vector3 obstacleOrigin = transform.position + Vector3.up * settings.obstacleCastHeight;
        Gizmos.DrawWireSphere(obstacleOrigin, settings.obstacleCastRadius);
    }
}
