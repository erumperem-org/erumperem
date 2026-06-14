// ============================================================
// ExplorationEnemyRoutineCatalog.cs
// Namespace : Systems.NPC.Exploration.Behaviours
// ============================================================
// Responsabilidade única (SRP): possuir e gerenciar todas as
// Coroutines de comportamento do inimigo (Wander, Chase, etc.).
//
// O Catalog NÃO decide quando iniciar um comportamento — isso
// é papel do Controller. Ele apenas expõe métodos para
// iniciar / parar cada rotina de forma segura.
//
// Depende de IExplorationEnemyRoutineContext para acessar
// os dados e componentes do inimigo sem acoplamento direto
// ao ExplorationEnemyController.
// ============================================================

using System.Collections;
using Core.Exploration.Character.Movement;
using Services.Navigation;
using Systems.NPC.Enemy.Contracts;
using UnityEngine;

namespace Systems.NPC.Exploration.Behaviours
{
    /// <summary>
    /// Contexto que o Catalog precisa para executar as rotinas.
    /// Implementado pelo ExplorationEnemyController.
    /// </summary>
    public interface IExplorationEnemyRoutineContext
    {
        Systems.NPC.Enemy.Contracts.NpcEnemyConfig     Config             { get; }
        NpcEnemyState      CurrentState       { get; }
        NpcMovementController      MovementController { get; }
        NavMeshAgentAdapter        Adapter            { get; }
        Transform                  SelfTransform      { get; }
        string                     CharacterName      { get; }

        /// <summary>Notifica o Controller que o Lifetime do Wander expirou.</summary>
        void NotifyWanderLifetimeExpired();

        /// <summary>Notifica o Controller que o inimigo saiu do raio de chase.</summary>
        void NotifyChaseRadiusExceeded();
    }

    public sealed class ExplorationEnemyRoutineCatalog
    {
        // ── Constantes ────────────────────────────────────────────────────

        private const float ChaseMonitorInterval  = 0.2f;
        private const float ContactCheckInterval  = 0.1f;

        // ── Estado interno ────────────────────────────────────────────────

        private readonly MonoBehaviour          _coroutineRunner;
        private readonly IExplorationEnemyRoutineContext _context;

        private Coroutine                       _stateBehaviorCoroutine;
        private Coroutine                       _chaseMonitorCoroutine;
        private Coroutine                       _wanderLifetimeCoroutine;
        private IReversibleCharacterMovementStrategy _activeBehavior;

        // ── Construtor ────────────────────────────────────────────────────

        /// <param name="coroutineRunner">MonoBehaviour usado para StartCoroutine/StopCoroutine.</param>
        /// <param name="context">Dados e componentes do inimigo.</param>
        public ExplorationEnemyRoutineCatalog(
            MonoBehaviour coroutineRunner,
            IExplorationEnemyRoutineContext context)
        {
            _coroutineRunner = coroutineRunner;
            _context         = context;
        }

        // ── API pública — entrada em estados ─────────────────────────────

        /// <summary>Inicia as rotinas do estado Wander.</summary>
        public void StartWander()
        {
            StopAll();

            _stateBehaviorCoroutine  = _coroutineRunner.StartCoroutine(WanderCoroutine());
            _wanderLifetimeCoroutine = _coroutineRunner.StartCoroutine(WanderLifetimeCoroutine());
        }

        /// <summary>Inicia as rotinas do estado Pursuing para o alvo dado.</summary>
        public void StartPursuing(Transform target)
        {
            if (target == null) return;

            StopAll();

            _context.Config.PursuitTarget = target;
            _stateBehaviorCoroutine = _coroutineRunner.StartCoroutine(PursuingCoroutine());
            _chaseMonitorCoroutine  = _coroutineRunner.StartCoroutine(ChaseRadiusMonitorCoroutine());
        }

        /// <summary>Para todas as rotinas e cancela o comportamento ativo de movimento.</summary>
        public void StopAll()
        {
            CancelActiveBehavior();
            StopStateBehavior();
            StopChaseMonitor();
            StopWanderLifetime();

            if (_context.Adapter != null && _context.MovementController != null)
                _context.MovementController.NavMesh.Stop(_context.Adapter);
        }

        // ── Coroutines de comportamento ───────────────────────────────────

        private IEnumerator WanderCoroutine()
        {
            var ctx = new WanderBehaviorContext(
                controller:       _context.MovementController,
                navMesh:          _context.MovementController.NavMesh,
                adapter:          _context.Adapter,
                self:             _context.SelfTransform,
                target:           null,
                characterName:    _context.CharacterName,
                perceptionRadius: 0f,
                wanderRadius:     _context.Config.WanderRadius,
                centerFixed:      true,
                center:           _context.Config.SpawnPoint,
                onPointReached:   null
            );

            var behavior = new WanderBehavior();
            _activeBehavior = behavior;
            _ = behavior.ExecuteBehavior(ctx);

            while (_context.CurrentState == NpcEnemyState.Wander)
                yield return new WaitForSeconds(0.5f);
        }

        private IEnumerator WanderLifetimeCoroutine()
        {
            yield return new WaitForSeconds(_context.Config.WanderLifetime);

            if (_context.CurrentState == NpcEnemyState.Wander)
            {
                Debug.Log($"[RoutineCatalog] '{_context.CharacterName}' expirou WanderLifetime " +
                          $"({_context.Config.WanderLifetime}s). Notificando Controller.");
                _context.NotifyWanderLifetimeExpired();
            }
        }

        private IEnumerator PursuingCoroutine()
        {
            if (_context.Config.PursuitTarget == null)
            {
                _context.NotifyChaseRadiusExceeded();
                yield break;
            }

            var ctx = new PursuingBehaviorContext(
                controller:       _context.MovementController,
                navMesh:          _context.MovementController.NavMesh,
                adapter:          _context.Adapter,
                self:             _context.SelfTransform,
                target:           _context.Config.PursuitTarget,
                characterName:    _context.CharacterName,
                perceptionRadius: _context.Config.ChaseRadius
            );

            var behavior = new PursuingBehavior();
            _activeBehavior = behavior;
            _ = behavior.ExecuteBehavior(ctx);

            yield return null;
        }

        private IEnumerator ChaseRadiusMonitorCoroutine()
        {
            var wait = new WaitForSeconds(ChaseMonitorInterval);

            while (_context.CurrentState == NpcEnemyState.Chase)
            {
                float dist = Vector3.Distance(
                    _context.SelfTransform.position,
                    _context.Config.SpawnPoint);

                if (dist > _context.Config.ChaseRadius)
                {
                    _context.NotifyChaseRadiusExceeded();
                    yield break;
                }

                yield return wait;
            }
        }

        // ── Helpers privados ──────────────────────────────────────────────

        private void CancelActiveBehavior()
        {
            _activeBehavior?.CancelImmediate();
            _activeBehavior = null;
        }

        private void StopStateBehavior()
        {
            if (_stateBehaviorCoroutine == null) return;
            _coroutineRunner.StopCoroutine(_stateBehaviorCoroutine);
            _stateBehaviorCoroutine = null;
        }

        private void StopChaseMonitor()
        {
            if (_chaseMonitorCoroutine == null) return;
            _coroutineRunner.StopCoroutine(_chaseMonitorCoroutine);
            _chaseMonitorCoroutine = null;
        }

        private void StopWanderLifetime()
        {
            if (_wanderLifetimeCoroutine == null) return;
            _coroutineRunner.StopCoroutine(_wanderLifetimeCoroutine);
            _wanderLifetimeCoroutine = null;
        }
    }
}
