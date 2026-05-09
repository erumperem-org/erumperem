using System.Threading.Tasks;
using Services.DebugUtilities;
using Services.DebugUtilities.Console;

namespace Core.Exploration.Enemy
{
    /// <summary>
    /// Estratégia executada quando o inimigo é devolvido à pool (inativo).
    /// Reposiciona o inimigo no ponto de pool e o move para o parent de objetos inativos.
    /// Também é chamada ao "desativar" (UnexecuteBehavior) caso seja trocada enquanto ativa.
    /// </summary>
    public class OnPoolBehavior : IReverseableEnemyStartegy
    {
        /// <summary>
        /// Reposiciona o inimigo e o reparenta para a hierarquia de poolados.
        /// </summary>
        public Task ExecuteBehavior(IEnemyStartegyContext context)
        {
            if (context is OnPoolBehaviorContext onPoolContext)
            {
                LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.AI,
                    $"Inimigo [{onPoolContext.Enemy.Data.EnemyId}] entrando em [OnPoolBehavior]");

                onPoolContext.Enemy.transform.position = onPoolContext.NewPosition;
                onPoolContext.Enemy.transform.parent   = onPoolContext.Parent;
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Repete o reposicionamento caso a estratégia de pool seja desfeita
        /// (ex: inimigo reativado e imediatamente devolvido).
        /// </summary>
        public Task UnexecuteBehavior(IEnemyStartegyContext context)
        {
            if (context is OnPoolBehaviorContext onPoolContext)
            {
                LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.AI,
                    $"Inimigo [{onPoolContext.Enemy.Data.EnemyId}] saindo de [OnPoolBehavior]");

                onPoolContext.Enemy.transform.position = onPoolContext.NewPosition;
                onPoolContext.Enemy.transform.parent   = onPoolContext.Parent;
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// OnPoolBehavior não possui loops assíncronos, portanto o cancelamento imediato é vazio.
        /// </summary>
        public void CancelImmediate() { }
    }
}
