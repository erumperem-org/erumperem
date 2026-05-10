using System.Threading;
using System.Threading.Tasks;
using Services.DebugUtilities;
using Services.DebugUtilities.Console;
using UnityEngine;
using UnityEngine.AI;

namespace Core.Exploration.Enemy
{
    /// <summary>
    /// MonoBehaviour central do inimigo de exploração.
    /// Expõe métodos estáticos thread-safe para trocar de estratégia e de nível,
    /// e garante cancelamento seguro da estratégia ativa ao ser destruído.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class ExplorationEnemyController : MonoBehaviour
    {
        /// <summary>Todos os dados do inimigo (navegação, estratégia, configuração).</summary>
        public ExplorationEnemyData Data = new ExplorationEnemyData();

        // SemaphoreSlim com 1 slot garante que apenas uma troca de estratégia
        // ocorra por vez, mesmo que chamadas assíncronas cheguem em paralelo.
        private readonly SemaphoreSlim _strategySemaphore = new SemaphoreSlim(1, 1);

        // ── API pública estática ──────────────────────────────────────────────────

        /// <summary>
        /// Troca a estratégia de comportamento do inimigo de forma thread-safe.
        /// Se o inimigo já possuía uma estratégia reversível, <see cref="IReverseableEnemyStartegy.UnexecuteBehavior"/>
        /// é aguardado antes de iniciar a nova estratégia.
        /// </summary>
        /// <param name="controller">Inimigo que receberá a nova estratégia.</param>
        /// <param name="newStrategy">Nova estratégia a ser executada.</param>
        /// <param name="newContext">Contexto com os dados que a nova estratégia precisará.</param>
        public static async Task SetEnemyStartegy(
            ExplorationEnemyController controller,
            IEnemyStartegy newStrategy,
            IEnemyStartegyContext newContext)
        {
            await controller._strategySemaphore.WaitAsync();
            try
            {
                if (controller.Data.ActiveStrategy == null)
                {
                    LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.AI,
                        $"Definindo estratégia inicial [{newStrategy}] em [{controller.Data.EnemyId}]");
                }
                else
                {
                    LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.AI,
                        $"Substituindo [{controller.Data.ActiveStrategy}] por [{newStrategy}] em [{controller.Data.EnemyId}]");

                    // Se a estratégia atual souber se desfazer, aguarda a limpeza assíncrona
                    if (controller.Data.ActiveStrategy is IReverseableEnemyStartegy reverseable)
                    {
                        await reverseable.UnexecuteBehavior(controller.Data.CurrentContext);
                    }
                }

                // Atualiza o estado antes de disparar a nova execução
                controller.Data.ActiveStrategy      = newStrategy;
                controller.Data.EnemyStartegyExposed = newStrategy.GetType().ToString();
                controller.Data.CurrentContext       = newContext;

                // Fire-and-forget: a estratégia roda de forma assíncrona de forma independente
                _ = controller.Data.ActiveStrategy.ExecuteBehavior(newContext);
            }
            finally
            {
                // Libera o semáforo mesmo em caso de exceção
                controller._strategySemaphore.Release();
            }
        }

        /// <summary>
        /// Atualiza apenas o nível do inimigo sem alterar a estratégia ativa.
        /// Retorna uma Task completada para manter consistência de API assíncrona.
        /// </summary>
        public static Task SetEnemyLevel(
            ExplorationEnemyController controller,
            ExplorationEnemyLevels newLevel)
        {
            controller.Data.EnemyLevel = newLevel;
            return Task.CompletedTask;
        }

        // ── Ciclo de vida Unity ───────────────────────────────────────────────────

        private void OnDestroy()
        {
            // Ao destruir o GameObject, cancela imediatamente qualquer loop assíncrono ativo
            // para evitar erros de acesso a objetos destruídos.
            if (Data.ActiveStrategy is IReverseableEnemyStartegy reverseable)
            {
                reverseable.CancelImmediate();
            }
        }
    }
}
