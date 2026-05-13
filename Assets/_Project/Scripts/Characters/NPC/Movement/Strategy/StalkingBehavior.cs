using System;
using System.Threading;
using System.Threading.Tasks;
using Services.DebugUtilities;
using Services.DebugUtilities.Console;
using Services.Navigation;
using UnityEngine;

namespace Core.Exploration.Character.Movement
{
    public sealed class StalkingBehavior : IReverseableCharacterMovementStartegy
    {
        private const float TargetMovedThresholdSq = 0.09f;
        private const int OuterLoopDelayMs = 100;
        private const int InnerLoopDelayMs = 50;
        private CancellationTokenSource _cts;
        private INavMeshService _nav;

        public async Task ExecuteBehavior(ICharacterMovementStartegyContext context)
        {
            if (context is not StalkingBehaviorContext stalkingContext) return;

            LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.AI, $"Character [{stalkingContext.characterData.name}] is entering [StalkingBehavior]");

            _nav = ResolveNavService(stalkingContext);

            if (_nav == null)
            {
                LoggerService.PrintLogMessage(LogLevel.Error, LogCategory.AI, $"Character [{stalkingContext.characterData.name}] has no INavMeshService. " + "Add NavMeshService to the enemy GameObject or inject it via context.");
                return;
            }

            _cts = new CancellationTokenSource();
            await StalkAsync(stalkingContext, _cts.Token);
        }

        public async Task UnexecuteBehavior(ICharacterMovementStartegyContext context)
        {
            if (context is StalkingBehaviorContext stalkingContext)
            {
                LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.AI, $"Character [{stalkingContext.characterData.name}] is exiting [StalkingBehavior]");
            }

            CancelImmediate();
            await Task.CompletedTask;
        }

        public void CancelImmediate()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private async Task StalkAsync(StalkingBehaviorContext context, CancellationToken ct)
        {
            try
            {
                float stalkDistSq = context.stalkingDistance * context.stalkingDistance;

                while (!ct.IsCancellationRequested)
                {
                    ct.ThrowIfCancellationRequested();
                    Vector3 targetPos = context.target.position;
                    Vector3 stalkerPos = context.self.transform.position;
                    Vector3 toEnemy = stalkerPos - targetPos;
                    float distanceSq = toEnemy.sqrMagnitude;

                    if (distanceSq <= stalkDistSq)
                    {
                        _nav.Stop();
                        await Task.Delay(OuterLoopDelayMs, ct);
                        continue;
                    }

                    Vector3 desiredPosition = ComputeStalkPosition(stalkerPos, targetPos, context.stalkingDistance);

                    if (!_nav.ValidateDestination(desiredPosition))
                    {
                        LoggerService.PrintLogMessage(LogLevel.Warning, LogCategory.AI,
                            $"Character [{context.characterData.name}]: stalk position {desiredPosition} is off NavMesh, skipping.");
                        await Task.Delay(OuterLoopDelayMs, ct);
                        continue;
                    }

                    if (Vector3.Distance(context.target.position, context.self.position) > 2)
                    {
                        LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.Data, $"Character [{context.characterData.name}]: new stalk position {desiredPosition}");
                    }

                    _nav.MoveTo(desiredPosition);
                    await WaitForPathAsync(ct);
                    await MonitorNavigationAsync(context, stalkDistSq, ct);
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _nav?.Stop();
            }
        }

        private async Task MonitorNavigationAsync(StalkingBehaviorContext context, float stalkDistSq, CancellationToken ct)
        {
            Vector3 lastKnownTargetPosition = context.target.position;

            while (!ct.IsCancellationRequested)
            {
                ct.ThrowIfCancellationRequested();
                Vector3 targetPos = context.target.position;
                Vector3 stalkerPos = context.self.position;
                Vector3 toEnemy = stalkerPos - targetPos;
                float distanceSq = toEnemy.sqrMagnitude;

                if (distanceSq <= stalkDistSq)
                {
                    _nav.Stop();
                    return;
                }

                bool targetMoved = (lastKnownTargetPosition - targetPos).sqrMagnitude > TargetMovedThresholdSq;
                if (targetMoved) return;
                if (_nav.HasReachedDestination()) return;

                await Task.Delay(InnerLoopDelayMs, ct);
            }
        }

        private async Task WaitForPathAsync(CancellationToken ct)
        {
            while (_nav.IsPending() && !ct.IsCancellationRequested)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Yield();
            }
        }

        private static Vector3 ComputeStalkPosition(Vector3 stalkerPos, Vector3 targetPos, float stalkingDistance)
        {
            Vector3 toEnemy = stalkerPos - targetPos;
            Vector3 direction = toEnemy.sqrMagnitude > 0.001f ? toEnemy.normalized : Vector3.back;
            return targetPos + direction * stalkingDistance;
        }

        private static INavMeshService ResolveNavService(StalkingBehaviorContext context)
        {
            if (context.navMeshService != null)
                return context.navMeshService;

            return context.self.GetComponent<INavMeshService>();
        }
    }

    public class StalkingBehaviorContext : ICharacterMovementStartegyContext
    {
        public CharacterData characterData;
        public float stalkingDistance;
        public Transform target;
        public Transform self;
        public NavMeshService navMeshService;
        public StalkingBehaviorContext(CharacterData characterData, float stalkingDistance, Transform target, Transform self, NavMeshService navMeshService)
        {
            this.characterData = characterData;
            this.stalkingDistance = stalkingDistance;
            this.target = target;
            this.self = self;
            this.navMeshService = navMeshService;
        }
    }
}