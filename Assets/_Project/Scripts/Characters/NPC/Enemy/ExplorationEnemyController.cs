using System.Threading.Tasks;

namespace Core.Exploration.Character.NPC.Enemy
{
    public class ExplorationEnemyController : ExplorationNpcMovementController
    {
        public ExplorationEnemyData enemyData;

        public static Task SetEnemyLevel(ExplorationEnemyController controller, ExplorationEnemyLevels newLevel)
        {
            controller.enemyData.enemyLevel = newLevel;
            return Task.CompletedTask;
        }
    }
}
