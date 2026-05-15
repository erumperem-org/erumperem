using System.Threading;
using System.Threading.Tasks;
using Services.DebugUtilities;
using Services.DebugUtilities.Console;
using Services.Navigation;
using UnityEngine;
using System;

namespace Core.Exploration.Character.Movement
{
    public sealed class GoToPointBehavior : IReverseableCharacterMovementStartegy
    {
        private const int PollingDelayMs = 100;
        private CancellationTokenSource _cts;
        private INavMeshService _nav;

        public async Task ExecuteBehavior(ICharacterMovementStartegyContext context)
        {
            if (context is not GoToPointBehaviorContext ctx) return;

            LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.AI,
                $"Character [{ctx.characterData.name}] is entering [GoToPointBehavior]");

            _nav = ctx.navMeshService ?? ctx.self.GetComponent<INavMeshService>();

            if (_nav == null)
            {
                LoggerService.PrintLogMessage(LogLevel.Error, LogCategory.AI,
                    $"Character [{ctx.characterData.name}] has no INavMeshService.");
                return;
            }

            if (!_nav.ValidateDestination(ctx.destination))
            {
                LoggerService.PrintLogMessage(LogLevel.Warning, LogCategory.AI,
                    $"Character [{ctx.characterData.name}]: destination {ctx.destination} is off NavMesh, aborting.");
                return;
            }

            _cts = new CancellationTokenSource();

            try
            {
                _nav.MoveTo(ctx.destination);
                await WaitForPathAsync(_cts.Token);
                await WaitUntilArrivedAsync(_cts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            // Chegou — encadeia o próximo behavior
            if (ctx.nextBehavior != null && ctx.nextBehaviorContext != null)
            {
                LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.AI,
                    $"Character [{ctx.characterData.name}]: reached destination, switching behavior.");
                await ctx.nextBehavior.ExecuteBehavior(ctx.nextBehaviorContext);
            }
        }

        public async Task UnexecuteBehavior(ICharacterMovementStartegyContext context)
        {
            if (context is GoToPointBehaviorContext ctx)
                LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.AI,
                    $"Character [{ctx.characterData.name}] is exiting [GoToPointBehavior]");

            CancelImmediate();
            await Task.CompletedTask;
        }

        public void CancelImmediate()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private async Task WaitForPathAsync(CancellationToken ct)
        {
            while (_nav.IsPending() && !ct.IsCancellationRequested)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Yield();
            }
        }

        private async Task WaitUntilArrivedAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                ct.ThrowIfCancellationRequested();
                if (_nav.HasReachedDestination()) return;
                await Task.Delay(PollingDelayMs, ct);
            }
        }
    }

    public sealed class GoToPointBehaviorContext : ICharacterMovementStartegyContext
    {
        public CharacterData characterData;
        public Vector3 destination;
        public Transform self;
        public NavMeshService navMeshService;
    
        public IReverseableCharacterMovementStartegy nextBehavior;
        public ICharacterMovementStartegyContext nextBehaviorContext;

        public GoToPointBehaviorContext(
            CharacterData characterData,
            Vector3 destination,
            Transform self,
            NavMeshService navMeshService,
            IReverseableCharacterMovementStartegy nextBehavior,
            ICharacterMovementStartegyContext nextBehaviorContext)
        {
            this.characterData = characterData;
            this.destination = destination;
            this.self = self;
            this.navMeshService = navMeshService;
            this.nextBehavior = nextBehavior;
            this.nextBehaviorContext = nextBehaviorContext;
        }
    }
}