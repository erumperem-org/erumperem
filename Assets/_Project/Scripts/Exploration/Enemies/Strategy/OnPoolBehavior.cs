using System.Threading.Tasks;
using Services.DebugUtilities;
using Services.DebugUtilities.Console;

public class OnPoolBehavior : IReverseableEnemyStartegy
{
    public Task ExecuteBehavior(IEnemyStartegyContext context)
    {
        if (context is OnPoolBehaviorContext onPoolBehaviorContext)
        {
            LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.AI,
                $"Enemy [{onPoolBehaviorContext.enemy.data.enemyId}], is entering [OnPoolBehavior]");

            onPoolBehaviorContext.enemy.transform.position = onPoolBehaviorContext.newPosition;
            onPoolBehaviorContext.enemy.transform.parent = onPoolBehaviorContext.parent;
        }

        return Task.CompletedTask;
    }

    public Task UnexecuteBehavior(IEnemyStartegyContext context)
    {
        if (context is OnPoolBehaviorContext onPoolBehaviorContext)
        {
            LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.AI,
                $"Enemy [{onPoolBehaviorContext.enemy.data.enemyId}], is exiting [OnPoolBehavior]");

            onPoolBehaviorContext.enemy.transform.position = onPoolBehaviorContext.newPosition;
            onPoolBehaviorContext.enemy.transform.parent = onPoolBehaviorContext.parent;
        }

        return Task.CompletedTask;
    }

    public void CancelImmediate() { }
}
