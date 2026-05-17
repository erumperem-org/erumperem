using System;
using System.Threading;
using System.Threading.Tasks;
using Services.DebugUtilities;
using Services.Navigation;
using UnityEngine;

namespace Core.Exploration.Character.Movement
{
    /// <summary>
    /// Perseguição ativa: atualiza o destino continuamente na direção do alvo.
    ///
    /// Responsabilidade: apenas movimentação em direção ao alvo.
    /// O que fazer ao alcançar ou perder o alvo é responsabilidade de sistemas externos,
    /// notificados via <see cref="PursuingBehaviorContext.OnTargetReached"/> e
    /// <see cref="PursuingBehaviorContext.OnTargetLost"/>.
    /// </summary>
    public sealed class PursuingBehavior : IReversibleCharacterMovementStrategy
    {
        private const int   DestinationUpdateMs = 100;
        private const int   MovementPollDelayMs = 15;

        private CancellationTokenSource _cts;

        public async Task ExecuteBehavior(ICharacterMovementStrategyContext context)
        {
            if (context is not PursuingBehaviorContext ctx) return;

            LoggerService.PrintLogMessage(
                LogLevel.Debug,
                $"[{ctx.CharacterName}] → [PursuingBehavior]", LogCategory.NPC, LogCategory.AI, LogCategory.Navigation);

            _cts = new CancellationTokenSource();
            await PursueAsync(ctx, _cts.Token);
        }

        public async Task UnexecuteBehavior(ICharacterMovementStrategyContext context)
        {
            if (context is PursuingBehaviorContext ctx)
                LoggerService.PrintLogMessage(
                    LogLevel.Debug,
                    $"[{ctx.CharacterName}] saindo de [PursuingBehavior]", LogCategory.NPC, LogCategory.AI, LogCategory.Navigation);

            CancelImmediate();
            await Task.CompletedTask;
        }

        public void CancelImmediate()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private async Task PursueAsync(PursuingBehaviorContext ctx, CancellationToken ct)
        {
            float lastUpdateTime = 0f;
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    ct.ThrowIfCancellationRequested();

                    if (ctx.Target == null)
                    {
                        ctx.NavMesh.Stop(ctx.Adapter);
                        return;
                    }

                    float now = Time.time;
                    if (now - lastUpdateTime >= DestinationUpdateMs / 1000f)
                    {
                        ctx.NavMesh.SetDestination(ctx.Adapter, ctx.Target.position);
                        lastUpdateTime = now;
                    }

                    await Task.Delay(MovementPollDelayMs, ct);
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                ctx.NavMesh.Stop(ctx.Adapter);
            }
        }
    }

    public sealed class PursuingBehaviorContext : CharacterMovementContextBase
    {
        public PursuingBehaviorContext(
            NpcMovementController controller,
            INavMeshService       navMesh,
            NavMeshAgentAdapter   adapter,
            Transform             self,
            Transform             target,
            string                characterName,
            float                 perceptionRadius)
            : base(controller, navMesh, adapter, self, target, characterName, perceptionRadius)
        {
            
        }
    }
}