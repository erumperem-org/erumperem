using System;
using Services.DebugUtilities.Canvas;
using Services.DebugUtilities.Console;
using UnityEngine;
using UnityEngine.AI;
namespace Core.Exploration.Enemies
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class ExplorationEnemyController : MonoBehaviour
    {
        private IExplorationEnemyStrategy enemyStategy;
        public ExplorationEnemyData data;

        public void StateChange(ExplorationEnemyStates newState)
        {
            if (data.explorationState != newState)
            {
                if (enemyStategy != null)
                {
                    enemyStategy.ExitBehavior();
                }

                switch (newState)
                {
                    case ExplorationEnemyStates.Wandering:
                        enemyStategy = new ExplorationEnemyWandering(data.wanderingData);
                        break;
                    case ExplorationEnemyStates.Chasing:
                        enemyStategy = new ExplorationEnemyChasing(data.chasingData);
                        break;
                    case ExplorationEnemyStates.OnPool:
                        enemyStategy = new ExplorationEnemyOnPool();
                        break;
                    case ExplorationEnemyStates.OnDestroy:
                        enemyStategy = new ExplorationEnemyOnDestroy();
                        break;
                    case ExplorationEnemyStates.OnCreate:
                        enemyStategy = new ExplorationEnemyOnCreate();
                        break;
                    default:
                        LoggerService.PrintLogMessage(
                            LogLevel.Debug,
                            LogCategory.Lifecycle,
                            $"Exception found on Startegy selection on {this.GetType()}");
                        break;
                }
                enemyStategy.UpdateExplorationState(ref data.explorationState);
                enemyStategy.EnterBehavior();
            }
        }

        public void InitializeState()
        {
            if (Vector3.Distance(this.transform.position, data.chasingData.target.transform.position) < data.chasingData.chaseRadius)
            {
                StateChange(ExplorationEnemyStates.Chasing);
                return;
            }
            StateChange(ExplorationEnemyStates.Wandering);
        }
    }

    [Serializable]
    public struct ExplorationEnemyData
    {
        public ExplorationEnemiesLevel enemyLevel;
        public ExplorationEnemyStates explorationState;
        public ExplorationEnemyChasingData chasingData;
        public ExplorationEnemyWanderingData wanderingData;
        public ExplorationEnemyOnPoolData onPoolData;
        public ExplorationEnemyOnDestroyData onDestroyData;
        public ExplorationEnemyOnCreateData onCreateData;

        public ExplorationEnemyData(
            ExplorationEnemiesLevel enemyLevel,
            ExplorationEnemyStates explorationState,
            ExplorationEnemyChasingData chasingData,
            ExplorationEnemyWanderingData wanderingData,
            ExplorationEnemyOnPoolData onPoolData,
            ExplorationEnemyOnDestroyData onDestroyData,
            ExplorationEnemyOnCreateData onCreateData)
        {
            this.enemyLevel = enemyLevel;
            this.explorationState = explorationState;
            this.chasingData = chasingData;
            this.wanderingData = wanderingData;
            this.onPoolData = onPoolData;
            this.onDestroyData = onDestroyData;
            this.onCreateData = onCreateData;
        }
    }
}