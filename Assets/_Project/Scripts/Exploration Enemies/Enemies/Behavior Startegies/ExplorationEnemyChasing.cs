using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Services.DebugUtilities;
using Services.DebugUtilities.Console;
using System;
using UnityEngine.SceneManagement;

namespace Core.Exploration.Enemies
{
    public class ExplorationEnemyChasing : IExplorationEnemyStrategy
    {
        public ExplorationEnemyStates state => ExplorationEnemyStates.Chasing;
        private Coroutine chaseRoutine;
        private ExplorationEnemyChasingData data;
        public ExplorationEnemyChasing(ExplorationEnemyChasingData data)
        {
            this.data = data;
        }

        public void EnterBehavior()
        {
            LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.Lifecycle, "Enemy Chasing strategy called");

            chaseRoutine = data.enemy.StartCoroutine(ChaseLoop());
        }

        public void ExitBehavior()
        {
            LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.Lifecycle, "Enemy Chasing strategy exit");
            StopBehavior();
        }
        private void StopBehavior()
        {
            LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.Lifecycle,
                $"[Chasing] StopBehavior called — chaseRoutine: {(chaseRoutine != null ? "running" : "null")}");

            if (chaseRoutine != null)
            {
                data.enemy.StopCoroutine(chaseRoutine);
                chaseRoutine = null;
            }

            data.agent.isStopped = true;
            data.agent.ResetPath();

            LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.Lifecycle,
                "[Chasing] StopBehavior complete — agent stopped and path reset");
        }

        private IEnumerator ChaseLoop()
        {
            data.agent.isStopped = false;

            int iteration = 0;

            while (true)
            {
                iteration++;

                if (!IsTargetValid())
                {
                    yield break;
                }

                float distance = Vector3.Distance(data.enemy.transform.position, data.target.transform.position);
                if (distance > data.chaseRadius)
                {
                    data.enemy.StateChange(ExplorationEnemyStates.Wandering);
                    yield break;
                }

                if (distance > data.agent.stoppingDistance)
                {
                    data.agent.isStopped = false;
                    bool pathSet = data.agent.SetDestination(data.target.transform.position);
                    if (!pathSet)
                    {
                        LoggerService.PrintLogMessage(LogLevel.Error, LogCategory.Lifecycle,
                            "[Chasing] SetDestination returned FALSE — target may be unreachable or off NavMesh!");
                    }
                }
                else
                {
                    data.agent.isStopped = true;
                    SceneManager.LoadSceneAsync(1);
                }

                yield return new WaitForSeconds(data.repathRate);
            }
        }

        private bool IsTargetValid() => data.target != null && data.target.activeInHierarchy;

    }

    [Serializable]
    public struct ExplorationEnemyChasingData
    {
        public ExplorationEnemyController enemy;
        public NavMeshAgent agent;
        public GameObject target;
        public float repathRate;
        public float chaseRadius;

        public ExplorationEnemyChasingData(
            ExplorationEnemyController enemy,
            NavMeshAgent agent,
            GameObject target,
            float repathRate,
            float chaseRadius)
        {
            this.enemy = enemy;
            this.agent = agent;
            this.target = target;
            this.repathRate = repathRate;
            this.chaseRadius = chaseRadius;
        }
    }
}