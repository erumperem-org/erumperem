using System;
using System.Threading;
using System.Threading.Tasks;
using Services.DebugUtilities;
using Services.Navigation;
using UnityEngine;

namespace Core.Exploration.Character.Movement
{
    /// <summary>
    /// Stalking: mantém o alvo dentro de uma banda de distância
    /// [<see cref="StalkingBehaviorContext.MinDistance"/>, <see cref="StalkingBehaviorContext.MaxDistance"/>].
    ///
    /// Responsabilidade: apenas movimentação de manutenção de distância.
    /// O que fazer ao perder o alvo ou ao entrar na banda é responsabilidade de sistemas externos,
    /// notificados via <see cref="StalkingBehaviorContext.OnTargetLost"/> e
    /// <see cref="StalkingBehaviorContext.OnObserving"/>.
    /// </summary>
    public sealed class StalkingBehavior : IReversibleCharacterMovementStrategy
    {
        private const int   UpdateIntervalMs    = 120;
        private const float NavMeshSampleRadius = 2f;

        private CancellationTokenSource _cts;

        public async Task ExecuteBehavior(ICharacterMovementStrategyContext context)
        {
            if (context is not StalkingBehaviorContext ctx) return;

            LoggerService.PrintLogMessage(
                LogLevel.Debug,
                $"[{ctx.CharacterName}] → [StalkingBehavior] banda [{ctx.MinDistance}, {ctx.MaxDistance}]", LogCategory.NPC, LogCategory.AI, LogCategory.Navigation);

            _cts = new CancellationTokenSource();
            await StalkAsync(ctx, _cts.Token);
        }

        public async Task UnexecuteBehavior(ICharacterMovementStrategyContext context)
        {
            if (context is StalkingBehaviorContext ctx)
                LoggerService.PrintLogMessage(
                    LogLevel.Debug,
                    $"[{ctx.CharacterName}] saindo de [StalkingBehavior]", LogCategory.NPC, LogCategory.AI, LogCategory.Navigation);

            CancelImmediate();
            await Task.CompletedTask;
        }

        public void CancelImmediate()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private async Task StalkAsync(StalkingBehaviorContext ctx, CancellationToken ct)
        {
            float perceptionSq = ctx.PerceptionRadius * ctx.PerceptionRadius;
            float minSq        = ctx.MinDistance      * ctx.MinDistance;
            float maxSq        = ctx.MaxDistance      * ctx.MaxDistance;

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    ct.ThrowIfCancellationRequested();

                    if (ctx.Target == null)
                    {
                        ctx.NavMesh.Stop(ctx.Adapter);
                        ctx.OnTargetLost?.Invoke();
                        return;
                    }

                    Vector3 toTarget = ctx.Target.position - ctx.Self.position;
                    float   sqDist   = toTarget.sqrMagnitude;

                    if (sqDist > perceptionSq)
                    {
                        ctx.NavMesh.Stop(ctx.Adapter);

                        LoggerService.PrintLogMessage(
                            LogLevel.Debug,
                            $"[{ctx.CharacterName}] [StalkingBehavior] alvo perdido", LogCategory.NPC, LogCategory.AI, LogCategory.Navigation);

                        ctx.OnTargetLost?.Invoke();
                        return;
                    }

                    if (sqDist > maxSq)
                    {
                        // Avança até ficar dentro da banda
                        Vector3 advanceTarget = ctx.Target.position - toTarget.normalized * ctx.MaxDistance;
                        ctx.NavMesh.SetDestination(ctx.Adapter, advanceTarget);
                        ctx.NavMesh.Resume(ctx.Adapter);
                    }
                    else if (sqDist < minSq)
                    {
                        // Recua até ficar dentro da banda
                        Vector3 retreatPoint = ctx.Self.position - toTarget.normalized * ctx.MinDistance;

                        if (ctx.NavMesh.SamplePosition(retreatPoint, out var sampled, NavMeshSampleRadius))
                        {
                            ctx.NavMesh.SetDestination(ctx.Adapter, sampled);
                            ctx.NavMesh.Resume(ctx.Adapter);
                        }
                        else
                        {
                            ctx.NavMesh.Stop(ctx.Adapter);
                        }
                    }
                    else
                    {
                        // Dentro da banda — para e notifica
                        ctx.NavMesh.Stop(ctx.Adapter);
                        ctx.OnObserving?.Invoke();
                    }

                    await Task.Delay(UpdateIntervalMs, ct);
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                ctx.NavMesh.Stop(ctx.Adapter);
            }
        }
    }

    public sealed class StalkingBehaviorContext : CharacterMovementContextBase
    {
        public readonly float  MinDistance;
        public readonly float  MaxDistance;

        /// <summary>Disparado quando o alvo sai do raio de percepção ou é nulo.</summary>
        public readonly Action OnTargetLost;

        /// <summary>Disparado a cada tick enquanto o agente está dentro da banda (observando).</summary>
        public readonly Action OnObserving;

        public StalkingBehaviorContext(
            NpcMovementController controller,
            INavMeshService       navMesh,
            NavMeshAgentAdapter   adapter,
            Transform             self,
            Transform             target,
            string                characterName,
            float                 perceptionRadius,
            float                 minDistance  = 4f,
            float                 maxDistance  = 8f,
            Action                onTargetLost = null,
            Action                onObserving  = null)
            : base(controller, navMesh, adapter, self, target, characterName, perceptionRadius)
        {
            MinDistance  = minDistance;
            MaxDistance  = maxDistance;
            OnTargetLost = onTargetLost;
            OnObserving  = onObserving;
        }
    }
}