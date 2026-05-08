using System;
using System.Threading;
using System.Threading.Tasks;
using Services.DebugUtilities;
using Services.DebugUtilities.Console;
using UnityEngine;
using UnityEngine.AI;

public class PatrolBehavior : IReverseableEnemyStartegy
{
    private CancellationTokenSource _cts;

    public async Task ExecuteBehavior(IEnemyStartegyContext context)
    {
        if (context is PatrolBehaviorContext patrolBehaviorContext)
        {
            LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.AI,
                $"Enemy [{patrolBehaviorContext.enemy.data.enemyId}], is entering [PatrolBehavior]");

            _cts = new CancellationTokenSource();
            await Patrol(patrolBehaviorContext, _cts);
        }
    }

    private async Task Patrol(PatrolBehaviorContext context, CancellationTokenSource cts)
    {
        try
        {
            while (!cts.IsCancellationRequested)
            {
                float sqDist =
                    (context.enemy.transform.position - context.target.position).sqrMagnitude;

                float sqPerception =
                    context.perceptionRadius * context.perceptionRadius;

                // Detect target immediately before selecting patrol point
                if (sqDist <= sqPerception)
                {
                    context.enemy.data.agent.ResetPath();

                    await ExplorationEnemyController.SetEnemyStartegy(
                        context.enemy,
                        new PursuingBehavior(),
                        new PursuingBehaviorContext(
                            context.enemy,
                            context.target,
                            context.perceptionRadius));

                    return;
                }

                if (TryGetRandomPoint(
                        context.enemy.transform.position,
                        context.enemy.data.patrolRadius,
                        out Vector3 targetPoint))
                {
                    LoggerService.PrintLogMessage(
                        LogLevel.Debug,
                        LogCategory.Data,
                        $"New random target pos {targetPoint} of {context.enemy.data.enemyId}");

                    context.enemy.data.agent.SetDestination(targetPoint);

                    // Wait until path calculation finishes
                    while (context.enemy.data.agent.pathPending)
                    {
                        cts.Token.ThrowIfCancellationRequested();
                        await Task.Yield();
                    }

                    // Move toward patrol point
                    while (!cts.IsCancellationRequested &&
                           context.enemy.data.agent.remainingDistance >
                           context.enemy.data.agent.stoppingDistance)
                    {
                        // Re-check target perception continuously
                        sqDist =
                            (context.enemy.transform.position - context.target.position).sqrMagnitude;

                        sqPerception =
                            context.perceptionRadius * context.perceptionRadius;

                        if (sqDist <= sqPerception)
                        {
                            context.enemy.data.agent.ResetPath();

                            await ExplorationEnemyController.SetEnemyStartegy(
                                context.enemy,
                                new PursuingBehavior(),
                                new PursuingBehaviorContext(
                                    context.enemy,
                                    context.target,
                                    context.perceptionRadius));

                            return;
                        }

                        await Task.Delay(5, cts.Token);
                    }

                    // Small idle delay after reaching patrol point
                    await Task.Delay(100, cts.Token);
                }
                else
                {
                    // Retry if no valid NavMesh point found
                    await Task.Delay(25, cts.Token);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected cancellation
        }
    }

    private bool TryGetRandomPoint(Vector3 center, float range, out Vector3 result)
    {
        Vector3 randomPoint = center + UnityEngine.Random.insideUnitSphere * range;
        if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 1.0f, NavMesh.AllAreas))
        {
            result = hit.position;
            return true;
        }
        result = Vector3.zero;
        return false;
    }

    public async Task UnexecuteBehavior(IEnemyStartegyContext context)
    {
        if (context is PatrolBehaviorContext patrolBehaviorContext)
        {
            LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.AI,
                $"Enemy [{patrolBehaviorContext.enemy.data.enemyId}], is exiting [PatrolBehavior]");

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        await Task.CompletedTask;
    }

    public void CancelImmediate()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }
}
