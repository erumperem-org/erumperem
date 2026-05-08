using System;
using System.Threading;
using System.Threading.Tasks;
using Services.DebugUtilities;
using Services.DebugUtilities.Console;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PursuingBehavior : IReverseableEnemyStartegy
{
    private CancellationTokenSource _cts;

    public async Task ExecuteBehavior(IEnemyStartegyContext context)
    {
        if (context is PursuingBehaviorContext pursuingBehaviorContext)
        {
            LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.AI,
                $"Enemy [{pursuingBehaviorContext.enemy.data.enemyId}], is entering [PursuingBehavior]");

            _cts = new CancellationTokenSource();
            await Pursue(pursuingBehaviorContext, _cts);
        }
    }

    private async Task Pursue(PursuingBehaviorContext context, CancellationTokenSource cts)
    {
        try
        {
            while (!cts.IsCancellationRequested)
            {
                float sqDist =
                    (context.enemy.transform.position - context.target.position).sqrMagnitude;

                float sqPerception =
                    context.perceptionRadius * context.perceptionRadius;

                // Lost target -> return to patrol immediately
                if (sqDist > sqPerception)
                {
                    context.enemy.data.agent.ResetPath();

                    await ExplorationEnemyController.SetEnemyStartegy(
                        context.enemy,
                        new PatrolBehavior(),
                        new PatrolBehaviorContext(
                            context.enemy,
                            context.target,
                            context.perceptionRadius));

                    return;
                }

                float stoppingDistance = context.enemy.data.agent.stoppingDistance;
                float sqStopping = stoppingDistance * stoppingDistance;

                // Continue pursuing target
                if (sqDist > sqStopping)
                {
                    LoggerService.PrintLogMessage(
                        LogLevel.Debug,
                        LogCategory.Data,
                        $"New target pos {context.target.position} of {context.enemy.data.enemyId}");

                    context.enemy.data.agent.SetDestination(context.target.position);

                    // Wait for path calculation
                    while (context.enemy.data.agent.pathPending)
                    {
                        cts.Token.ThrowIfCancellationRequested();
                        await Task.Yield();
                    }

                    // Continuously update pursuit while moving
                    while (!cts.IsCancellationRequested &&
                           context.enemy.data.agent.remainingDistance >
                           context.enemy.data.agent.stoppingDistance)
                    {
                        sqDist =
                            (context.enemy.transform.position - context.target.position).sqrMagnitude;

                        sqPerception =
                            context.perceptionRadius * context.perceptionRadius;

                        // Target escaped perception radius
                        if (sqDist > sqPerception)
                        {
                            context.enemy.data.agent.ResetPath();

                            await ExplorationEnemyController.SetEnemyStartegy(
                                context.enemy,
                                new PatrolBehavior(),
                                new PatrolBehaviorContext(
                                    context.enemy,
                                    context.target,
                                    context.perceptionRadius));

                            return;
                        }

                        // Continuously refresh target position
                        context.enemy.data.agent.SetDestination(context.target.position);

                        await Task.Delay(50, cts.Token);
                    }

                    await Task.Delay(25, cts.Token);
                }
                else
                {
                    // Enemy reached target
                    context.enemy.data.agent.ResetPath();

                    LoggerService.PrintLogMessage(
                        LogLevel.Debug,
                        LogCategory.AI,
                        $"Enemy [{context.enemy.data.enemyId}] reached target. Loading combat scene.");

                    SceneManager.LoadScene("CombatScene");

                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected cancellation
        }
    }

    public async Task UnexecuteBehavior(IEnemyStartegyContext context)
    {
        if (context is PursuingBehaviorContext pursuingBehaviorContext)
        {
            LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.AI,
                $"Enemy [{pursuingBehaviorContext.enemy.data.enemyId}], is exiting [PursuingBehavior]");

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
