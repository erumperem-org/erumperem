using System;
using UnityEngine;

[RequireComponent(typeof(PhysicsMovementService))]
public class ChaserAI : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private Transform target;
    public Transform Target => target;
    [SerializeField] private ChaserSettings settings;
    [SerializeField] private WanderArea wanderArea;
    [SerializeField] private PerceptionSensor perceptionSensor;
    [Tooltip("A SafeArea/Hub que este Chaser nunca deve atravessar fisicamente. Normalmente a mesma referência usada como excludedArea no WanderArea.")]
    [SerializeField] private CircularZone excludedSafeArea;

    [Header("Debug")]
    [Tooltip("Loga no Console cada transição de estado (Wandering/Chasing/Investigating/Resting). Sem custo em build, é só um Debug.Log.")]
    [SerializeField] private bool enableStateDebugLogs = true;

    private PhysicsMovementService movement;

    private Vector3 currentWanderTarget;
    private Vector3 lastKnownTargetPosition;
    private float investigateTimer;

    public ChaserState CurrentState { get; private set; }

    public event Action OnTargetCaught;

    private void Awake()
    {
        movement = GetComponent<PhysicsMovementService>();
        IMovementValidator obstacleValidator = new ObstacleAvoidanceValidator(settings.maxAvoidanceAngle, settings.avoidanceAngleStep);

        if (excludedSafeArea != null)
        {
            IMovementValidator safeAreaValidator = new SafeAreaAvoidanceValidator(
                excludedSafeArea,
                settings.safeAreaAvoidanceMargin,
                settings.safeAreaAvoidanceMaxSearchAngle,
                settings.avoidanceAngleStep);

            movement.SetValidator(new CompositeMovementValidator(obstacleValidator, safeAreaValidator));
        }
        else
        {
            movement.SetValidator(obstacleValidator);
        }
    }

    private void Start()
    {
        EnterWandering();
    }

    private void FixedUpdate()
    {
        switch (CurrentState)
        {
            case ChaserState.Wandering:
                TickWandering();
                break;
            case ChaserState.Chasing:
                TickChasing();
                break;
            case ChaserState.Investigating:
                TickInvestigating();
                break;
            case ChaserState.Resting:
                TickResting();
                break;
        }
    }

    // ------------------------------------------------------------------
    // Transições de estado
    // ------------------------------------------------------------------

    private void EnterWandering()
    {
        CurrentState = ChaserState.Wandering;
        LogStateChange("Wandering"); // DEBUG: rastreio de estado
        movement.SetSprinting(false);
        perceptionSensor.Activate(target, settings);
        PickNewWanderTarget();
    }

    private void EnterChasing()
    {
        CurrentState = ChaserState.Chasing;
        LogStateChange("Chasing"); // DEBUG: rastreio de estado
        movement.SetSprinting(settings.sprintWhileChasing);
    }

    private void EnterInvestigating()
    {
        CurrentState = ChaserState.Investigating;
        LogStateChange("Investigating"); // DEBUG: rastreio de estado
        investigateTimer = 0f;
        movement.SetSprinting(false);
    }

    public void EnterResting()
    {
        CurrentState = ChaserState.Resting;
        LogStateChange("Resting"); // DEBUG: rastreio de estado
        movement.SetSprinting(false);
        perceptionSensor.Deactivate();

        // Note: movement direction is decided per-frame in TickResting —
        // moves away from target while the distance to it is below
        // settings.restDepartureDistance, then stops once reached.
    }

    public void ExitResting()
    {
        if (CurrentState != ChaserState.Resting)
        {
            return;
        }

        EnterWandering();
    }

    // ------------------------------------------------------------------
    // Lógica de cada estado
    // ------------------------------------------------------------------

    private void TickWandering()
    {
        movement.SetMoveDirection(DirectionTo(currentWanderTarget));

        if (HasArrivedAt(currentWanderTarget))
        {
            PickNewWanderTarget();
        }

        if (perceptionSensor.CanSeeTarget)
        {
            EnterChasing();
        }
    }

    private void TickChasing()
    {
        movement.SetMoveDirection(DirectionTo(target.position));

        float distance = Vector3.Distance(transform.position, target.position);
        if (distance <= settings.catchDistance)
        {
            HandleTargetCaught();
            return;
        }

        if (!perceptionSensor.CanSeeTarget)
        {
            lastKnownTargetPosition = target.position;
            EnterInvestigating();
        }
    }

    private void TickInvestigating()
    {
        if (perceptionSensor.CanSeeTarget)
        {
            EnterChasing();
            return;
        }

        if (!HasArrivedAt(lastKnownTargetPosition))
        {
            movement.SetMoveDirection(DirectionTo(lastKnownTargetPosition));
            return;
        }

        movement.SetMoveDirection(Vector3.zero);
        investigateTimer += Time.fixedDeltaTime;

        if (investigateTimer >= settings.investigateWaitTime)
        {
            EnterWandering();
        }
    }

    private void TickResting()
    {
        if (target == null)
        {
            // No target assigned — nothing to depart from, go idle immediately.
            movement.SetMoveDirection(Vector3.zero);
            return;
        }

        // Only moves away from target while the current distance to it is
        // still below settings.restDepartureDistance. Once that distance
        // is reached or exceeded, it stops for good (no routine).
        if (IsCloserToTargetThanRestDepartureDistance())
        {
            movement.SetMoveDirection(DirectionAwayFromTarget());
        }
        else
        {
            movement.SetMoveDirection(Vector3.zero);
        }
    }

    private void PickNewWanderTarget()
    {
        currentWanderTarget = wanderArea.GetRandomPoint();
    }

    private void HandleTargetCaught()
    {
        movement.SetMoveDirection(Vector3.zero);
        OnTargetCaught?.Invoke();
    }

    // ------------------------------------------------------------------
    // Utilitários
    // ------------------------------------------------------------------

    private Vector3 DirectionTo(Vector3 worldPosition)
    {
        Vector3 flatDelta = worldPosition - transform.position;
        flatDelta.y = 0f;
        return flatDelta.sqrMagnitude > 0.0001f ? flatDelta.normalized : Vector3.zero;
    }

    private bool HasArrivedAt(Vector3 worldPosition)
    {
        Vector3 flatDelta = worldPosition - transform.position;
        flatDelta.y = 0f;
        return flatDelta.sqrMagnitude <= settings.arrivalThreshold * settings.arrivalThreshold;
    }

    private bool IsCloserToTargetThanRestDepartureDistance()
    {
        Vector3 flatDelta = transform.position - target.position;
        flatDelta.y = 0f;
        return flatDelta.sqrMagnitude < settings.restDepartureDistance * settings.restDepartureDistance;
    }

    /// <summary>
    /// Direction pointing from the target to the Chaser's current position
    /// (i.e. straight away from the target). Falls back to the Chaser's own
    /// forward direction in the degenerate case where it's exactly on top
    /// of the target (zero-length delta), to avoid a Vector3.zero move direction.
    /// </summary>
    private Vector3 DirectionAwayFromTarget()
    {
        Vector3 flatDelta = transform.position - target.position;
        flatDelta.y = 0f;

        if (flatDelta.sqrMagnitude <= 0.0001f)
        {
            Vector3 fallback = transform.forward;
            fallback.y = 0f;
            return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.forward;
        }

        return flatDelta.normalized;
    }

    /// <summary>
    /// Permite a um sistema externo (ex: ChaserPool) atualizar dinamicamente
    /// quem este Chaser persegue - necessário porque o personagem "Em Jogo"
    /// pode trocar em runtime (ver PlayableCharacterController).
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    // DEBUG: método único de log, para poder desligar tudo de uma vez pelo Inspector
    private void LogStateChange(string stateName)
    {
        if (enableStateDebugLogs)
        {
            Debug.Log($"[ChaserAI] {name} -> {stateName}", this);
        }
    }

    // ------------------------------------------------------------------
    // Debug (Editor)
    // ------------------------------------------------------------------

    [ContextMenu("Debug/Forçar Unfreeze")]
    private void DebugForceUnfreeze()
    {
        Time.timeScale = 1f;
    }
}