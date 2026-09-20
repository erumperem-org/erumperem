using UnityEngine;

/// <summary>
/// Controlador do papel "Companheiro": persegue o personagem Em Jogo
/// eternamente, desviando de obstáculos no caminho.
///
/// O estado de movimento do personagem é exposto através do
/// CharacterStateExposed:
/// - Idle: parado ou sem alvo.
/// - Walk: seguindo o líder normalmente.
/// - Run: realizando catch-up através de sprint.
///
/// Utiliza histerese entre duas distâncias para evitar que o sprint
/// fique alternando próximo ao limite.
/// </summary>
[RequireComponent(typeof(PhysicsMovementService))]
[RequireComponent(typeof(CharacterStateExposed))]
public class CompanionFollowController : MonoBehaviour
{
    private PhysicsMovementService movement;
    private CharacterStateExposed characterState;

    private PlayableCharacterSettings settings;
    private Transform followTarget;

    private bool isSprinting;

    /// <summary>
    /// Chamado uma vez pelo PlayableCharacters no Awake.
    /// </summary>
    public void Initialize(
        PlayableCharacterSettings characterSettings)
    {
        movement = GetComponent<PhysicsMovementService>();
        characterState = GetComponent<CharacterStateExposed>();

        settings = characterSettings;

        movement.SetValidator(
            new ObstacleAvoidanceValidator(
                settings.maxAvoidanceAngle,
                settings.avoidanceAngleStep
            )
        );
    }

    /// <summary>
    /// Define quem este companheiro deve seguir.
    /// Deve ser atualizado sempre que o personagem Em Jogo mudar.
    /// </summary>
    public void SetFollowTarget(Transform target)
    {
        followTarget = target;
    }

    private void OnEnable()
    {
        isSprinting = false;
    }

    private void OnDisable()
    {
        isSprinting = false;

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

    private void FixedUpdate()
    {
        if (followTarget == null)
        {
            movement.SetMoveDirection(Vector3.zero);
            movement.SetSprinting(false);

            SetMovementState(
                CharacterStateExposed.CharacterMovementState.Idle
            );

            return;
        }

        float distance = Vector3.Distance(
            transform.position,
            followTarget.position
        );

        UpdateSprint(distance);

        if (distance <= settings.pathClearDistance)
        {
            Vector3 direction = ComputeSidestepDirection();

            movement.SetMoveDirection(direction);
            movement.SetSprinting(isSprinting);

            SetMovementState(
                isSprinting
                    ? CharacterStateExposed.CharacterMovementState.Run
                    : CharacterStateExposed.CharacterMovementState.Walk
            );

            return;
        }

        if (distance <= settings.stopDistance)
        {
            movement.SetMoveDirection(Vector3.zero);
            movement.SetSprinting(false);

            SetMovementState(
                CharacterStateExposed.CharacterMovementState.Idle
            );

            return;
        }

        Vector3 moveDirection =
            DirectionTo(followTarget.position);

        movement.SetMoveDirection(moveDirection);
        movement.SetSprinting(isSprinting);

        SetMovementState(
            isSprinting
                ? CharacterStateExposed.CharacterMovementState.Run
                : CharacterStateExposed.CharacterMovementState.Walk
        );
    }

    private void UpdateSprint(float distance)
    {
        if (!isSprinting &&
            distance >= settings.catchUpTriggerDistance)
        {
            isSprinting = true;
        }
        else if (isSprinting &&
                 distance <= settings.catchUpRecoverDistance)
        {
            isSprinting = false;
        }
    }

    private void SetMovementState(
        CharacterStateExposed.CharacterMovementState state)
    {
        if (characterState == null)
        {
            return;
        }

        characterState.SetMovementState(state);
    }

    private Vector3 DirectionTo(Vector3 worldPosition)
    {
        Vector3 flatDelta =
            worldPosition - transform.position;

        flatDelta.y = 0f;

        return flatDelta.sqrMagnitude > 0.0001f
            ? flatDelta.normalized
            : Vector3.zero;
    }

    /// <summary>
    /// Calcula uma direção lateral perpendicular ao forward do líder,
    /// para o lado em que o companheiro já está.
    ///
    /// Isso evita alternar de lado a cada frame.
    /// </summary>
    private Vector3 ComputeSidestepDirection()
    {
        Vector3 leaderForward = followTarget.forward;

        leaderForward.y = 0f;

        if (leaderForward.sqrMagnitude < 0.0001f)
        {
            leaderForward = Vector3.forward;
        }
        else
        {
            leaderForward.Normalize();
        }

        Vector3 sideAxis =
            Vector3.Cross(Vector3.up, leaderForward);

        Vector3 toCompanion =
            transform.position - followTarget.position;

        toCompanion.y = 0f;

        float side =
            Vector3.Dot(toCompanion, sideAxis);

        float sideSign =
            Mathf.Abs(side) > 0.01f
                ? Mathf.Sign(side)
                : 1f;

        return sideAxis * sideSign;
    }
}