using System;
using UnityEngine;

[RequireComponent(typeof(PhysicsMovementService))]
[RequireComponent(typeof(CharacterStateExposed))]
public class RestingMovementController : MonoBehaviour
{
    private PhysicsMovementService movement;
    private CharacterStateExposed characterState;

    private PlayableCharacterSettings settings;
    private Transform restingPoint;
    private Action onArrived;

    // Fallback manual para o líder (SetYieldTarget), usado se o
    // PlayableCharacterController não fornecer o personagem Em Jogo.
    private Transform manualLeaderTarget;

    private PlayableCharacterController characterController;
    private Transform leaderTarget;
    private Transform companionTarget;
    private float nextControllerSearchTime;

    private bool arrivalNotified;
    private bool warnedMissingTarget;

    [Header("Debug")]
    [SerializeField] private bool debugEnabled = true;
    [SerializeField] private float debugInterval = 0.5f;

    private float nextDebugTime;

    public void Initialize(
        Transform destinationPoint,
        PlayableCharacterSettings characterSettings,
        Action onArrivedCallback)
    {
        movement = GetComponent<PhysicsMovementService>();
        characterState = GetComponent<CharacterStateExposed>();

        restingPoint = destinationPoint;
        settings = characterSettings;
        onArrived = onArrivedCallback;

        movement.SetValidator(
            new ObstacleAvoidanceValidator(
                settings.maxAvoidanceAngle,
                settings.avoidanceAngleStep
            )
        );

        DebugLog("Initialized.");
    }

    /// <summary>
    /// Fallback manual para o alvo-líder. Normalmente os alvos (Em Jogo e
    /// Companheiro) são resolvidos automaticamente pelo
    /// PlayableCharacterController.
    /// </summary>
    public void SetYieldTarget(Transform target)
    {
        manualLeaderTarget = target;

        DebugLog(
            $"SetYieldTarget: {(target != null ? target.name : "NULL")}"
        );
    }

    public void BeginResting()
    {
        arrivalNotified = false;

        SetMovementState(
            CharacterStateExposed.CharacterMovementState.Walk
        );

        DebugLog("BeginResting called.");
    }

    private void OnEnable()
    {
        DebugLog("Controller enabled.");
    }

    private void OnDisable()
    {
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

        DebugLog("Controller disabled.");
    }

    private void FixedUpdate()
    {
        if (restingPoint == null)
        {
            movement.SetMoveDirection(Vector3.zero);
            movement.SetSprinting(false);

            SetMovementState(
                CharacterStateExposed.CharacterMovementState.Idle
            );

            DebugLogThrottled("RestingPoint is NULL.");
            return;
        }

        ResolveYieldTargets();

        Transform closestTarget = GetClosestTarget(out float distance);

        if (closestTarget == null)
        {
            if (!warnedMissingTarget)
            {
                warnedMissingTarget = true;

                Debug.LogWarning(
                    $"[Resting:{name}] Nenhum alvo de desvio disponível " +
                    "(Em Jogo / Companheiro). Sidestep desativado.",
                    this
                );
            }

            MoveTowardsRestingPoint();
            return;
        }

        warnedMissingTarget = false;

        DebugLogThrottled(
            $"Closest target: {closestTarget.name} | " +
            $"Distance: {distance:F2} | " +
            $"PathClear: {settings.pathClearDistance:F2} | " +
            $"Stop: {settings.stopDistance:F2}"
        );

        if (distance <= settings.pathClearDistance)
        {
            Vector3 direction =
                ComputeSidestepDirection(closestTarget);

            DebugLogThrottled(
                $"SIDESTEP from {closestTarget.name} | " +
                $"Direction: {direction} | " +
                $"Magnitude: {direction.magnitude:F2}"
            );

            movement.SetMoveDirection(direction);
            movement.SetSprinting(false);

            SetMovementState(
                CharacterStateExposed.CharacterMovementState.Walk
            );

            return;
        }

        if (distance <= settings.stopDistance)
        {
            DebugLogThrottled("IDLE | Waiting for target to move away.");

            movement.SetMoveDirection(Vector3.zero);
            movement.SetSprinting(false);

            SetMovementState(
                CharacterStateExposed.CharacterMovementState.Idle
            );

            return;
        }

        DebugLogThrottled("RETURNING TO RESTING POINT.");

        MoveTowardsRestingPoint();
    }

    // ── Alvos de desvio ──────────────────────────────────────────

    /// <summary>
    /// Atualiza as referências do personagem Em Jogo e do Companheiro
    /// a cada tick, para acompanhar trocas de personagem.
    /// </summary>
    private void ResolveYieldTargets()
    {
        if (characterController == null)
        {
            // Evita FindObjectOfType a cada FixedUpdate quando não existe.
            if (Time.time >= nextControllerSearchTime)
            {
                nextControllerSearchTime = Time.time + 0.5f;
                characterController =
                    FindObjectOfType<PlayableCharacterController>();
            }
        }

        leaderTarget = null;
        companionTarget = null;

        if (characterController != null)
        {
            var inGame = characterController.InGameCharacter;
            if (inGame != null)
            {
                leaderTarget = inGame.transform;
            }

            var companion = characterController.CompanionCharacter;
            if (companion != null)
            {
                companionTarget = companion.transform;
            }
        }

        if (leaderTarget == null)
        {
            leaderTarget = manualLeaderTarget;
        }

        // Nunca desviar de si mesmo.
        if (leaderTarget == transform) leaderTarget = null;
        if (companionTarget == transform) companionTarget = null;
    }

    /// <summary>
    /// Retorna o alvo de desvio mais próximo (Em Jogo ou Companheiro)
    /// e a distância até ele. Null se nenhum estiver disponível.
    /// </summary>
    private Transform GetClosestTarget(out float distance)
    {
        distance = float.MaxValue;
        Transform closest = null;

        TryPickCloser(leaderTarget, ref closest, ref distance);
        TryPickCloser(companionTarget, ref closest, ref distance);

        return closest;
    }

    private void TryPickCloser(
        Transform candidate,
        ref Transform closest,
        ref float closestDistance)
    {
        if (candidate == null) return;

        float candidateDistance = Vector3.Distance(
            transform.position,
            candidate.position
        );

        if (candidateDistance < closestDistance)
        {
            closest = candidate;
            closestDistance = candidateDistance;
        }
    }

    // ── Movimento ────────────────────────────────────────────────

    private void MoveTowardsRestingPoint()
    {
        Vector3 flatDelta =
            restingPoint.position - transform.position;

        flatDelta.y = 0f;

        float arrivalDistance =
            settings.restingArrivalThreshold;

        if (flatDelta.sqrMagnitude <=
            arrivalDistance * arrivalDistance)
        {
            movement.SetMoveDirection(Vector3.zero);
            movement.SetSprinting(false);

            SetMovementState(
                CharacterStateExposed.CharacterMovementState.Idle
            );

            if (!arrivalNotified)
            {
                arrivalNotified = true;

                DebugLog("ARRIVED at resting point.");

                onArrived?.Invoke();
            }

            return;
        }

        Vector3 moveDirection =
            DirectionTo(restingPoint.position);

        movement.SetMoveDirection(moveDirection);
        movement.SetSprinting(false);

        SetMovementState(
            CharacterStateExposed.CharacterMovementState.Walk
        );
    }

    private void SetMovementState(
        CharacterStateExposed.CharacterMovementState state)
    {
        if (characterState == null)
            return;

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
    /// Direção lateral perpendicular ao forward do alvo, para o lado
    /// em que este personagem já está (evita alternar de lado a cada frame).
    /// </summary>
    private Vector3 ComputeSidestepDirection(Transform target)
    {
        Vector3 targetForward = target.forward;

        targetForward.y = 0f;

        if (targetForward.sqrMagnitude < 0.0001f)
        {
            targetForward = Vector3.forward;
        }
        else
        {
            targetForward.Normalize();
        }

        Vector3 sideAxis =
            Vector3.Cross(Vector3.up, targetForward);

        Vector3 toSelf =
            transform.position - target.position;

        toSelf.y = 0f;

        float side =
            Vector3.Dot(toSelf, sideAxis);

        float sideSign =
            Mathf.Abs(side) > 0.01f
                ? Mathf.Sign(side)
                : 1f;

        return sideAxis * sideSign;
    }

    // ── Debug ────────────────────────────────────────────────────

    private void DebugLog(string message)
    {
        if (!debugEnabled)
            return;

        Debug.Log($"[Resting:{name}] {message}", this);
    }

    private void DebugLogThrottled(string message)
    {
        if (!debugEnabled || Time.time < nextDebugTime)
            return;

        nextDebugTime = Time.time + debugInterval;

        Debug.Log($"[Resting:{name}] {message}", this);
    }
}