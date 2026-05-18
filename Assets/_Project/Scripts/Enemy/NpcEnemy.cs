// ============================================================
// NpcEnemy.cs
// Namespace : Systems.NPC.Enemy
// ============================================================
// Fluxo de estados:
//
//   Activate → Wander ──[detecta Player]──────────→ Chase
//                  ↓ (WanderLifetime esgotado)         ↓ (ultrapassa ChaseRadius)
//              ReturnToPool ←─────────────────────────────
//
// WanderLifetime: tempo máximo em Wander antes de retornar à pool.
// Configurado via NpcEnemyConfig.WanderLifetime.
// Após retornar, o Spawner aguarda RespawnDelay e realoca o NPC.
// ============================================================

using System.Collections;
using Core.Exploration.Character.Movement;
using DetectionSystem.Core;
using Services.Navigation;
using Systems.NPC.Enemy.Contracts;
using UnityEngine;

namespace Systems.NPC.Enemy
{
    [RequireComponent(typeof(NpcMovementController))]
    [RequireComponent(typeof(NavMeshAgentAdapter))]
    [RequireComponent(typeof(Detector))]
    public sealed class NpcEnemy : MonoBehaviour, INpcEnemy
    {
        // ── Estado ────────────────────────────────────────────────────────

        public NpcEnemyState CurrentState { get; private set; } = NpcEnemyState.Idle;

        // ── Componentes internos ──────────────────────────────────────────

        private NpcMovementController _movementController;
        private NavMeshAgentAdapter _adapter;
        private Detector _detector;

        // ── Configuração injetada pelo Builder ────────────────────────────

        private NpcEnemyConfig _config;

        // ── Coroutines ────────────────────────────────────────────────────

        private Coroutine _stateBehaviorCoroutine;
        private Coroutine _chaseMonitorCoroutine;
        private Coroutine _detectionPollingCoroutine;
        private Coroutine _wanderLifetimeCoroutine;   // ← timeout do wander

        private IReversibleCharacterMovementStrategy _activeBehavior;

        // ── Intervalos de polling ─────────────────────────────────────────

        private const float DetectionPollInterval = 0.15f;
        private const float ChaseMonitorInterval = 0.2f;
        private const float ContactCheckInterval = 0.1f;

        // ── Unity Awake ───────────────────────────────────────────────────

        private void Awake()
        {
            _movementController = GetComponent<NpcMovementController>();
            _adapter = GetComponent<NavMeshAgentAdapter>();
            _detector = GetComponent<Detector>();
        }

        // ═════════════════════════════════════════════════════════════════
        // INpcEnemy
        // ═════════════════════════════════════════════════════════════════

        public void Initialize(NpcEnemyConfig config)
        {
            _config = config;
            _detector.OnDetectorEnter += OnDetectorEnter;
            _detector.OnDetectorExit += OnDetectorExit;
        }

        public void Activate()
        {
            transform.position = _config.SpawnPoint;
            _movementController.NavMesh.EnableAgent(_adapter);
            _movementController.NavMesh.Warp(_adapter, _config.SpawnPoint);
            _movementController.NavMesh.ResetAgent(_adapter);
            StartDetectionPolling();
            EnterWander();
        }

        public void ReturnToPool()
        {
            if (CurrentState == NpcEnemyState.ReturningToPool) return;

            CurrentState = NpcEnemyState.ReturningToPool;

            StopAllBehaviorCoroutines();

            _detector.OnDetectorEnter -= OnDetectorEnter;
            _detector.OnDetectorExit -= OnDetectorExit;

            if (_config != null) _config.PursuitTarget = null;

            _movementController.NavMesh.Stop(_adapter);
            _movementController.NavMesh.ClearPath(_adapter);
            _movementController.NavMesh.SetVelocity(_adapter, Vector3.zero);
            _movementController.NavMesh.ResetAgent(_adapter);

            _config?.OnReturnToPool?.Invoke(this);
        }

        // ═════════════════════════════════════════════════════════════════
        // Transições de estado
        // ═════════════════════════════════════════════════════════════════

        private void EnterWander()
        {
            CancelActiveBehavior();
            StopStateBehaviorCoroutine();
            StopChaseMonitor();
            StopWanderLifetime();           // cancela timer anterior se houver

            CurrentState = NpcEnemyState.Wander;
            _stateBehaviorCoroutine = StartCoroutine(WanderCoroutine());
            _wanderLifetimeCoroutine = StartCoroutine(WanderLifetimeCoroutine());
        }

        private void EnterChase(Transform target)
        {
            if (CurrentState == NpcEnemyState.ReturningToPool) return;
            if (target == null) return;

            CancelActiveBehavior();
            StopStateBehaviorCoroutine();
            StopWanderLifetime();           // interrompe o timeout — NPC está em ação

            CurrentState = NpcEnemyState.Chase;
            _config.PursuitTarget = target;

            _stateBehaviorCoroutine = StartCoroutine(ChaseCoroutine());
            _chaseMonitorCoroutine = StartCoroutine(ChaseRadiusMonitorCoroutine());
        }

        // ═════════════════════════════════════════════════════════════════
        // Coroutines de comportamento
        // ═════════════════════════════════════════════════════════════════

        private IEnumerator WanderCoroutine()
        {
            var context = new WanderBehaviorContext(
                controller: _movementController,
                navMesh: _movementController.NavMesh,
                adapter: _adapter,
                self: transform,
                target: null,
                characterName: gameObject.name,
                perceptionRadius: 0f,
                wanderRadius: _config.WanderRadius,
                centerFixed: true,
                center: _config.SpawnPoint,
                onPointReached: null
            );

            var behavior = new WanderBehavior();
            _activeBehavior = behavior;
            _ = behavior.ExecuteBehavior(context);

            while (CurrentState == NpcEnemyState.Wander)
                yield return new WaitForSeconds(0.5f);
        }

        /// <summary>
        /// Timer de vida máxima no estado Wander.
        /// Ao expirar, o NPC retorna à pool para ser realocado pelo Spawner.
        /// </summary>
        private IEnumerator WanderLifetimeCoroutine()
        {
            yield return new WaitForSeconds(_config.WanderLifetime);

            // Só retorna se ainda estiver em Wander (pode ter entrado em Chase entre um frame e outro)
            if (CurrentState == NpcEnemyState.Wander)
            {
                Debug.Log($"[NpcEnemy] '{name}' expirou o WanderLifetime ({_config.WanderLifetime}s). Retornando à pool.");
                ReturnToPool();
            }
        }

        private IEnumerator ChaseCoroutine()
        {
            if (_config.PursuitTarget == null)
            {
                ReturnToPool();
                yield break;
            }

            var context = new PursuingBehaviorContext(
                controller: _movementController,
                navMesh: _movementController.NavMesh,
                adapter: _adapter,
                self: transform,
                target: _config.PursuitTarget,
                characterName: gameObject.name,
                perceptionRadius: _config.ChaseRadius
            );

            var behavior = new PursuingBehavior();
            _activeBehavior = behavior;
            _ = behavior.ExecuteBehavior(context);

            var contactWait = new WaitForSeconds(ContactCheckInterval);

        }

        private IEnumerator ChaseRadiusMonitorCoroutine()
        {
            var wait = new WaitForSeconds(ChaseMonitorInterval);

            while (CurrentState == NpcEnemyState.Chase)
            {
                float distFromSpawn = Vector3.Distance(transform.position, _config.SpawnPoint);
                if (distFromSpawn > _config.ChaseRadius)
                {
                    ReturnToPool();
                    yield break;
                }

                yield return wait;
            }
        }

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

        private void OnDetectorEnter(Collider detected, string shapeLabel, int shapeIndex)
        {
            if (CurrentState == NpcEnemyState.ReturningToPool) return;
            if (!detected.CompareTag("Player")) return;
            if (shapeLabel == "Perception" && CurrentState == NpcEnemyState.Wander)
            {
                EnterChase(detected.transform);
                return;
            }
            if (shapeLabel == "Contact")
            {
                OnContactWithPlayer();
            }
        }

        private void OnDetectorExit(Collider detected, string shapeLabel, int shapeIndex)
        {
            if (CurrentState != NpcEnemyState.Chase) return;
            if (!detected.CompareTag("Player")) return;
            if (shapeLabel == "Perception" && CurrentState == NpcEnemyState.Wander)
            {
                EnterWander();
            }
        }

        // ═════════════════════════════════════════════════════════════════
        // Contato com Player
        // ═════════════════════════════════════════════════════════════════

        private void OnContactWithPlayer()
        {
            ScenesManager.Instance.LoadSceneByName("CombatScene");
        }

        // ═════════════════════════════════════════════════════════════════
        // Helpers — coroutines
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

        private void StopWanderLifetime()
        {
            if (_wanderLifetimeCoroutine == null) return;
            StopCoroutine(_wanderLifetimeCoroutine);
            _wanderLifetimeCoroutine = null;
        }

        private void CancelActiveBehavior()
        {
            _activeBehavior?.CancelImmediate();
            _activeBehavior = null;
        }

        private void StopAllBehaviorCoroutines()
        {
            CancelActiveBehavior();
            StopStateBehaviorCoroutine();
            StopChaseMonitor();
            StopDetectionPolling();
            StopWanderLifetime();

            if (_adapter != null && _movementController != null)
                _movementController.NavMesh.Stop(_adapter);
        }

        // ═════════════════════════════════════════════════════════════════
        // OnDestroy
        // ═════════════════════════════════════════════════════════════════

        private void OnDestroy()
        {
            _detector.OnDetectorEnter -= OnDetectorEnter;
            _detector.OnDetectorExit -= OnDetectorExit;
            StopAllBehaviorCoroutines();
        }
    }
}