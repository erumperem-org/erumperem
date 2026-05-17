using System;
using System.Threading;
using System.Threading.Tasks;
using Services.DebugUtilities;
using Services.Navigation;
using UnityEngine;

namespace Core.Exploration.Character.Movement
{
    /// <summary>
    /// Estado neutro: o agente está parado sem executar nenhuma rotina.
    /// Serve como estratégia padrão ao inicializar o controller ou
    /// ao sair de qualquer outro behavior sem ter um próximo definido.
    /// </summary>
    public sealed class FreeBehavior : IReversibleCharacterMovementStrategy
    {
        private CancellationTokenSource _cts;

        public async Task ExecuteBehavior(ICharacterMovementStrategyContext context)
        {
            if (context is not FreeBehaviorContext ctx) return;

            LoggerService.PrintLogMessage(
                LogLevel.Debug,
                $"[{ctx.CharacterName}] → [FreeBehavior]", LogCategory.NPC, LogCategory.AI, LogCategory.Navigation);

            ctx.NavMesh.Stop(ctx.Adapter);

            _cts = new CancellationTokenSource();

            // Fica vivo aguardando cancelamento externo.
            // Não consome CPU — apenas bloqueia na task de cancelamento.
            try
            {
                await Task.Delay(Timeout.Infinite, _cts.Token);
            }
            catch (OperationCanceledException) { }
        }

        public async Task UnexecuteBehavior(ICharacterMovementStrategyContext context)
        {
            if (context is FreeBehaviorContext ctx)
                LoggerService.PrintLogMessage(
                    LogLevel.Debug,
                    $"[{ctx.CharacterName}] saindo de [FreeBehavior]", LogCategory.NPC, LogCategory.AI, LogCategory.Navigation);

            CancelImmediate();
            await Task.CompletedTask;
        }

        public void CancelImmediate()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
    }

    public sealed class FreeBehaviorContext : CharacterMovementContextBase
    {
        public FreeBehaviorContext(
            NpcMovementController controller,
            INavMeshService       navMesh,
            NavMeshAgentAdapter   adapter,
            Transform             self,
            string                characterName)
            : base(controller, navMesh, adapter, self, null, characterName, 0f) { }
    }
}
