using System;
using System.Threading;
using System.Threading.Tasks;
using Services.DebugUtilities;
using Services.DebugUtilities.Console;
using UnityEngine;
using UnityEngine.AI;

namespace Core.Exploration.Enemy
{
    /// <summary>
    /// Comportamento de patrulha: o inimigo escolhe pontos aleatórios dentro do seu
    /// <see cref="ExplorationEnemyData.PatrolRadius"/> e navega até eles em loop.
    /// Ao detectar o alvo dentro de <see cref="PatrolBehaviorContext.PerceptionRadius"/>,
    /// transiciona automaticamente para <see cref="PursuingBehavior"/>.
    /// </summary>
    public class PatrolBehavior : IReverseableEnemyStartegy
    {
        private CancellationTokenSource _cts;

        // ── IEnemyStartegy ────────────────────────────────────────────────────────

        public async Task ExecuteBehavior(IEnemyStartegyContext context)
        {
            if (context is PatrolBehaviorContext patrolContext)
            {
                LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.AI,
                    $"Inimigo [{patrolContext.Enemy.Data.EnemyId}] entrando em [PatrolBehavior]");

                _cts = new CancellationTokenSource();
                await Patrol(patrolContext, _cts);
            }
        }

        // ── IReverseableEnemyStartegy ─────────────────────────────────────────────

        public async Task UnexecuteBehavior(IEnemyStartegyContext context)
        {
            if (context is PatrolBehaviorContext patrolContext)
            {
                LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.AI,
                    $"Inimigo [{patrolContext.Enemy.Data.EnemyId}] saindo de [PatrolBehavior]");

                CancelAndDisposeCts();
            }

            await Task.CompletedTask;
        }

        public void CancelImmediate() => CancelAndDisposeCts();

        // ── Lógica interna ────────────────────────────────────────────────────────

        /// <summary>
        /// Loop principal de patrulha.
        /// A cada iteração: verifica percepção → escolhe ponto NavMesh → navega até ele.
        /// Se o alvo for detectado durante qualquer fase, sai e inicia a perseguição.
        /// </summary>
        private async Task Patrol(PatrolBehaviorContext context, CancellationTokenSource cts)
        {
            try
            {
                while (!cts.IsCancellationRequested)
                {
                    // Verifica percepção antes de escolher novo ponto de destino
                    if (IsTargetPerceived(context))
                    {
                        await TransitionToPursuit(context);
                        return;
                    }

                    if (TryGetRandomNavMeshPoint(
                            context.Enemy.transform.position,
                            context.Enemy.Data.PatrolRadius,
                            out Vector3 targetPoint))
                    {
                        LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.Data,
                            $"Novo ponto de patrulha {targetPoint} para [{context.Enemy.Data.EnemyId}]");

                        context.Enemy.Data.Agent.SetDestination(targetPoint);

                        // Aguarda o NavMesh calcular o caminho antes de começar a seguir
                        while (context.Enemy.Data.Agent.pathPending)
                        {
                            cts.Token.ThrowIfCancellationRequested();
                            await Task.Yield();
                        }

                        // Move em direção ao ponto verificando percepção continuamente
                        while (!cts.IsCancellationRequested &&
                               context.Enemy.Data.Agent.remainingDistance >
                               context.Enemy.Data.Agent.stoppingDistance)
                        {
                            if (IsTargetPerceived(context))
                            {
                                context.Enemy.Data.Agent.ResetPath();
                                await TransitionToPursuit(context);
                                return;
                            }

                            await Task.Delay(5, cts.Token);
                        }

                        // Pausa breve ao chegar ao destino antes de escolher o próximo ponto
                        await Task.Delay(100, cts.Token);
                    }
                    else
                    {
                        // Nenhum ponto válido encontrado no NavMesh; tenta novamente em breve
                        await Task.Delay(25, cts.Token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Cancelamento esperado ao trocar de estratégia ou destruir o objeto
            }
        }

        /// <summary>Retorna true se o alvo estiver dentro do raio de percepção.</summary>
        private bool IsTargetPerceived(PatrolBehaviorContext context)
        {
            float sqDist       = (context.Enemy.transform.position - context.Target.position).sqrMagnitude;
            float sqPerception = context.PerceptionRadius * context.PerceptionRadius;
            return sqDist <= sqPerception;
        }

        /// <summary>
        /// Reseta o path e solicita a transição para <see cref="PursuingBehavior"/>
        /// com o mesmo alvo e raio de percepção.
        /// </summary>
        private Task TransitionToPursuit(PatrolBehaviorContext context)
        {
            context.Enemy.Data.Agent.ResetPath();

            return ExplorationEnemyController.SetEnemyStartegy(
                context.Enemy,
                new PursuingBehavior(),
                new PursuingBehaviorContext(
                    context.Enemy,
                    context.Target,
                    context.PerceptionRadius));
        }

        /// <summary>
        /// Tenta obter um ponto aleatório válido no NavMesh dentro de <paramref name="range"/>
        /// a partir de <paramref name="center"/>.
        /// </summary>
        private bool TryGetRandomNavMeshPoint(Vector3 center, float range, out Vector3 result)
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

        private void CancelAndDisposeCts()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
    }
}
