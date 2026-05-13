using System;
using System.Threading;
using System.Threading.Tasks;
using Services.DebugUtilities;
using Services.DebugUtilities.Console;
using Services.Navigation;
using UnityEngine;
using Core.Exploration.Character.NPC;
using UnityEngine.AI;

namespace Core.Exploration.Character.Movement
{
    public sealed class PatrolBehavior : IReverseableCharacterMovementStartegy
    {
        private const float NavMeshSampleRadius = 1f;
        private const int InnerLoopDelayMs = 5;
        private const int IdleAfterArrivalMs = 100;
        private const int RetryDelayMs = 25;
        private CancellationTokenSource _cts;
        private INavMeshService _nav;

        public async Task ExecuteBehavior(ICharacterMovementStartegyContext context)
        {
            if (context is not PatrolBehaviorContext patrolContext) return;

            LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.AI, $"Character [{patrolContext.character.name}] is entering [PatrolBehavior]");

            _nav = ResolveNavService(patrolContext);

            if (_nav == null)
            {
                LoggerService.PrintLogMessage(LogLevel.Error, LogCategory.AI, $"Character [{patrolContext.character.name}] has no INavMeshService. " + "Add NavMeshService to the enemy GameObject or inject it via context.");
                return;
            }

            _cts = new CancellationTokenSource();
            await PatrolAsync(patrolContext, _cts.Token);
        }

        public async Task UnexecuteBehavior(ICharacterMovementStartegyContext context)
        {
            if (context is PatrolBehaviorContext patrolContext)
            {
                LoggerService.PrintLogMessage(
                    LogLevel.Debug, LogCategory.AI,
                    $"Character [{patrolContext.character.name}] is exiting [PatrolBehavior]");
            }

            CancelImmediate();
            await Task.CompletedTask;
        }

        public void CancelImmediate()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private async Task PatrolAsync(PatrolBehaviorContext context, CancellationToken ct)
        {
            try
            {
                float perceptionSq = context.perceptionRadius * context.perceptionRadius;

                while (!ct.IsCancellationRequested)
                {
                    ct.ThrowIfCancellationRequested();

                    // ── Verifica percepção antes de selecionar próximo ponto ───────
                    if (IsTargetPerceived(context, perceptionSq))
                    {
                        await SwitchToPursuingAsync(context);
                        return;
                    }

                    // ── Tenta obter um ponto de patrulha válido na NavMesh ─────────
                    Vector3 patrolCenter = context.self.position;

                    if (!TryGetRandomNavMeshPoint(patrolCenter, context.patrolRadius, out Vector3 targetPoint))
                    {
                        await Task.Delay(RetryDelayMs, ct);
                        continue;
                    }

                    LoggerService.PrintLogMessage(
                        LogLevel.Debug, LogCategory.Data,
                        $"Character [{context.character.name}]: new patrol point {targetPoint}");

                    _nav.MoveTo(targetPoint);

                    // ── Aguarda cálculo do caminho ─────────────────────────────────
                    await WaitForPathAsync(ct);

                    // ── Loop interno: caminha ao ponto monitorando percepção ────────
                    bool switchedBehavior = await MonitorPatrolMovementAsync(context, perceptionSq, ct);

                    if (switchedBehavior) return;

                    // Pequeno idle ao chegar ao ponto antes de escolher o próximo
                    await Task.Delay(IdleAfterArrivalMs, ct);
                }
            }
            catch (OperationCanceledException)
            {
                // Cancelamento esperado
            }
            finally
            {
                _nav?.Stop();
            }
        }

        private async Task<bool> MonitorPatrolMovementAsync(
            PatrolBehaviorContext context,
            float perceptionSq,
            CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && !_nav.HasReachedDestination())
            {
                ct.ThrowIfCancellationRequested();

                if (IsTargetPerceived(context, perceptionSq))
                {
                    await SwitchToPursuingAsync(context);
                    return true;
                }

                await Task.Delay(InnerLoopDelayMs, ct);
            }

            return false;
        }

        private async Task SwitchToPursuingAsync(PatrolBehaviorContext context)
        {
            _nav.Stop();

            await ExplorationNpcMovementController.SetNpcMovementStartegy(context.controller, new PursuingBehavior(), new PursuingBehaviorContext(context.controller, context.character, context.perceptionRadius, context.patrolRadius, context.target, context.self, context.navMeshService, context.agent));
        }

        private static bool IsTargetPerceived(PatrolBehaviorContext context, float perceptionSq)
        {
            float sqDist = (context.self.position - context.target.position).sqrMagnitude;
            return sqDist <= perceptionSq;
        }

        private bool TryGetRandomNavMeshPoint(Vector3 center, float range, out Vector3 result)
        {
            Vector3 randomPoint = center + UnityEngine.Random.insideUnitSphere * range;
            return _nav.SamplePosition(randomPoint, out result, NavMeshSampleRadius);
        }

        private async Task WaitForPathAsync(CancellationToken ct)
        {
            while (_nav.IsPending() && !ct.IsCancellationRequested)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Yield();
            }
        }

        private static INavMeshService ResolveNavService(PatrolBehaviorContext context)
        {
            if (context.navMeshService != null)
                return context.navMeshService;

            return context.self.GetComponent<INavMeshService>();
        }
    }

    public class PatrolBehaviorContext : ICharacterMovementStartegyContext
    {
        public ExplorationNpcMovementController controller;
        public CharacterData character;
        public float perceptionRadius;
        public float patrolRadius;
        public Transform target;
        public Transform self;
        public NavMeshService navMeshService;
        public NavMeshAgent agent;
        public PatrolBehaviorContext(ExplorationNpcMovementController controller, CharacterData character, float perceptionRadius, float patrolRadius, Transform target, Transform self, NavMeshService service, NavMeshAgent agent)
        {
            this.controller = controller;
            this.character = character;
            this.target = target;
            this.perceptionRadius = perceptionRadius;
            this.patrolRadius = patrolRadius;
            this.self = self;
            this.navMeshService = service;
            this.agent = agent;
        }
    }
}