using System;
using System.Threading;
using System.Threading.Tasks;
using Services.DebugUtilities;
using Services.DebugUtilities.Console;
using UnityEngine;

public class StalkingBehavior : IReverseableEnemyStartegy
{
    private CancellationTokenSource _cts;

    public async Task ExecuteBehavior(IEnemyStartegyContext context)
    {
        if (context is StalkingBehaviorContext stalkingBehaviorContext)
        {
            LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.AI,
                $"Enemy [{stalkingBehaviorContext.enemy.data.enemyId}], is entering [StalkingBehavior]");

            _cts = new CancellationTokenSource();
            await Stalk(stalkingBehaviorContext, _cts);
        }
    }

    private async Task Stalk(StalkingBehaviorContext context, CancellationTokenSource cts)
    {
        try
        {
            Vector3 lastTargetPosition = context.target.position;

            while (!cts.IsCancellationRequested)
            {
                Vector3 targetPos = context.target.position;

                Vector3 toTarget =
                    context.enemy.transform.position - targetPos;

                float distanceSq = toTarget.sqrMagnitude;

                float stalkSq =
                    context.stalkingDistance * context.stalkingDistance;

                // Too far from target -> move closer
                if (distanceSq > stalkSq)
                {
                    // Update only if target moved enough
                    if ((lastTargetPosition - targetPos).sqrMagnitude > 0.01f)
                    {
                        lastTargetPosition = targetPos;

                        Vector3 direction = toTarget.normalized;

                        Vector3 desiredPosition =
                            targetPos + direction * context.stalkingDistance;

                        LoggerService.PrintLogMessage(
                            LogLevel.Debug,
                            LogCategory.Data,
                            $"New stalk position {desiredPosition} of {context.enemy.data.enemyId}");

                        context.enemy.data.agent.SetDestination(desiredPosition);

                        // Wait for path calculation
                        while (context.enemy.data.agent.pathPending)
                        {
                            cts.Token.ThrowIfCancellationRequested();
                            await Task.Yield();
                        }

                        // Continuously monitor movement while stalking
                        while (!cts.IsCancellationRequested &&
                               context.enemy.data.agent.remainingDistance >
                               context.enemy.data.agent.stoppingDistance)
                        {
                            targetPos = context.target.position;

                            toTarget =
                                context.enemy.transform.position - targetPos;

                            distanceSq = toTarget.sqrMagnitude;

                            // Stop immediately if already inside stalking range
                            if (distanceSq <= stalkSq)
                            {
                                context.enemy.data.agent.ResetPath();
                                break;
                            }

                            // Recalculate stalking position if target moved
                            if ((lastTargetPosition - targetPos).sqrMagnitude > 0.01f)
                            {
                                lastTargetPosition = targetPos;

                                direction = toTarget.normalized;

                                desiredPosition =
                                    targetPos + direction * context.stalkingDistance;

                                context.enemy.data.agent.SetDestination(desiredPosition);
                            }

                            await Task.Delay(50, cts.Token);
                        }
                    }
                }
                else
                {
                    // Already inside stalking distance
                    context.enemy.data.agent.ResetPath();
                }

                await Task.Delay(100, cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected cancellation
        }
    }

    public async Task UnexecuteBehavior(IEnemyStartegyContext context)
    {
        if (context is StalkingBehaviorContext stalkingBehaviorContext)
        {
            LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.AI,
                $"Enemy [{stalkingBehaviorContext.enemy.data.enemyId}], is exiting [StalkingBehavior]");

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
