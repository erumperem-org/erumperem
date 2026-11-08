using System;
using UnityEngine;

/// <summary>
/// Cérebro de um perseguidor: só a máquina de estados
/// (Wandering / Chasing / Investigating) e os comandos de movimento.
///
/// Tudo o que antes vivia aqui foi para componentes próprios:
/// <list type="bullet">
/// <item>montagem dos validadores de movimento → <see cref="ChaserMovementValidators"/></item>
/// <item>detecção de contato físico (qualquer collider) → <see cref="ChaserContactSensor"/>; a captura é decidida aqui</item>
/// <item>onde vagar → <see cref="IRoamStrategy"/></item>
/// <item>onde nascer / realocar → <see cref="ChaserPlacementRules"/> (usado por Spawner/Relocator)</item>
/// <item>qual é o alvo atual → <see cref="ChaserWorld"/></item>
/// <item>caçada periódica → <see cref="PeriodicHuntBehaviour"/></item>
/// <item>animação / áudio de proximidade → <see cref="ChaserAnimationDriver"/> / <see cref="ChaserProximityAudio"/></item>
/// </list>
///
/// Ciclo de vida de um chaser gerado pelo <see cref="ChaserSpawner"/>:
/// Instantiate → <see cref="Initialize"/> → (desativado) → ativado + <see cref="Begin"/>.
/// Um chaser posto à mão na cena pode apenas ter o campo <c>world</c> atribuído:
/// ele se inicializa sozinho no Start.
///
/// Contratos preservados (usados pelo EnemyHuntOrchestrator): <see cref="OnTargetCaught"/>
/// e <see cref="ResetCatchStateForCombatRetry"/>.
/// </summary>
[RequireComponent(typeof(PhysicsMovementService))]
[RequireComponent(typeof(ChaserContactSensor))]
public class ChaserAI : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private ChaserSettings settings;
    [SerializeField] private PerceptionSensor perceptionSensor;

    [Tooltip("Opcional. Chasers criados pelo ChaserSpawner recebem o mundo via Initialize(). " +
             "Atribua aqui só para um chaser posto manualmente na cena (ele se inicializa no Start).")]
    [SerializeField] private ChaserWorld world;

    [Header("Debug")]
    [Tooltip("Loga cada transição de estado no Console. Deixe desligado com muitos inimigos na cena.")]
    [SerializeField] private bool enableStateDebugLogs = false;

    private PhysicsMovementService movement;
    private Rigidbody body;
    private ChaserContactSensor contactSensor;
    private IRoamStrategy roam;

    private Vector3 currentWanderTarget;
    private Vector3 lastKnownTargetPosition;
    private float investigateTimer;
    private bool hasPlayedSpotSound;
    private bool hasCaughtTarget;
    private bool initialized;

    // Detecção de travamento (ver IsStuck).
    private Vector3 stuckSamplePosition;
    private float stuckSampleTimer;
    private float stuckSeconds;

    public Transform Target { get; private set; }
    public ChaserWorld World => world;
    public ChaserState CurrentState { get; private set; }

    /// <summary>
    /// True se o <see cref="ChaserRelocator"/> pode teleportá-lo: só quem está vagando.
    /// Quem persegue ou investiga (inclusive a caçada periódica) não é interrompido.
    /// </summary>
    public bool CanBeRelocated => HasBegun && CurrentState == ChaserState.Wandering;

    /// <summary>True depois de <see cref="Begin"/>: o chaser está vivo no mundo e rodando a IA.</summary>
    public bool HasBegun { get; private set; }

    /// <summary>Disparado na captura do alvo. Contrato com o EnemyHuntOrchestrator.</summary>
    public event Action OnTargetCaught;

    /// <summary>Disparado quando <see cref="ResetCatchStateForCombatRetry"/> libera uma nova captura.</summary>
    public event Action OnCatchStateReset;

    /// <summary>Disparado depois de <see cref="Relocate"/> teleportar o chaser.</summary>
    public event Action OnRelocated;

    // ------------------------------------------------------------------
    // Ciclo de vida
    // ------------------------------------------------------------------

    private void Awake()
    {
        CacheComponents();
    }

    private void OnEnable()
    {
        hasPlayedSpotSound = false;
        contactSensor.Contact += HandleContact;
        SubscribeToWorld();

        // Reativado depois de já ter começado: o sensor foi parado no OnDisable.
        if (HasBegun)
        {
            perceptionSensor.Activate(Target, settings);
        }
    }

    private void OnDisable()
    {
        contactSensor.Contact -= HandleContact;
        UnsubscribeFromWorld();
    }

    private void Start()
    {
        if (HasBegun)
        {
            return;
        }

        // Chaser que não veio do ChaserSpawner (ex.: instância solta na cena): se não tem mundo,
        // adota o ChaserWorld da cena e se registra no ChaserPool para também ser realocado.
        if (!initialized)
        {
            if (world == null)
            {
                world = FindAnyObjectByType<ChaserWorld>();
            }

            if (world != null)
            {
                Initialize(world);
            }
        }

        if (!initialized)
        {
            Debug.LogWarning($"[ChaserAI] {name} não foi inicializado: sem ChaserWorld na cena (ou configuração " +
                             "inválida, veja os erros acima). Esta IA ficará parada.", this);
            return;
        }

        ChaserPool pool = FindAnyObjectByType<ChaserPool>();
        if (pool != null)
        {
            pool.Register(this);
        }

        Begin(transform.position);
    }

    private void CacheComponents()
    {
        if (movement != null)
        {
            return;
        }

        movement = GetComponent<PhysicsMovementService>();
        body = GetComponent<Rigidbody>();

        contactSensor = GetComponent<ChaserContactSensor>();
        if (contactSensor == null)
        {
            // Prefabs antigos não têm o componente; RequireComponent só age no editor.
            contactSensor = gameObject.AddComponent<ChaserContactSensor>();
        }

        if (perceptionSensor == null)
        {
            perceptionSensor = GetComponent<PerceptionSensor>();
        }
    }

    /// <summary>
    /// Injeta as dependências de cena e monta os validadores de movimento.
    /// Pode ser chamado com o objeto ainda desativado.
    /// </summary>
    public void Initialize(ChaserWorld newWorld)
    {
        if (newWorld == null)
        {
            Debug.LogError($"[ChaserAI] {name}: Initialize chamado sem ChaserWorld.", this);
            return;
        }

        CacheComponents();

        if (settings == null || perceptionSensor == null)
        {
            Debug.LogError($"[ChaserAI] {name}: falta ChaserSettings ou PerceptionSensor no prefab.", this);
            return;
        }

        if (newWorld.WanderArea == null)
        {
            Debug.LogError($"[ChaserAI] {name}: o ChaserWorld não tem a WanderArea atribuída. " +
                           "Sem ela o chaser não tem para onde ir.", this);
            return;
        }

        UnsubscribeFromWorld();
        world = newWorld;
        SubscribeToWorld();

        movement.SetValidator(ChaserMovementValidators.Create(settings, world.SafeArea, world.SafeAreaMargin));

        if (roam == null)
        {
            roam = new WanderAreaRoam(world.WanderArea);
        }

        initialized = true;
    }

    /// <summary>
    /// Coloca o chaser no mundo (posição inicial válida escolhida por quem chama)
    /// e inicia a IA em Wandering. O objeto precisa estar ativo.
    /// </summary>
    public void Begin(Vector3 position)
    {
        if (!initialized)
        {
            Debug.LogError($"[ChaserAI] {name}: Begin chamado antes de Initialize.", this);
            return;
        }

        TeleportTo(position);
        hasCaughtTarget = false;
        HasBegun = true;

        SetTarget(world.Target);
        EnterWandering();
    }

    /// <summary>Troca a estratégia de "para onde ir quando não há alvo" (padrão: WanderArea).</summary>
    public void SetRoamStrategy(IRoamStrategy strategy)
    {
        roam = strategy ?? (world != null ? new WanderAreaRoam(world.WanderArea) : null);

        if (HasBegun && CurrentState == ChaserState.Wandering)
        {
            PickNewWanderTarget();
        }
    }

    private void SubscribeToWorld()
    {
        if (world != null && isActiveAndEnabled)
        {
            world.TargetChanged += SetTarget;
        }
    }

    private void UnsubscribeFromWorld()
    {
        if (world != null)
        {
            world.TargetChanged -= SetTarget;
        }
    }

    private void FixedUpdate()
    {
        if (!HasBegun)
        {
            return;
        }

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
        LogStateChange("Wandering");
        ResetStuckDetection();
        movement.SetSprinting(false);
        PickNewWanderTarget();
    }

    private void EnterChasing()
    {
        if (!hasPlayedSpotSound)
        {
            hasPlayedSpotSound = true;
            AudioManager.instance?.PlaySFXAtPosition("EnemySpot", transform.position);
        }

        CurrentState = ChaserState.Chasing;
        LogStateChange("Chasing");
        movement.SetSprinting(settings.sprintWhileChasing);
    }

    private void EnterInvestigating()
    {
        CurrentState = ChaserState.Investigating;
        LogStateChange("Investigating");
        investigateTimer = 0f;
        ResetStuckDetection();
        movement.SetSprinting(false);
    }

    // ------------------------------------------------------------------
    // Lógica de cada estado
    // ------------------------------------------------------------------

    private void TickWandering()
    {
        movement.SetMoveDirection(DirectionTo(currentWanderTarget));

        if (HasArrivedAt(currentWanderTarget) || IsStuck())
        {
            PickNewWanderTarget();
            ResetStuckDetection();
        }

        if (perceptionSensor.CanSeeTarget)
        {
            EnterChasing();
        }
    }

    private void TickChasing()
    {
        if (Target == null)
        {
            EnterWandering();
            return;
        }

        movement.SetMoveDirection(DirectionTo(Target.position));

        float distance = Vector3.Distance(transform.position, Target.position);
        if (distance <= settings.catchDistance)
        {
            HandleTargetCaught();
            return;
        }

        if (!perceptionSensor.CanSeeTarget)
        {
            lastKnownTargetPosition = KeepOutsideSafeArea(Target.position);
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

            // Destino inalcançável (obstáculo, quina): desiste e passa a esperar onde está,
            // em vez de empurrar para sempre sem nunca disparar o timer de espera.
            if (IsStuck())
            {
                lastKnownTargetPosition = transform.position;
            }

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
        currentWanderTarget = roam.GetNextDestination(transform.position);
    }

    // ------------------------------------------------------------------
    // Captura
    // ------------------------------------------------------------------

    /// <summary>
    /// Contato físico captura em qualquer estado, mas só se o collider for do alvo atual
    /// (o personagem Em Jogo). Companheiro e Resting também são PlayableCharacters, porém
    /// nunca são o alvo do ChaserWorld.
    /// </summary>
    private void HandleContact(Collider other)
    {
        if (!HasBegun || Target == null || other == null)
        {
            return;
        }

        Transform otherTransform = other.transform;
        if (otherTransform == Target || otherTransform.IsChildOf(Target))
        {
            HandleTargetCaught();
        }
    }

    private void HandleTargetCaught()
    {
        if (hasCaughtTarget)
        {
            return;
        }

        hasCaughtTarget = true;
        movement.SetMoveDirection(Vector3.zero);
        CombatOverworldFlowDiagnostics.LogPhase("ChaserAI", $"captura — {name}", this);
        OnTargetCaught?.Invoke();
    }

    /// <summary>Permite nova captura se a entrada em combate falhou (ex.: load bloqueado).</summary>
    public void ResetCatchStateForCombatRetry()
    {
        hasCaughtTarget = false;
        OnCatchStateReset?.Invoke();
    }

    // ------------------------------------------------------------------
    // API para sistemas externos
    // ------------------------------------------------------------------

    /// <summary>
    /// Troca o alvo perseguido (chamado pelo ChaserWorld quando o personagem Em Jogo muda).
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        Target = newTarget;

        if (isActiveAndEnabled)
        {
            perceptionSensor.Activate(Target, settings);
        }
    }

    /// <summary>
    /// Teleporta o chaser e o devolve a Wandering (nunca direto a Chasing).
    /// </summary>
    public void Relocate(Vector3 newPosition)
    {
        TeleportTo(newPosition);
        EnterWandering();
        perceptionSensor.ClearResult();
        OnRelocated?.Invoke();
    }

    /// <summary>
    /// Força o chaser a ir até <paramref name="position"/> como se fosse a última posição
    /// vista do alvo (estado Investigating). Ignorado se já estiver perseguindo.
    /// Se enxergar o alvo no caminho, vira Chasing normalmente.
    /// </summary>
    public bool ForceInvestigate(Vector3 position)
    {
        if (!HasBegun || hasCaughtTarget || CurrentState == ChaserState.Chasing)
        {
            return false;
        }

        lastKnownTargetPosition = KeepOutsideSafeArea(position);
        EnterInvestigating();
        return true;
    }

    // ------------------------------------------------------------------
    // Utilitários
    // ------------------------------------------------------------------

    /// <summary>
    /// Move o chaser instantaneamente de forma coerente com a física: posiciona o Rigidbody
    /// (evita glitch de interpolação), zera a velocidade herdada e para o comando de movimento.
    /// </summary>
    private void TeleportTo(Vector3 position)
    {
        transform.position = position;

        if (body != null)
        {
            body.position = position;
            body.linearVelocity = Vector3.zero;
        }

        movement.SetMoveDirection(Vector3.zero);
        ResetStuckDetection();
    }

    /// <summary>
    /// Se o destino cai dentro da área segura (+ margem), leva-o para a borda externa.
    /// O validador impede de entrar lá, então um destino dentro dela nunca seria alcançado.
    /// </summary>
    private Vector3 KeepOutsideSafeArea(Vector3 position)
    {
        CircularZone area = world != null ? world.SafeArea : null;
        if (area == null)
        {
            return position;
        }

        // Folga para que "chegou" (arrivalThreshold) aconteça fora do alcance do validador.
        float minRadius = area.Radius + world.SafeAreaMargin + settings.arrivalThreshold + 0.5f;

        Vector3 delta = position - area.Center;
        delta.y = 0f;

        if (delta.sqrMagnitude >= minRadius * minRadius)
        {
            return position;
        }

        Vector3 outward = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector3.right;
        Vector3 result = area.Center + outward * minRadius;
        result.y = position.y;
        return result;
    }

    private void ResetStuckDetection()
    {
        stuckSamplePosition = transform.position;
        stuckSampleTimer = 0f;
        stuckSeconds = 0f;
    }

    /// <summary>
    /// Chamar a cada FixedUpdate enquanto o chaser DEVERIA estar se movendo. Mede o deslocamento a cada
    /// <see cref="ChaserSettings.stuckCheckInterval"/>; devolve true quando ficou
    /// <see cref="ChaserSettings.stuckGiveUpTime"/> segundos sem progresso.
    /// </summary>
    private bool IsStuck()
    {
        stuckSampleTimer += Time.fixedDeltaTime;
        if (stuckSampleTimer < settings.stuckCheckInterval)
        {
            return false;
        }

        stuckSampleTimer = 0f;

        Vector3 delta = transform.position - stuckSamplePosition;
        delta.y = 0f;
        stuckSamplePosition = transform.position;

        if (delta.magnitude >= settings.stuckMinProgress)
        {
            stuckSeconds = 0f;
            return false;
        }

        stuckSeconds += settings.stuckCheckInterval;
        if (stuckSeconds < settings.stuckGiveUpTime)
        {
            return false;
        }

        stuckSeconds = 0f;
        return true;
    }

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

    private void LogStateChange(string stateName)
    {
        if (enableStateDebugLogs)
        {
            Debug.Log($"[ChaserAI] {name} -> {stateName}", this);
        }
    }

    // Se o jogo congelar o tempo (Time.timeScale = 0), FixedUpdate para e nenhum chaser se move.
    [ContextMenu("Debug/Forçar Unfreeze")]
    private void DebugForceUnfreeze()
    {
        Time.timeScale = 1f;
    }
}
