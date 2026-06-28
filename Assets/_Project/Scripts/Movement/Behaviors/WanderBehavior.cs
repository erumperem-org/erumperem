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
    /// Caminhada aleatória a partir da posição atual do agente.
    ///
    /// Responsabilidade: apenas movimentação orgânica pelo mapa.
    /// Detecção e troca de estratégia são responsabilidade de sistemas externos,
    /// notificados via <see cref="WanderBehaviorContext.OnPointReached"/>.
    /// </summary>
    public sealed class WanderBehavior : IReversibleCharacterMovementStrategy
    {
        private const float NavMeshSampleRadius = 1.5f;
        private const int PathPendingDelayMs = 5;
        private const int MovementPollDelayMs = 15;
        private const int RetryDelayMs = 50;
        private const int IdleAfterArrivalMs = 300;
        private const int MaxSampleAttempts = 10;

        private CancellationTokenSource _cts;

        public async Task ExecuteBehavior(ICharacterMovementStrategyContext context)
        {
            if (context is not WanderBehaviorContext ctx) return;

            _cts = new CancellationTokenSource();
            await WanderAsync(ctx, _cts.Token);
        }

        public async Task UnexecuteBehavior(ICharacterMovementStrategyContext context)
        {

            CancelImmediate();
            await Task.CompletedTask;
        }

        public void CancelImmediate()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private async Task WanderAsync(WanderBehaviorContext ctx, CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    ct.ThrowIfCancellationRequested();
                    ctx.center = ctx.centerFixed ? ctx.center : ctx.Self.position;
                    if (!TryGetRandomNavMeshPoint(ctx.center, ctx.WanderRadius, out var point))
                    {
                        await Task.Delay(RetryDelayMs, ct);
                        continue;
                    }

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

        private static bool TryGetRandomNavMeshPoint(Vector3 origin, float radius, out Vector3 result)
        {
            for (int i = 0; i < MaxSampleAttempts; i++)
            {
                Vector3 candidate = origin + Random.insideUnitSphere * radius;
                candidate.y = origin.y;

                if (NavMeshUtils.SamplePosition(candidate, out result, NavMeshSampleRadius))
                    return true;
            }

            result = origin;
            return false;
        }
    }

    public sealed class WanderBehaviorContext : CharacterMovementContextBase
    {
        public readonly float WanderRadius;
        public bool centerFixed;
        public Vector3 center;
        /// <summary>
        /// Disparado ao chegar em cada ponto.
        /// Sistema externo decide se troca de estratégia com base nisso.
        /// </summary>
        public readonly Action<Vector3> OnPointReached;

        public WanderBehaviorContext(
            NpcMovementController controller,
            INavMeshService navMesh,
            NavMeshAgentAdapter adapter,
            Transform self,
            Transform target,
            string characterName,
            float perceptionRadius,
            float wanderRadius,
            bool centerFixed,
            Vector3 center,
            Action<Vector3> onPointReached = null
            )
            : base(controller, navMesh, adapter, self, target, characterName, perceptionRadius)
        {
            WanderRadius = wanderRadius;
            OnPointReached = onPointReached;
            this.centerFixed = centerFixed;
            if(centerFixed)
            {
                this.center = center;
            }
            else
            {
                this.center = self.transform.position;
            }
        }
    }
}