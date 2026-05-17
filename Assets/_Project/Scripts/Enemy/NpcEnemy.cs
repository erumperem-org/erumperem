// ============================================================
// NpcEnemy.cs
// Namespace : Systems.NPC.Enemy
// ============================================================
// NPC Inimigo principal.
//
// Arquitetura:
//   • Cada estado possui UMA coroutine exclusiva.
//   • Ao entrar em novo estado → coroutine anterior é encerrada.
//   • Ao retornar para pool  → TODAS as coroutines são encerradas.
//   • Sem Update, FixedUpdate ou loops permanentes.
//
// Fluxo de estados:
//   Activate → Wander ──[detecta Player]──→ Chase
//                                            ↓ (ultrapassa ChaseRadius)
//                                        ReturnToPool
//
// Dependências (injetadas pelo Builder via Initialize):
//   • NpcEnemyConfig      – parâmetros do ciclo atual
//   • NpcMovementController – estratégias de movimento (WanderBehavior / PursuingBehavior)
//   • Detector              – sistema de detecção do pack
// ============================================================

using System.Collections;
using Core.Exploration.Character.Movement;
using DetectionSystem.Core;
using Services.Navigation;
using Systems.NPC.Enemy.Contracts;
using UnityEngine;

namespace Systems.NPC.Enemy
{
    /// <summary>
    /// NPC Inimigo baseado em Coroutines controláveis.
    ///
    /// Não possui Update/FixedUpdate. Todo processamento acontece
    /// dentro de coroutines que são iniciadas e encerradas conforme
    /// o estado ativo. Quando retorna à pool, zero processamento residual.
    /// </summary>
    [RequireComponent(typeof(NpcMovementController))]
    [RequireComponent(typeof(NavMeshAgentAdapter))]
    [RequireComponent(typeof(Detector))]
    public sealed class NpcEnemy : MonoBehaviour, INpcEnemy
    {
        // ── Estado ────────────────────────────────────────────────────────

        public NpcEnemyState CurrentState { get; private set; } = NpcEnemyState.Idle;

        // ── Componentes internos (resolvidos no Awake) ────────────────────

        private NpcMovementController _movementController;
        private NavMeshAgentAdapter   _adapter;
        private Detector              _detector;

        // ── Configuração do ciclo atual (injetada pelo Builder) ───────────

        private NpcEnemyConfig _config;

        // ── Coroutines ativas (apenas UMA por vez, mais o monitor de chase) ─

        private Coroutine _stateBehaviorCoroutine;  // Wander ou Chase
        private Coroutine _chaseMonitorCoroutine;   // Monitora limite de chase em paralelo
        private Coroutine _detectionPollingCoroutine; // Polling do detector

        // ── Behavior ativo — cancelado via CancelImmediate() antes de cada troca ──
        // Guardamos a instância do behavior (não um CTS) porque ExecuteBehavior()
        // não recebe CancellationToken — cada behavior gerencia seu próprio _cts
        // internamente. O cancelamento externo é feito via IReversibleCharacterMovementStrategy.CancelImmediate().

        private IReversibleCharacterMovementStrategy _activeBehavior;

        // ── Constantes de polling ─────────────────────────────────────────

        private const float DetectionPollInterval  = 0.15f; // segundos entre scans
        private const float ChaseMonitorInterval   = 0.2f;  // segundos entre cheques de distância
        private const float ContactCheckInterval   = 0.1f;  // segundos entre cheques de contato

        // ── Unity Awake ───────────────────────────────────────────────────

        private void Awake()
        {
            _movementController = GetComponent<NpcMovementController>();
            _adapter            = GetComponent<NavMeshAgentAdapter>();
            _detector           = GetComponent<Detector>();
        }

        // ═════════════════════════════════════════════════════════════════
        // INpcEnemy - API pública
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Inicializa o NPC com a configuração do ciclo atual.
        /// Chamado pelo Builder imediatamente após retirar da pool.
        /// NÃO inicia comportamento ainda — Activate() faz isso.
        /// </summary>
        public void Initialize(NpcEnemyConfig config)
        {
            _config = config;

            // Registra listener no detector para reagir quando Player entrar/sair
            _detector.OnDetectorEnter += OnDetectorEnter;
            _detector.OnDetectorExit  += OnDetectorExit;
        }

        /// <summary>
        /// Posiciona o NPC no spawn point e inicia comportamento de Wander.
        /// Chamado pelo Builder após Initialize().
        /// </summary>
        public void Activate()
        {
            // Garante posição correta antes de qualquer movimento
            transform.position = _config.SpawnPoint;

            // Habilita o NavMeshAgent antes de qualquer operação —
            // a pool o desabilita para mover o NPC fora do NavMesh enquanto inativo.
            // ResetAgent() começa com IsReady() que exige agent.enabled && isOnNavMesh,
            // portanto sem EnableAgent() ele retorna silenciosamente sem fazer nada.
            _movementController.NavMesh.EnableAgent(_adapter);

            // Warp para o spawn point — garante que o agent está registrado
            // na superfície correta antes de receber comandos de movimento.
            _movementController.NavMesh.Warp(_adapter, _config.SpawnPoint);

            // Reseta estado do agente (path, velocidade, parâmetros default)
            _movementController.NavMesh.ResetAgent(_adapter);

            // Inicia polling do detector via Coroutine (não usa TickingDetector)
            StartDetectionPolling();

            // Entra no estado inicial: Wander
            EnterWander();
        }

        /// <summary>
        /// Encerra todas as Coroutines, limpa estado e notifica a pool.
        /// Seguro para chamada múltipla (idempotente).
        /// </summary>
        public void ReturnToPool()
        {
            if (CurrentState == NpcEnemyState.ReturningToPool) return;

            CurrentState = NpcEnemyState.ReturningToPool;

            // ── 1. Encerra TODAS as Coroutines ────────────────────────────
            StopAllBehaviorCoroutines();

            // ── 2. Desregistra eventos do detector ────────────────────────
            _detector.OnDetectorEnter -= OnDetectorEnter;
            _detector.OnDetectorExit  -= OnDetectorExit;

            // ── 3. Limpa o alvo de perseguição ────────────────────────────
            if (_config != null) _config.PursuitTarget = null;

            // ── 4. Reseta o NavMeshAgent ──────────────────────────────────
            _movementController.NavMesh.Stop(_adapter);
            _movementController.NavMesh.ClearPath(_adapter);
            _movementController.NavMesh.SetVelocity(_adapter, Vector3.zero);
            _movementController.NavMesh.ResetAgent(_adapter);

            // ── 5. Notifica a pool para desativar e reposicionar ──────────
            _config?.OnReturnToPool?.Invoke(this);
        }

        // ═════════════════════════════════════════════════════════════════
        // Transições de estado
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Entra no estado Wander.
        /// Encerra qualquer coroutine anterior e inicia a de wander.
        /// </summary>
        private void EnterWander()
        {
            CancelActiveBehavior();         // para o loop interno do behavior via CancelImmediate()
            StopStateBehaviorCoroutine();
            StopChaseMonitor();

            CurrentState = NpcEnemyState.Wander;
            _stateBehaviorCoroutine = StartCoroutine(WanderCoroutine());
        }

        /// <summary>
        /// Entra no estado Chase em direção ao target detectado.
        /// Encerra o wander e inicia a perseguição + monitor de distância.
        /// </summary>
        private void EnterChase(Transform target)
        {
            if (CurrentState == NpcEnemyState.ReturningToPool) return;
            if (target == null) return;

            CancelActiveBehavior();         // para o loop interno do behavior via CancelImmediate()
            StopStateBehaviorCoroutine();

            CurrentState          = NpcEnemyState.Chase;
            _config.PursuitTarget = target;

            _stateBehaviorCoroutine = StartCoroutine(ChaseCoroutine());
            _chaseMonitorCoroutine  = StartCoroutine(ChaseRadiusMonitorCoroutine());
        }

        // ═════════════════════════════════════════════════════════════════
        // Coroutines de comportamento
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Coroutine de Wander.
        /// Caminha aleatoriamente dentro do WanderRadius via NavMesh.
        /// Usa WanderBehavior do pack através do NpcMovementController.
        /// Encerrada automaticamente quando EnterChase() é chamado.
        /// </summary>
        private IEnumerator WanderCoroutine()
        {
            var context = new WanderBehaviorContext(
                controller      : _movementController,
                navMesh         : _movementController.NavMesh,
                adapter         : _adapter,
                self            : transform,
                target          : null,
                characterName   : gameObject.name,
                perceptionRadius: 0f,
                wanderRadius    : _config.WanderRadius,
                centerFixed     : true,
                center          : _config.SpawnPoint,
                onPointReached  : null
            );

            // Instancia o behavior, guarda a referência para CancelImmediate()
            // e dispara ExecuteBehavior (fire-and-forget intencional — o behavior
            // loop é independente e para via seu próprio _cts quando cancelado).
            var behavior = new WanderBehavior();
            _activeBehavior = behavior;
            _ = behavior.ExecuteBehavior(context);

            while (CurrentState == NpcEnemyState.Wander)
                yield return new WaitForSeconds(0.5f);
        }

        /// <summary>
        /// Coroutine de Chase.
        /// Persegue o Player continuamente via NavMesh.
        /// Detecta contato (distância mínima) e dispara eventos de combate.
        /// Usa PursuingBehavior do pack através do NpcMovementController.
        /// </summary>
        private IEnumerator ChaseCoroutine()
        {
            if (_config.PursuitTarget == null)
            {
                ReturnToPool();
                yield break;
            }

            var context = new PursuingBehaviorContext(
                controller      : _movementController,
                navMesh         : _movementController.NavMesh,
                adapter         : _adapter,
                self            : transform,
                target          : _config.PursuitTarget,
                characterName   : gameObject.name,
                perceptionRadius: _config.ChaseRadius
            );

            var behavior = new PursuingBehavior();
            _activeBehavior = behavior;
            _ = behavior.ExecuteBehavior(context);

            var contactWait = new WaitForSeconds(ContactCheckInterval);

            while (CurrentState == NpcEnemyState.Chase && _config.PursuitTarget != null)
            {
                float distToPlayer = Vector3.Distance(
                    transform.position, _config.PursuitTarget.position);

                if (distToPlayer <= _config.ContactDistance)
                    OnContactWithPlayer();

                yield return contactWait;
            }
        }

        /// <summary>
        /// Coroutine de monitoramento do raio máximo de chase.
        /// Roda em paralelo com ChaseCoroutine.
        /// Se o NPC ultrapassar ChaseRadius a partir do SpawnPoint → retorna à pool.
        /// </summary>
        private IEnumerator ChaseRadiusMonitorCoroutine()
        {
            var wait = new WaitForSeconds(ChaseMonitorInterval);

            while (CurrentState == NpcEnemyState.Chase)
            {
                float distFromSpawn = Vector3.Distance(transform.position, _config.SpawnPoint);

                if (distFromSpawn > _config.ChaseRadius)
                {
                    // NPC ultrapassou o limite — retorna para pool
                    ReturnToPool();
                    yield break;
                }

                yield return wait;
            }
        }

        /// <summary>
        /// Coroutine de polling do Detector.
        /// Chama Scan() em intervalo fixo em vez de usar TickingDetector (que usa Update).
        /// Encerrada junto com todas as outras ao ReturnToPool.
        /// </summary>
        private IEnumerator DetectionPollingCoroutine()
        {
            var wait = new WaitForSeconds(DetectionPollInterval);

            while (true)
            {
                _detector.Scan();
                yield return wait;
            }
        }

        // ═════════════════════════════════════════════════════════════════
        // Eventos do Detector
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Chamado pelo Detector quando um Collider entra em qualquer shape.
        /// Filtra pela tag "Player" e inicia a perseguição.
        /// </summary>
        private void OnDetectorEnter(Collider detected, string shapeLabel, int shapeIndex)
        {
            if (CurrentState == NpcEnemyState.ReturningToPool) return;
            if (!detected.CompareTag("Player")) return;

            // Player detectado → inicia Chase
            EnterChase(detected.transform);
        }

        /// <summary>
        /// Chamado pelo Detector quando um Collider sai de qualquer shape.
        /// Se o Player saiu da área de detecção e o NPC ainda está perseguindo,
        /// volta para Wander (o Player pode ainda estar dentro do ChaseRadius).
        /// </summary>
        private void OnDetectorExit(Collider detected, string shapeLabel, int shapeIndex)
        {
            if (CurrentState != NpcEnemyState.Chase) return;
            if (!detected.CompareTag("Player")) return;

            // Player saiu do cone/esfera de detecção mas NPC ainda está no limite.
            // Volta para wander — o ChaseRadiusMonitor já trata o retorno à pool.
            EnterWander();
        }

        // ═════════════════════════════════════════════════════════════════
        // Contato com Player
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Chamado quando o NPC toca o Player (distância <= ContactDistance).
        /// </summary>
        private void OnContactWithPlayer()
        {
            // TODO: Damage Player
            // TODO: Trigger Combat
            // TODO: Notify Combat System

            // Exemplo: DamageSystem.Instance.ApplyDamage(_config.PursuitTarget, damage);
            // Exemplo: CombatEvents.OnNpcContact?.Invoke(this, _config.PursuitTarget);
        }

        // ═════════════════════════════════════════════════════════════════
        // Helpers — controle de coroutines
        // ═════════════════════════════════════════════════════════════════

        private void StartDetectionPolling()
        {
            StopDetectionPolling();
            _detectionPollingCoroutine = StartCoroutine(DetectionPollingCoroutine());
        }

        private void StopDetectionPolling()
        {
            if (_detectionPollingCoroutine == null) return;
            StopCoroutine(_detectionPollingCoroutine);
            _detectionPollingCoroutine = null;
        }

        private void StopStateBehaviorCoroutine()
        {
            if (_stateBehaviorCoroutine == null) return;
            StopCoroutine(_stateBehaviorCoroutine);
            _stateBehaviorCoroutine = null;
        }

        private void StopChaseMonitor()
        {
            if (_chaseMonitorCoroutine == null) return;
            StopCoroutine(_chaseMonitorCoroutine);
            _chaseMonitorCoroutine = null;
        }

        /// <summary>
        /// Cancela o behavior ativo via CancelImmediate() — para o loop interno
        /// de NavMesh do behavior antes de iniciar o próximo estado.
        /// </summary>
        private void CancelActiveBehavior()
        {
            _activeBehavior?.CancelImmediate();
            _activeBehavior = null;
        }

        /// <summary>
        /// Encerra absolutamente todas as coroutines.
        /// Chamado apenas em ReturnToPool e OnDestroy.
        /// </summary>
        private void StopAllBehaviorCoroutines()
        {
            CancelActiveBehavior();     // cancela o behavior ativo primeiro
            StopStateBehaviorCoroutine();
            StopChaseMonitor();
            StopDetectionPolling();

            // Para o agente NavMesh — garante que nenhum movimento residual persiste
            // mesmo que o behavior já tenha sido cancelado pelo token.
            if (_adapter != null && _movementController != null)
                _movementController.NavMesh.Stop(_adapter);
        }

        // ═════════════════════════════════════════════════════════════════
        // Unity OnDestroy — limpeza de segurança
        // ═════════════════════════════════════════════════════════════════

        private void OnDestroy()
        {
            // Garante limpeza mesmo se ReturnToPool não foi chamado
            _detector.OnDetectorEnter -= OnDetectorEnter;
            _detector.OnDetectorExit  -= OnDetectorExit;
            StopAllBehaviorCoroutines();
        }
    }
}