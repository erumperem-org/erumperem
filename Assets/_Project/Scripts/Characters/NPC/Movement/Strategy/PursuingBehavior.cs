using System;
using System.Threading;
using System.Threading.Tasks;
using Services.DebugUtilities;
using Services.DebugUtilities.Console;
using Services.Navigation;
using UnityEngine;
using UnityEngine.AI;
using Core.Exploration.Character.NPC;

namespace Core.Exploration.Character.Movement
{
    public sealed class PursuingBehavior : IReverseableCharacterMovementStartegy
    {
        private const int PursuitUpdateDelayMs = 50;
        private const int IdleAtTargetDelayMs = 100;
        private const int PostArrivalDelayMs = 25;
        private CancellationTokenSource _cts;
        private INavMeshService _nav;
        public async Task ExecuteBehavior(ICharacterMovementStartegyContext context)
        {
            if (context is not PursuingBehaviorContext pursuingContext) return;

            LoggerService.PrintLogMessage(
                LogLevel.Debug, LogCategory.AI,
                $"Character [{pursuingContext.character.name}] is entering [PursuingBehavior]");

            _nav = ResolveNavService(pursuingContext);

            if (_nav == null)
            {
                LoggerService.PrintLogMessage(
                    LogLevel.Error, LogCategory.AI,
                    $"Character [{pursuingContext.character.name}] has no INavMeshService. " +
                    "Add NavMeshService to the enemy GameObject or inject it via context.");
                return;
            }

            _cts = new CancellationTokenSource();
            await PursueAsync(pursuingContext, _cts.Token);
        }

        public async Task UnexecuteBehavior(ICharacterMovementStartegyContext context)
        {
            if (context is PursuingBehaviorContext pursuingContext)
            {
                LoggerService.PrintLogMessage(
                    LogLevel.Debug, LogCategory.AI,
                    $"Character [{pursuingContext.character.name}] is exiting [PursuingBehavior]");
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
        private async Task PursueAsync(PursuingBehaviorContext context, CancellationToken ct)
        {
            try
            {
                float perceptionSq = context.perceptionRadius * context.perceptionRadius;
                float stoppingDistSq = context.agent.stoppingDistance
                                       * context.agent.stoppingDistance;

                while (!ct.IsCancellationRequested)
                {
                    ct.ThrowIfCancellationRequested();
                    float sqDist = (context.self.position - context.target.position).sqrMagnitude;

                    if (sqDist > perceptionSq)
                    {
                        await SwitchToPatrolAsync(context);
                        return;
                    }
                    if (sqDist <= stoppingDistSq)
                    {
                        _nav.Stop();

                        // TODO: Trocar cena
                        await Task.Delay(IdleAtTargetDelayMs, ct);
                        continue;
                    }

                    LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.Data, $"Character [{context.character.name}]: pursuing target at {context.target.position}");
                    _nav.MoveTo(context.target.position);
                    await WaitForPathAsync(ct);
                    bool switchedBehavior = await MonitorPursuitAsync(context, perceptionSq, ct);

                    if (switchedBehavior) return;

                    await Task.Delay(PostArrivalDelayMs, ct);
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
        private async Task<bool> MonitorPursuitAsync(
            PursuingBehaviorContext context,
            float perceptionSq,
            CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && !_nav.HasReachedDestination())
            {
                ct.ThrowIfCancellationRequested();
                float sqDist = (context.self.position - context.target.position).sqrMagnitude;

                if (sqDist > perceptionSq)
                {
                    await SwitchToPatrolAsync(context);
                    return true;
                }

                _nav.SetDestination(context.target.position);
                await Task.Delay(PursuitUpdateDelayMs, ct);
            }

            return false;
        }

        private async Task SwitchToPatrolAsync(PursuingBehaviorContext context)
        {
            _nav.Stop();

            await ExplorationNpcMovementController.SetNpcMovementStartegy(context.controller, new PatrolBehavior(), new PatrolBehaviorContext(context.controller, context.character, context.perceptionRadius, context.patrolRadius, context.target, context.self, context.navMeshService, context.agent));
        }

        private async Task WaitForPathAsync(CancellationToken ct)
        {
            while (_nav.IsPending() && !ct.IsCancellationRequested)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Yield();
            }
        }

        private static INavMeshService ResolveNavService(PursuingBehaviorContext context)
        {
            if (context.navMeshService != null)
                return context.navMeshService;

            return context.self.GetComponent<INavMeshService>();
        }
    }

    public class PursuingBehaviorContext : ICharacterMovementStartegyContext
    {
        public ExplorationNpcMovementController controller;
        public CharacterData character;
        public float perceptionRadius;
        public float patrolRadius;
        public Transform target;
        public Transform self;
        public NavMeshService navMeshService;
        public NavMeshAgent agent;
        public PursuingBehaviorContext(ExplorationNpcMovementController controller, CharacterData character, float perceptionRadius, float patrolRadius, Transform target, Transform self, NavMeshService service, NavMeshAgent agent)
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