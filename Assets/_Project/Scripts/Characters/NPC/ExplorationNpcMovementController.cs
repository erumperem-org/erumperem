using System.Threading;
using System.Threading.Tasks;
using Core.Exploration.Character.Movement;
using Services.DebugUtilities;
using Services.DebugUtilities.Console;
using UnityEngine;
using UnityEngine.AI;

namespace Core.Exploration.Character.NPC
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class ExplorationNpcMovementController : MonoBehaviour
    {
        public ExplorationNpcMovementData data = new ExplorationNpcMovementData();
        private readonly SemaphoreSlim _strategySemaphore = new SemaphoreSlim(1, 1);
        public static async Task SetNpcMovementStartegy(ExplorationNpcMovementController controller, ICharacterMovementStartegy newStrategy, ICharacterMovementStartegyContext newContext)
        {
            await controller._strategySemaphore.WaitAsync();
            try
            {
                if (controller.data.movementData.ActiveStrategy == null)
                {
                    LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.AI, $"Definindo estratégia inicial [{newStrategy}] em [{controller.data.name}]");
                }
                else
                {
                    LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.AI, $"Substituindo [{controller.data.movementData.ActiveStrategy}] por [{newStrategy}] em [{controller.data.name}]");
                    if (controller.data.movementData.ActiveStrategy is IReverseableCharacterMovementStartegy reverseable)
                    {
                        await reverseable.UnexecuteBehavior(controller.data.movementData.CurrentContext);
                    }
                }

                controller.data.movementData.ActiveStrategy      = newStrategy;
                controller.data.movementData.EnemyStartegyExposed = newStrategy.GetType().Name.ToString();
                controller.data.movementData.CurrentContext       = newContext;
                _ = controller.data.movementData.ActiveStrategy.ExecuteBehavior(newContext);
            }
            finally
            {
                controller._strategySemaphore.Release();
            }
        }

        private void OnDestroy()
        {
            if (data.movementData.ActiveStrategy is IReverseableCharacterMovementStartegy reverseable)
            {
                reverseable.CancelImmediate();
            }
        }
    }
}
