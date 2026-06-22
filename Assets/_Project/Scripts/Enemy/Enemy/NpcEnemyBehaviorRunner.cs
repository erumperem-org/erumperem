// ============================================================
// NpcEnemyBehaviorRunner.cs
// Namespace : Systems.NPC.Enemy
// ============================================================
// Responsabilidade única: executar as coroutines de comportamento
// (Wander, Chase, ChaseMonitor, WanderLifetime) em resposta às
// transições de estado notificadas pela StateMachine.
//
// Não sabe nada de detecção, pool ou transição de cena.
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

        private const float ChaseMonitorInterval  = 0.2f;

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

        private IEnumerator ChaseRadiusMonitorCoroutine()
        {
            var wait = new WaitForSeconds(ChaseMonitorInterval);

            while (_stateMachine.Is(NpcEnemyState.Chase))
            {
                float dist = Vector3.Distance(_owner.transform.position, _config.SpawnPoint);
                if (dist > _config.ChaseRadius)
                {
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
