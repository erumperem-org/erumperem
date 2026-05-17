using System;
using System.Threading;
using System.Threading.Tasks;
using Services.DebugUtilities;
using Services.Navigation;
using UnityEngine;

namespace Core.Exploration.Character.Movement
{
    /// <summary>
    /// Move o agente para um ponto fixo no mundo.
    /// Ao chegar (ou falhar), transiciona para <see cref="FreeBehavior"/> automaticamente.
    /// </summary>
    public sealed class GoToPointBehavior : IReversibleCharacterMovementStrategy
    {
        private const float NavMeshSampleRadius = 1f;
        private const int   PathPendingDelayMs  = 5;
        private const int   MovementPollDelayMs = 10;

        private CancellationTokenSource _cts;

        public async Task ExecuteBehavior(ICharacterMovementStrategyContext context)
        {
            if (context is not GoToPointBehaviorContext ctx) return;

            LoggerService.PrintLogMessage(
                LogLevel.Debug,
                $"[{ctx.CharacterName}] → [GoToPointBehavior] destino: {ctx.Destination}", LogCategory.NPC, LogCategory.AI, LogCategory.Navigation);

            _cts = new CancellationTokenSource();
            await GoToAsync(ctx, _cts.Token);
        }

        public async Task UnexecuteBehavior(ICharacterMovementStrategyContext context)
        {
            if (context is GoToPointBehaviorContext ctx)
                LoggerService.PrintLogMessage(
                    LogLevel.Debug,
                    $"[{ctx.CharacterName}] saindo de [GoToPointBehavior]", LogCategory.NPC, LogCategory.AI, LogCategory.Navigation);

            CancelImmediate();
            await Task.CompletedTask;
        }

        public void CancelImmediate()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        // ── Lógica principal ──────────────────────────────────────────────

        private async Task GoToAsync(GoToPointBehaviorContext ctx, CancellationToken ct)
        {
            try
            {
                // Valida o ponto antes de mover
                if (!ctx.NavMesh.SamplePosition(ctx.Destination, out var sampledPoint, NavMeshSampleRadius))
                {
                    LoggerService.PrintLogMessage(
                        LogLevel.Warning,
                        $"[{ctx.CharacterName}] [GoToPointBehavior] destino fora da NavMesh: {ctx.Destination}", LogCategory.NPC, LogCategory.AI, LogCategory.Navigation);

                    await FallbackToFreeAsync(ctx);
                    return;
                }

                ctx.NavMesh.MoveTo(ctx.Adapter, sampledPoint);

                // Aguarda cálculo do caminho
                while (ctx.NavMesh.IsPending(ctx.Adapter) && !ct.IsCancellationRequested)
                {
                    ct.ThrowIfCancellationRequested();
                    await Task.Delay(PathPendingDelayMs, ct);
                }

                if (!ctx.NavMesh.IsPathComplete(ctx.Adapter))
                {
                    LoggerService.PrintLogMessage(
                        LogLevel.Warning,
                        $"[{ctx.CharacterName}] [GoToPointBehavior] caminho incompleto para {sampledPoint}", LogCategory.NPC, LogCategory.AI, LogCategory.Navigation);

                    await FallbackToFreeAsync(ctx);
                    return;
                }

                // Aguarda chegada
                while (!ct.IsCancellationRequested && !ctx.NavMesh.HasReachedDestination(ctx.Adapter))
                {
                    ct.ThrowIfCancellationRequested();
                    await Task.Delay(MovementPollDelayMs, ct);
                }

                LoggerService.PrintLogMessage(
                    LogLevel.Debug,
                    $"[{ctx.CharacterName}] [GoToPointBehavior] destino alcançado", LogCategory.NPC, LogCategory.AI, LogCategory.Navigation);

                ctx.OnArrived?.Invoke();
                await FallbackToFreeAsync(ctx);
            }
            catch (OperationCanceledException) { }
            finally
            {
                ctx.NavMesh.Stop(ctx.Adapter);
            }
        }

        private static async Task FallbackToFreeAsync(GoToPointBehaviorContext ctx)
        {
            var freeCtx = new FreeBehaviorContext(
                ctx.Controller, ctx.NavMesh, ctx.Adapter, ctx.Self, ctx.CharacterName);

            await ctx.Controller.SetStrategy(new FreeBehavior(), freeCtx);
        }
    }

    public sealed class GoToPointBehaviorContext : CharacterMovementContextBase
    {
        public readonly Vector3 Destination;

        /// <summary>
        /// Callback opcional invocado ao chegar ao destino.
        /// Executado antes da transição para FreeBehavior.
        /// </summary>
        public readonly Action OnArrived;

        public GoToPointBehaviorContext(
            NpcMovementController controller,
            INavMeshService       navMesh,
            NavMeshAgentAdapter   adapter,
            Transform             self,
            string                characterName,
            float                 perceptionRadius,
            Transform             target,
            Vector3               destination,
            Action                onArrived = null)
            : base(controller, navMesh, adapter, self, target, characterName, perceptionRadius)
        {
            Destination = destination;
            OnArrived   = onArrived;
        }
    }
}
