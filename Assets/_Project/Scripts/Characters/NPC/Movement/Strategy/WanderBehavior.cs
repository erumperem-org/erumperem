using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.Exploration.Character.Movement;
using Services.DebugUtilities;
using Services.DebugUtilities.Console;
using Services.Navigation;
using UnityEngine;

namespace Core.Exploration.Character.Movement
{
    /// <summary>
    /// Comportamento de patrulha/wander baseado em waypoints.
    ///
    /// REVISÃO:
    /// - Removido Dispose prematuro do CTS
    /// - Guards contra objetos destruídos
    /// - Removido GetComponent por interface
    /// - Loops async protegidos
    /// - Encerramento seguro no Editor/PlayMode
    /// - Tratamento de exceções robusto
    /// </summary>
    public sealed class WanderBehavior : IReverseableCharacterMovementStartegy
    {
        // ─────────────────────────────────────────────────────────────
        // Constantes
        // ─────────────────────────────────────────────────────────────

        private const int PollingDelayMs = 100;

        // ─────────────────────────────────────────────────────────────
        // Estado interno
        // ─────────────────────────────────────────────────────────────

        private CancellationTokenSource _cts;
        private NavMeshService _nav;

        // Ping-pong real
        private int _direction = 1;

        // ─────────────────────────────────────────────────────────────
        // API pública
        // ─────────────────────────────────────────────────────────────

        public async Task ExecuteBehavior(ICharacterMovementStartegyContext context)
        {
            if (context is not WanderBehaviorContext ctx)
                return;

            LoggerService.PrintLogMessage(
                LogLevel.Debug,
                LogCategory.AI,
                $"Character [{ctx.characterData.name}] entering [WanderBehavior]");

            CancelImmediate();

            // IMPORTANTE:
            // Nunca usar GetComponent<Interface>() aqui
            _nav = ctx.navMeshService != null
                ? ctx.navMeshService
                : ctx.self.GetComponent<NavMeshService>();

            if (_nav == null)
            {
                LoggerService.PrintLogMessage(
                    LogLevel.Error,
                    LogCategory.AI,
                    $"Character [{ctx.characterData.name}] has no NavMeshService.");

                return;
            }

            if (ctx.waypoints == null || ctx.waypoints.Count == 0)
            {
                LoggerService.PrintLogMessage(
                    LogLevel.Warning,
                    LogCategory.AI,
                    $"Character [{ctx.characterData.name}] has no waypoints.");

                return;
            }

            _cts = new CancellationTokenSource();

            try
            {
                await WanderAsync(ctx, _cts.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        public async Task UnexecuteBehavior(ICharacterMovementStartegyContext context)
        {
            if (context is WanderBehaviorContext ctx)
            {
                LoggerService.PrintLogMessage(
                    LogLevel.Debug,
                    LogCategory.AI,
                    $"Character [{ctx.characterData.name}] exiting [WanderBehavior]");
            }

            CancelImmediate();

            await Task.CompletedTask;
        }

        public void CancelImmediate()
        {
            if (_cts == null)
                return;

            try
            {
                _cts.Cancel();
            }
            catch
            {
                // ignorado
            }

            _cts = null;

            if (_nav != null)
            {
                try
                {
                    _nav.Stop();
                }
                catch
                {
                    // NavMeshAgent pode já ter sido destruído
                }
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Loop principal
        // ─────────────────────────────────────────────────────────────

        private async Task WanderAsync(
            WanderBehaviorContext ctx,
            CancellationToken ct)
        {
            int index = ctx.startAtClosest
                ? GetClosestWaypointIndex(ctx)
                : 0;

            while (!ct.IsCancellationRequested)
            {
                ct.ThrowIfCancellationRequested();

                // Objeto destruído
                if (ctx.self == null)
                    return;

                if (_nav == null)
                    return;

                if (!_nav.IsOnNavMesh())
                {
                    await Task.Delay(PollingDelayMs, ct);
                    continue;
                }

                Vector3 waypoint = ctx.waypoints[index];

                // waypoint inválido
                if (!_nav.ValidateDestination(waypoint))
                {
                    LoggerService.PrintLogMessage(
                        LogLevel.Warning,
                        LogCategory.AI,
                        $"Invalid waypoint [{index}]");

                    index = NextIndex(index, ctx);
                    continue;
                }

                LoggerService.PrintLogMessage(
                    LogLevel.Debug,
                    LogCategory.AI,
                    $"Moving to waypoint [{index}]");

                bool moveStarted = false;

                try
                {
                    moveStarted = _nav.MoveTo(waypoint);
                }
                catch
                {
                    return;
                }

                if (!moveStarted)
                {
                    await Task.Delay(PollingDelayMs, ct);
                    continue;
                }

                // Espera path calcular
                await WaitForPathAsync(ct);

                // Espera chegada
                await WaitUntilArrivedAsync(ct);

                // Pausa opcional
                if (ctx.waitAtPointMs > 0)
                {
                    await Task.Delay(ctx.waitAtPointMs, ct);
                }

                index = NextIndex(index, ctx);
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Esperas async
        // ─────────────────────────────────────────────────────────────

        private async Task WaitForPathAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                ct.ThrowIfCancellationRequested();

                if (_nav == null)
                    return;

                if (!_nav.IsPending())
                    return;

                await Task.Delay(1, ct);
            }
        }

        private async Task WaitUntilArrivedAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                ct.ThrowIfCancellationRequested();

                if (_nav == null)
                    return;

                if (_nav.HasReachedDestination())
                    return;

                await Task.Delay(PollingDelayMs, ct);
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────

        private int GetClosestWaypointIndex(WanderBehaviorContext ctx)
        {
            if (ctx.self == null)
                return 0;

            Vector3 selfPos = ctx.self.position;

            int closest = 0;
            float minDist = float.MaxValue;

            for (int i = 0; i < ctx.waypoints.Count; i++)
            {
                float dist =
                    (ctx.waypoints[i] - selfPos).sqrMagnitude;

                if (dist < minDist)
                {
                    minDist = dist;
                    closest = i;
                }
            }

            return closest;
        }

        private int NextIndex(int current, WanderBehaviorContext ctx)
        {
            int total = ctx.waypoints.Count;

            if (total <= 1)
                return 0;

            // Loop simples
            if (!ctx.reverseLoop)
            {
                return (current + 1) % total;
            }

            // Ping-pong real
            if (current >= total - 1)
                _direction = -1;
            else if (current <= 0)
                _direction = 1;

            return current + _direction;
        }
    }

    // ═════════════════════════════════════════════════════════════════
    // Context
    // ═════════════════════════════════════════════════════════════════

    public sealed class WanderBehaviorContext
        : ICharacterMovementStartegyContext
    {
        public CharacterData characterData;
        public List<Vector3> waypoints;
        public Transform self;
        public NavMeshService navMeshService;

        /// <summary>
        /// Começa pelo waypoint mais próximo.
        /// </summary>
        public bool startAtClosest;

        /// <summary>
        /// Loop ping-pong.
        /// </summary>
        public bool reverseLoop;

        /// <summary>
        /// Espera ao chegar no ponto.
        /// </summary>
        public int waitAtPointMs;

        public WanderBehaviorContext(
            CharacterData characterData,
            List<Vector3> waypoints,
            Transform self,
            NavMeshService navMeshService,
            bool startAtClosest = true,
            bool reverseLoop = false,
            int waitAtPointMs = 500)
        {
            this.characterData   = characterData;
            this.waypoints       = waypoints;
            this.self            = self;
            this.navMeshService  = navMeshService;
            this.startAtClosest  = startAtClosest;
            this.reverseLoop     = reverseLoop;
            this.waitAtPointMs   = waitAtPointMs;
        }
    }
}