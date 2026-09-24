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
    [Tooltip("A SafeArea/Hub que este Chaser nunca deve atravessar fisicamente. Normalmente a mesma referência usada como excludedArea no WanderArea. Também usada, na checagem de nascimento, para não teleportar o Chaser para dentro dela.")]
    [SerializeField] private CircularZone excludedSafeArea;
    [Tooltip("Limites do mapa, usados apenas na checagem de nascimento (não deixar o Chaser fora do mapa ao se afastar da visão do player). Opcional - se não for atribuído, essa checagem é ignorada.")]
    [SerializeField] private MapLimits mapLimits;

    [Header("Debug")]
    [Tooltip("Loga no Console cada transição de estado (Wandering/Chasing/Investigating). Sem custo em build, é só um Debug.Log.")]
    [SerializeField] private bool enableStateDebugLogs = true;

    private PhysicsMovementService movement;

    private Vector3 currentWanderTarget;
    private Vector3 lastKnownTargetPosition;
    private float investigateTimer;

    public ChaserState CurrentState { get; private set; }

    public event Action OnTargetCaught;

    private bool _hasCaughtTarget;

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
        // Todo Chaser fica ativo 100% do tempo (sem Resting) - por isso,
        // antes de começar a vagar/perseguir, garantimos que ele não nasça
        // já dentro da visão do player (ex: posicionado manualmente na cena
        // bem perto dele).
        RelocateIfInsidePlayerViewAtStart();
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

    private void PickNewWanderTarget()
    {
        currentWanderTarget = wanderArea.GetRandomPoint();
    }

    private void HandleTargetCaught()
    {
        if (_hasCaughtTarget)
        {
            return;
        }

        _hasCaughtTarget = true;
        movement.SetMoveDirection(Vector3.zero);
        CombatOverworldFlowDiagnostics.LogPhase("ChaserAI", $"captura — {name}", this);
        OnTargetCaught?.Invoke();
    }

    /// <summary>
    /// Permite nova captura se a entrada em combate falhou (ex.: load bloqueado).
    /// </summary>
    public void ResetCatchStateForCombatRetry()
    {
        _hasCaughtTarget = false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (CurrentState != ChaserState.Chasing)
        {
            return;
        }

        if (!IsPlayerCollision(collision.collider))
        {
            return;
        }

        HandleTargetCaught();
    }

    private static bool IsPlayerCollision(Collider collider)
    {
        if (collider == null)
        {
            return false;
        }

        if (collider.CompareTag("Player"))
        {
            return true;
        }

        return collider.GetComponentInParent<PlayableCharacter>() != null;
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

    /// <summary>
    /// Permite a um sistema externo (ex: ChaserPool) atualizar dinamicamente
    /// quem este Chaser persegue - necessário porque o personagem "Em Jogo"
    /// pode trocar em runtime (ver PlayableCharacterController). Também
    /// repassa a troca imediatamente para o PerceptionSensor, já que o
    /// Chaser nunca fica com a percepção desligada para "pegar" o novo alvo
    /// só na próxima transição de estado.
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        perceptionSensor.Activate(target, settings);
    }

    /// <summary>
    /// Teleporta o Chaser para uma nova posição e o coloca de volta em
    /// Wandering (nunca direto em Chasing, mesmo que a nova posição esteja
    /// tecnicamente perto do alvo) - usado pelo ChaserPool para trazer de
    /// volta, para perto do player, um Chaser que se afastou demais.
    /// Diferente do antigo fluxo de Resting, o Chaser nunca fica com
    /// movimento/percepção desligados durante a troca - é um teleporte
    /// direto seguido de uma reentrada normal em Wandering.
    /// </summary>
    public void Relocate(Vector3 newPosition)
    {
        transform.position = newPosition;
        EnterWandering();
    }

    /// <summary>
    /// Checagem de nascimento: se a posição atual do Chaser está dentro da
    /// aproximação simplificada de campo de visão do player
    /// (<see cref="PlayerFieldOfViewApproximation"/>), sorteia um ponto fora
    /// dela (respeitando MapLimits e a área segura excluída, se atribuídos)
    /// num raio ao redor de si mesmo. Se nenhuma tentativa aleatória der
    /// certo, cai no fallback de empurrar em linha reta para fora da esfera
    /// de visão (sem garantia de respeitar MapLimits/área excluída nesse
    /// caso extremo).
    /// </summary>
    private void RelocateIfInsidePlayerViewAtStart()
    {
        if (target == null)
        {
            return;
        }

        if (!PlayerFieldOfViewApproximation.IsInside(transform.position, target, settings.initialViewForwardOffset, settings.initialViewRadius))
        {
            return;
        }

        for (int i = 0; i < settings.initialPlacementMaxSampleAttempts; i++)
        {
            Vector3 candidate = RandomPointAroundSelf(settings.initialPlacementSearchRadius);

            if (IsValidInitialPlacement(candidate))
            {
                transform.position = candidate;
                return;
            }
        }

        transform.position = PlayerFieldOfViewApproximation.PushOutside(
            transform.position, target, settings.initialViewForwardOffset, settings.initialViewRadius, margin: 1f);
    }

    private Vector3 RandomPointAroundSelf(float radius)
    {
        float angle = UnityEngine.Random.value * Mathf.PI * 2f;
        float distance = UnityEngine.Random.Range(0f, radius);

        Vector3 offset = new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
        Vector3 point = transform.position + offset;
        point.y = transform.position.y;
        return point;
    }

    private bool IsValidInitialPlacement(Vector3 point)
    {
        if (mapLimits != null && !mapLimits.Contains(point))
        {
            return false;
        }

        if (excludedSafeArea != null && IsInsideExcludedSafeArea(point))
        {
            return false;
        }

        return !PlayerFieldOfViewApproximation.IsInside(point, target, settings.initialViewForwardOffset, settings.initialViewRadius);
    }

    private bool IsInsideExcludedSafeArea(Vector3 point)
    {
        Vector3 delta = point - excludedSafeArea.Center;
        delta.y = 0f;
        return delta.sqrMagnitude <= excludedSafeArea.Radius * excludedSafeArea.Radius;
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
