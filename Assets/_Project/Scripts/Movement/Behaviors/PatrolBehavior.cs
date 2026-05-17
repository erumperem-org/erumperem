using System;
using System.Threading;
using System.Threading.Tasks;
using Services.DebugUtilities;
using Services.Navigation;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Core.Exploration.Character.Movement
{
    /// <summary>
    /// Patrulha aleatória em torno de um centro fixo.
    ///
    /// Responsabilidade: apenas movimentação entre pontos aleatórios.
    /// Detecção de alvo e troca de estratégia são responsabilidade de sistemas externos,
    /// notificados via <see cref="PatrolBehaviorContext.OnPointReached"/>.
    /// </summary>
    public sealed class PatrolBehavior : IReversibleCharacterMovementStrategy
    {
        private const float NavMeshSampleRadius = 1f;
        private const int   PathPendingDelayMs  = 5;
        private const int   MovementPollDelayMs = 10;
        private const int   RetryDelayMs        = 25;
        private const int   IdleAfterArrivalMs  = 500;

        private CancellationTokenSource _cts;

        public async Task ExecuteBehavior(ICharacterMovementStrategyContext context)
        {
            if (context is not PatrolBehaviorContext ctx) return;

            LoggerService.PrintLogMessage(
                LogLevel.Debug,
                $"[{ctx.CharacterName}] → [PatrolBehavior]", LogCategory.NPC, LogCategory.AI, LogCategory.Navigation);

            _cts = new CancellationTokenSource();
            await PatrolAsync(ctx, _cts.Token);
        }

        public async Task UnexecuteBehavior(ICharacterMovementStrategyContext context)
        {
            if (context is PatrolBehaviorContext ctx)
                LoggerService.PrintLogMessage(
                    LogLevel.Debug,
                    $"[{ctx.CharacterName}] saindo de [PatrolBehavior]", LogCategory.NPC, LogCategory.AI, LogCategory.Navigation);

            CancelImmediate();
            await Task.CompletedTask;
        }

        public void CancelImmediate()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private async Task PatrolAsync(PatrolBehaviorContext ctx, CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    ct.ThrowIfCancellationRequested();

                    if (!TryGetRandomNavMeshPoint(ctx.PatrolCenter, ctx.PatrolRadius, out var point))
                    {
                        await Task.Delay(RetryDelayMs, ct);
                        continue;
                    }

                    LoggerService.PrintLogMessage(
                        LogLevel.Debug,
                        $"[{ctx.CharacterName}] [PatrolBehavior] novo ponto: {point}", LogCategory.NPC, LogCategory.AI, LogCategory.Navigation);

                    ctx.NavMesh.MoveTo(ctx.Adapter, point);

                    while (ctx.NavMesh.IsPending(ctx.Adapter) && !ct.IsCancellationRequested)
                    {
                        ct.ThrowIfCancellationRequested();
                        await Task.Delay(PathPendingDelayMs, ct);
                    }

                    while (!ct.IsCancellationRequested && !ctx.NavMesh.HasReachedDestination(ctx.Adapter))
                    {
                        ct.ThrowIfCancellationRequested();
                        await Task.Delay(MovementPollDelayMs, ct);
                    }

                    if (ct.IsCancellationRequested) break;

                    ctx.OnPointReached?.Invoke(point);

                    await Task.Delay(IdleAfterArrivalMs, ct);
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                ctx.NavMesh.Stop(ctx.Adapter);
            }
        }

        private static bool TryGetRandomNavMeshPoint(Vector3 center, float radius, out Vector3 result)
        {
            Vector3 candidate = center + Random.insideUnitSphere * radius;
            candidate.y = center.y;
            return NavMeshUtils.SamplePosition(candidate, out result, NavMeshSampleRadius);
        }
    }

    public sealed class PatrolBehaviorContext : CharacterMovementContextBase
    {
        public readonly Vector3         PatrolCenter;
        public readonly float           PatrolRadius;

        /// <summary>
        /// Disparado ao chegar em cada ponto de patrulha.
        /// Sistema externo decide se troca de estratégia com base nisso.
        /// </summary>
        public readonly Action<Vector3> OnPointReached;

        public PatrolBehaviorContext(
            NpcMovementController controller,
            INavMeshService       navMesh,
            NavMeshAgentAdapter   adapter,
            Transform             self,
            Transform             target,
            string                characterName,
            float                 perceptionRadius,
            Vector3               patrolCenter,
            float                 patrolRadius,
            Action<Vector3>       onPointReached = null)
            : base(controller, navMesh, adapter, self, target, characterName, perceptionRadius)
        {
            PatrolCenter   = patrolCenter;
            PatrolRadius   = patrolRadius;
            OnPointReached = onPointReached;
        }
    }
}