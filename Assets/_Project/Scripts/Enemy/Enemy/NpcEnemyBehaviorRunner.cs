// ============================================================
// NpcEnemyBehaviorRunner.cs
// Namespace : Systems.NPC.Enemy
// ============================================================
// Responsabilidade única: executar as coroutines de comportamento
// (Wander, Chase, ChaseMonitor, WanderLifetime) em resposta às
// transições de estado notificadas pela StateMachine.
//
// CORREÇÕES:
//   [4] ChaseRadiusMonitorCoroutine agora mede a distância entre
//       o inimigo e o PursuitTarget (player), não o SpawnPoint.
//       Isso garante que o chase só termine quando o player
//       realmente escapou do alcance, não quando o inimigo se
//       afastou do seu ponto de origem.
//   [8] StopAll() é chamado preventivamente em Initialize (via
//       NpcEnemy) para evitar vazamento de coroutines entre
//       ciclos de reutilização da pool.
// ============================================================

using System.Collections;
using Core.Exploration.Character.Movement;
using Services.Navigation;
using Systems.NPC.Enemy.Contracts;
using Systems.NPC.Enemy.StateMachine;
using UnityEngine;

namespace Systems.NPC.Enemy
{
    public sealed class NpcEnemyBehaviorRunner
    {
        // ── Dependências ──────────────────────────────────────────────────

        private readonly NpcMovementController  _movementController;
        private readonly NavMeshAgentAdapter    _adapter;
        private readonly NpcEnemyStateMachine   _stateMachine;
        private readonly MonoBehaviour          _owner;
        private readonly NpcEnemyConfig         _config;

        // ── Intervalos ────────────────────────────────────────────────────

        private const float ChaseMonitorInterval = 0.2f;

        // ── Coroutines ────────────────────────────────────────────────────

        private Coroutine _stateBehaviorCoroutine;
        private Coroutine _chaseMonitorCoroutine;
        private Coroutine _wanderLifetimeCoroutine;

        private IReversibleCharacterMovementStrategy _activeBehavior;

        // ── Callbacks ─────────────────────────────────────────────────────

        /// <summary>Chamado quando o WanderLifetime expira ou ChaseRadius é ultrapassado.</summary>
        public System.Action OnShouldReturnToPool;

        // ── Construtor ────────────────────────────────────────────────────

        public NpcEnemyBehaviorRunner(
            NpcMovementController movementController,
            NavMeshAgentAdapter adapter,
            NpcEnemyStateMachine stateMachine,
            MonoBehaviour owner,
            NpcEnemyConfig config)
        {
            _movementController = movementController;
            _adapter            = adapter;
            _stateMachine       = stateMachine;
            _owner              = owner;
            _config             = config;
        }

        // ── API pública ───────────────────────────────────────────────────

        public void RunWander()
        {
            StopAll();
            _stateBehaviorCoroutine  = _owner.StartCoroutine(WanderCoroutine());
            _wanderLifetimeCoroutine = _owner.StartCoroutine(WanderLifetimeCoroutine());
        }

        public void RunChase(Transform target)
        {
            StopStateBehavior();
            StopWanderLifetime();
            _config.PursuitTarget   = target;
            _stateBehaviorCoroutine = _owner.StartCoroutine(ChaseCoroutine());
            _chaseMonitorCoroutine  = _owner.StartCoroutine(ChaseRadiusMonitorCoroutine());
        }

        public void StopAll()
        {
            CancelActiveBehavior();
            StopStateBehavior();
            StopChaseMonitor();
            StopWanderLifetime();
        }

        // ── Coroutines de comportamento ───────────────────────────────────

        private IEnumerator WanderCoroutine()
        {
            var context = new WanderBehaviorContext(
                controller    : _movementController,
                navMesh       : _movementController.NavMesh,
                adapter       : _adapter,
                self          : _owner.transform,
                target        : null,
                characterName : _owner.gameObject.name,
                perceptionRadius: 0f,
                wanderRadius  : _config.WanderRadius,
                centerFixed   : true,
                center        : _config.SpawnPoint,
                onPointReached: null
            );

            var behavior = new WanderBehavior();
            _activeBehavior = behavior;
            _ = behavior.ExecuteBehavior(context);

            while (_stateMachine.Is(NpcEnemyState.Wander))
                yield return new WaitForSeconds(0.5f);
        }

        private IEnumerator WanderLifetimeCoroutine()
        {
            yield return new WaitForSeconds(_config.WanderLifetime);

            // [8] Verifica estado antes de notificar — evita disparo após retorno à pool.
            if (_stateMachine.Is(NpcEnemyState.Wander))
            {
                Debug.Log($"[NpcEnemyBehaviorRunner] '{_owner.name}' expirou WanderLifetime ({_config.WanderLifetime}s).");
                OnShouldReturnToPool?.Invoke();
            }
        }

        private IEnumerator ChaseCoroutine()
        {
            if (_config.PursuitTarget == null)
            {
                OnShouldReturnToPool?.Invoke();
                yield break;
            }

            var context = new PursuingBehaviorContext(
                controller    : _movementController,
                navMesh       : _movementController.NavMesh,
                adapter       : _adapter,
                self          : _owner.transform,
                target        : _config.PursuitTarget,
                characterName : _owner.gameObject.name,
                perceptionRadius: _config.ChaseRadius
            );

            var behavior = new PursuingBehavior();
            _activeBehavior = behavior;
            _ = behavior.ExecuteBehavior(context);
        }

        // [4] Mede distância até o PursuitTarget (player), não até o SpawnPoint.
        private IEnumerator ChaseRadiusMonitorCoroutine()
        {
            var wait = new WaitForSeconds(ChaseMonitorInterval);

            while (_stateMachine.Is(NpcEnemyState.Chase))
            {
                // [4] Se o alvo foi perdido (ex.: player morreu / retornou à pool),
                //     considera como saída do raio.
                if (_config.PursuitTarget == null)
                {
                    OnShouldReturnToPool?.Invoke();
                    yield break;
                }

                float dist = Vector3.Distance(
                    _owner.transform.position,
                    _config.PursuitTarget.position); // [4] Distância ao player, não ao spawn.

                if (dist > _config.ChaseRadius)
                {
                    Debug.Log($"[NpcEnemyBehaviorRunner] '{_owner.name}' saiu do raio de chase ({dist:F1} > {_config.ChaseRadius:F1}). Retornando à pool.");
                    OnShouldReturnToPool?.Invoke();
                    yield break;
                }

                yield return wait;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private void CancelActiveBehavior()
        {
            _activeBehavior?.CancelImmediate();
            _activeBehavior = null;
        }

        private void StopStateBehavior()
        {
            if (_stateBehaviorCoroutine == null) return;
            _owner.StopCoroutine(_stateBehaviorCoroutine);
            _stateBehaviorCoroutine = null;
        }

        private void StopChaseMonitor()
        {
            if (_chaseMonitorCoroutine == null) return;
            _owner.StopCoroutine(_chaseMonitorCoroutine);
            _chaseMonitorCoroutine = null;
        }

        private void StopWanderLifetime()
        {
            if (_wanderLifetimeCoroutine == null) return;
            _owner.StopCoroutine(_wanderLifetimeCoroutine);
            _wanderLifetimeCoroutine = null;
        }
    }
}
