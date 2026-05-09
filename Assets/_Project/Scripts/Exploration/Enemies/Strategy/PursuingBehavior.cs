using System;
using System.Threading;
using System.Threading.Tasks;
using Services.DebugUtilities;
using Services.DebugUtilities.Console;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core.Exploration.Enemy
{
    /// <summary>
    /// Comportamento de perseguição: o inimigo segue ativamente o alvo enquanto ele
    /// permanecer dentro de <see cref="PursuingBehaviorContext.PerceptionRadius"/>.
    /// Ao alcançar o alvo (dentro do stoppingDistance do NavMeshAgent), carrega a cena de combate.
    /// Se o alvo sair do raio de percepção, retorna para <see cref="PatrolBehavior"/>.
    /// </summary>
    public class PursuingBehavior : IReverseableEnemyStartegy
    {
        private CancellationTokenSource _cts;

        // ── IEnemyStartegy ────────────────────────────────────────────────────────

        public async Task ExecuteBehavior(IEnemyStartegyContext context)
        {
            if (context is PursuingBehaviorContext pursuingContext)
            {
                LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.AI,
                    $"Inimigo [{pursuingContext.Enemy.Data.EnemyId}] entrando em [PursuingBehavior]");

                _cts = new CancellationTokenSource();
                await Pursue(pursuingContext, _cts);
            }
        }

        // ── IReverseableEnemyStartegy ─────────────────────────────────────────────

        public async Task UnexecuteBehavior(IEnemyStartegyContext context)
        {
            if (context is PursuingBehaviorContext pursuingContext)
            {
                LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.AI,
                    $"Inimigo [{pursuingContext.Enemy.Data.EnemyId}] saindo de [PursuingBehavior]");

                CancelAndDisposeCts();
            }

            await Task.CompletedTask;
        }

        public void CancelImmediate() => CancelAndDisposeCts();

        // ── Lógica interna ────────────────────────────────────────────────────────

        /// <summary>
        /// Loop principal de perseguição.
        /// A cada iteração: verifica se alvo ainda está no raio → navega em direção a ele.
        /// Se o alvo for alcançado (stoppingDistance), carrega a cena de combate.
        /// Se o alvo sair do raio, retorna para patrulha.
        /// </summary>
        private async Task Pursue(PursuingBehaviorContext context, CancellationTokenSource cts)
        {
            try
            {
                while (!cts.IsCancellationRequested)
                {
                    float sqDist       = (context.Enemy.transform.position - context.Target.position).sqrMagnitude;
                    float sqPerception = context.PerceptionRadius * context.PerceptionRadius;

                    // Alvo saiu do raio de percepção → volta para patrulha
                    if (sqDist > sqPerception)
                    {
                        context.Enemy.Data.Agent.ResetPath();
                        await TransitionToPatrol(context);
                        return;
                    }

                    float sqStopping = context.Enemy.Data.Agent.stoppingDistance *
                                       context.Enemy.Data.Agent.stoppingDistance;

                    if (sqDist > sqStopping)
                    {
                        // Alvo ainda fora do stoppingDistance → navega em direção a ele
                        LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.Data,
                            $"Novo destino de perseguição {context.Target.position} para [{context.Enemy.Data.EnemyId}]");

                        context.Enemy.Data.Agent.SetDestination(context.Target.position);

                        // Aguarda o cálculo do caminho
                        while (context.Enemy.Data.Agent.pathPending)
                        {
                            cts.Token.ThrowIfCancellationRequested();
                            await Task.Yield();
                        }

                        // Segue o alvo; atualiza o destino periodicamente pois o alvo se move
                        while (!cts.IsCancellationRequested &&
                               context.Enemy.Data.Agent.remainingDistance >
                               context.Enemy.Data.Agent.stoppingDistance)
                        {
                            sqDist       = (context.Enemy.transform.position - context.Target.position).sqrMagnitude;
                            sqPerception = context.PerceptionRadius * context.PerceptionRadius;

                            // Alvo saiu do raio durante a navegação
                            if (sqDist > sqPerception)
                            {
                                context.Enemy.Data.Agent.ResetPath();
                                await TransitionToPatrol(context);
                                return;
                            }

                            // Atualiza destino para acompanhar o movimento do alvo
                            context.Enemy.Data.Agent.SetDestination(context.Target.position);

                            await Task.Delay(50, cts.Token);
                        }

                        await Task.Delay(25, cts.Token);
                    }
                    else
                    {
                        // Alvo alcançado → inicia o combate
                        context.Enemy.Data.Agent.ResetPath();

                        LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.AI,
                            $"Inimigo [{context.Enemy.Data.EnemyId}] alcançou o alvo. Carregando cena de combate.");

                        SceneManager.LoadScene("CombatScene");
                        return;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Cancelamento esperado ao trocar de estratégia ou destruir o objeto
            }
        }

        /// <summary>
        /// Solicita a transição de volta para <see cref="PatrolBehavior"/>
        /// quando o alvo sai do raio de percepção.
        /// </summary>
        private Task TransitionToPatrol(PursuingBehaviorContext context)
        {
            return ExplorationEnemyController.SetEnemyStartegy(
                context.Enemy,
                new PatrolBehavior(),
                new PatrolBehaviorContext(
                    context.Enemy,
                    context.Target,
                    context.PerceptionRadius));
        }

        private void CancelAndDisposeCts()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
    }
}
