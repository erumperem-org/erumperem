// ============================================================
// NpcEnemy.cs
// Namespace : Systems.NPC.Enemy
// ============================================================
// Responsabilidade única: orquestrar os colaboradores.
//
// NpcEnemy agora é um MonoBehaviour enxuto que:
//   1. Cria e conecta StateMachine, DetectionHandler e BehaviorRunner.
//   2. Implementa o ciclo de vida (Initialize / Activate / ReturnToPool).
//   3. Expõe OnPlayerContact como evento — sem saber o que acontece depois.
//
// Quem decide o que fazer no contato com o Player é o ouvinte
// externo (ex: NpcEnemyContactHandler), não este script.
// ============================================================

using System;
using Core.Exploration.Character.Movement;
using DetectionSystem.Core;
using Services.Navigation;
using Systems.NPC.Enemy.Contracts;
using Systems.NPC.Enemy.StateMachine;
using UnityEngine;

namespace Systems.NPC.Enemy
{
    [RequireComponent(typeof(NpcMovementController))]
    [RequireComponent(typeof(NavMeshAgentAdapter))]
    [RequireComponent(typeof(Detector))]
    public sealed class NpcEnemy : MonoBehaviour, INpcEnemy
    {
        // ── INpcEnemy ─────────────────────────────────────────────────────

        public NpcEnemyState CurrentState => _stateMachine?.Current ?? NpcEnemyState.Idle;

        /// <inheritdoc/>
        public event Action<INpcEnemy> OnPlayerContact;

        // ── Componentes Unity ─────────────────────────────────────────────

        private NpcMovementController _movementController;
        private NavMeshAgentAdapter   _adapter;
        private Detector              _detector;

        // ── Colaboradores (criados em Initialize) ─────────────────────────

        private NpcEnemyStateMachine    _stateMachine;
        private NpcEnemyDetectionHandler _detectionHandler;
        private NpcEnemyBehaviorRunner  _behaviorRunner;

        private NpcEnemyConfig _config;

        // ── Unity Awake ───────────────────────────────────────────────────

        private void Awake()
        {
            _movementController = GetComponent<NpcMovementController>();
            _adapter            = GetComponent<NavMeshAgentAdapter>();
            _detector           = GetComponent<Detector>();
        }

        // ═════════════════════════════════════════════════════════════════
        // INpcEnemy
        // ═════════════════════════════════════════════════════════════════

        public void Initialize(NpcEnemyConfig config)
        {
            _config = config;

            _stateMachine = new NpcEnemyStateMachine();

            _detectionHandler = new NpcEnemyDetectionHandler(_detector, _stateMachine, this, this);

            _behaviorRunner = new NpcEnemyBehaviorRunner(
                _movementController, _adapter, _stateMachine, this, config);

            // Conecta StateMachine → BehaviorRunner
            _stateMachine.OnEnterWander       += _behaviorRunner.RunWander;
            _stateMachine.OnEnterChase        += _behaviorRunner.RunChase;
            _stateMachine.OnEnterReturnToPool += OnReturnToPoolRequested;

            // BehaviorRunner notifica quando deve retornar à pool
            _behaviorRunner.OnShouldReturnToPool += ReturnToPool;
        }

        public void Activate()
        {
            Vector3 spawnPoint = _config.SpawnPoint;
            transform.position = spawnPoint;
            _movementController.NavMesh.EnableAgent(_adapter);

            var enemyView = GetComponent<NpcEnemyView>();
            if (enemyView != null)
            {
                enemyView.RefreshCorruptionTierVisuals();
            }

            if (!_movementController.NavMesh.Warp(_adapter, spawnPoint))
                _movementController.NavMesh.TeleportToNearestNavMeshPoint(_adapter, spawnPoint);

            _movementController.NavMesh.ResetAgent(_adapter);

            _detectionHandler.StartPolling();
            _stateMachine.ToWander();
        }

        public void ReturnToPool()
        {
            if (_stateMachine.Is(NpcEnemyState.ReturningToPool)) return;
            _stateMachine.ToReturnToPool();
        }

        // ═════════════════════════════════════════════════════════════════
        // Handlers internos
        // ═════════════════════════════════════════════════════════════════

        private void OnReturnToPoolRequested()
        {
            _detectionHandler.StopPolling();
            _behaviorRunner.StopAll();

            if (_config != null) _config.PursuitTarget = null;

            _movementController.NavMesh.Stop(_adapter);
            _movementController.NavMesh.ClearPath(_adapter);
            _movementController.NavMesh.SetVelocity(_adapter, Vector3.zero);
            _movementController.NavMesh.ResetAgent(_adapter);

            _config?.OnReturnToPool?.Invoke(this);
        }

        /// <summary>
        /// Chamado pelo NpcEnemyDetectionHandler quando a shape "Contact" detecta o Player.
        /// Dispara o evento — a decisão do que fazer fica fora desta classe.
        /// </summary>
        internal void NotifyPlayerContact()
        {
            OnPlayerContact?.Invoke(this);
        }

        // ═════════════════════════════════════════════════════════════════
        // OnDestroy
        // ═════════════════════════════════════════════════════════════════

        private void OnDestroy()
        {
            _detectionHandler?.StopPolling();
            _behaviorRunner?.StopAll();
        }
    }
}
