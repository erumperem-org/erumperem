using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Services.DebugUtilities;
using Services.DebugUtilities.Console;
using System;

namespace Core.Exploration.Enemies
{
    public class ExplorationEnemyWandering : IExplorationEnemyStrategy
    {
        public ExplorationEnemyStates state => ExplorationEnemyStates.Wandering;
        private Coroutine wanderRoutine;
        private ExplorationEnemyWanderingData data;

        public ExplorationEnemyWandering(ExplorationEnemyWanderingData data)
        {
            this.data = data;
        }

        public void EnterBehavior()
        {
            LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.Lifecycle, "Enemy wandering strategy called");
            if (wanderRoutine != null)
            {
                data.enemy.StopCoroutine(wanderRoutine);
                wanderRoutine = null;
            }
            wanderRoutine = data.enemy.StartCoroutine(WanderLoop());
        }

        public void ExitBehavior()
        {
            LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.Lifecycle, "Enemy wandering strategy exit");
            Stopwandering();
        }

        private IEnumerator WanderLoop()
        {
            data.agent.isStopped = false;

            int iteration = 0;

            float distance = Vector3.Distance(data.enemy.transform.position, data.target.transform.position);
            if(distance < data.chaseRadius)
            {
                data.enemy.StateChange(ExplorationEnemyStates.Chasing);
            }
            while (true)
            {
                iteration++;

                if (TryGetRandomDestination(out Vector3 destination))
                {
                    data.agent.isStopped = false;
                    bool pathSet = data.agent.SetDestination(destination);
                    if (!pathSet)
                    {
                        LoggerService.PrintLogMessage(LogLevel.Warning, LogCategory.Lifecycle,
                            "[wandering] SetDestination returned FALSE — skipping this destination");
                    }
                }
                else
                {
                    LoggerService.PrintLogMessage(LogLevel.Warning, LogCategory.Lifecycle,
                        $"[wandering] Failed to find valid NavMesh destination after {data.navMeshSampleAttempts} attempts — skipping");
                }
            
                yield return new WaitForSeconds(data.repathRate);
            }
        }
        private void Stopwandering()
        {
            if (wanderRoutine != null)
            {
                data.enemy.StopCoroutine(wanderRoutine);
                wanderRoutine = null;
            }
            data.agent.isStopped = true;
            data.agent.ResetPath();
        }

        private bool TryGetRandomDestination(out Vector3 result)
        {
            Vector3 origin = data.enemy.transform.position;

            for (int i = 0; i < data.navMeshSampleAttempts; i++)
            {
                Vector3 randomOffset = new Vector3(
                    UnityEngine.Random.Range(-data.wanderRangeX, data.wanderRangeX),
                    0f,
                    UnityEngine.Random.Range(-data.wanderRangeZ, data.wanderRangeZ)
                );

                Vector3 candidate = origin + randomOffset;

                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, data.navMeshSampleRadius, NavMesh.AllAreas))
                {
                    result = hit.position;
                    return true;
                }
            }

            result = Vector3.zero;
            return false;
        }
    }

    [Serializable]
    public struct ExplorationEnemyWanderingData
    {
        public ExplorationEnemyController enemy;
        public NavMeshAgent agent;
        public float repathRate;
        public float wanderRangeX;
        public float wanderRangeZ;
        public int navMeshSampleAttempts;
        public float navMeshSampleRadius;
        public float chaseRadius;
        public GameObject target;

        public ExplorationEnemyWanderingData(
            ExplorationEnemyController enemy,
            NavMeshAgent agent,
            float repathRate,
            float wanderRangeX,
            float wanderRangeZ,
            int navMeshSampleAttempts,
            float navMeshSampleRadius,
            float chaseRadius,
            GameObject target)
        {
            this.enemy = enemy;
            this.agent = agent;
            this.repathRate = repathRate;
            this.wanderRangeX = wanderRangeX;
            this.wanderRangeZ = wanderRangeZ;
            this.navMeshSampleAttempts = navMeshSampleAttempts;
            this.navMeshSampleRadius = navMeshSampleRadius;
            this.chaseRadius = chaseRadius;
            this.target = target;
        }
    }
}
