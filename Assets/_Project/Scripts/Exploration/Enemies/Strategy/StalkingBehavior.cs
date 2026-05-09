using System;
using System.Threading;
using System.Threading.Tasks;
using Services.DebugUtilities;
using Services.DebugUtilities.Console;
using UnityEngine;

namespace Core.Exploration.Enemy
{
    /// <summary>
    /// Comportamento de stalking: o inimigo mantém uma distância fixa do alvo,
    /// posicionando-se atrás/ao redor dele sem se aproximar demais.
    /// Ideal para criar tensão sem engajar diretamente o jogador.
    /// </summary>
    public class StalkingBehavior : IReverseableEnemyStartegy
    {
        private CancellationTokenSource _cts;

        // ── IEnemyStartegy ────────────────────────────────────────────────────────

        public async Task ExecuteBehavior(IEnemyStartegyContext context)
        {
            if (context is StalkingBehaviorContext stalkingContext)
            {
                LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.AI,
                    $"Inimigo [{stalkingContext.Enemy.Data.EnemyId}] entrando em [StalkingBehavior]");

                _cts = new CancellationTokenSource();
                await Stalk(stalkingContext, _cts);
            }
        }

        // ── IReverseableEnemyStartegy ─────────────────────────────────────────────

        public async Task UnexecuteBehavior(IEnemyStartegyContext context)
        {
            if (context is StalkingBehaviorContext stalkingContext)
            {
                LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.AI,
                    $"Inimigo [{stalkingContext.Enemy.Data.EnemyId}] saindo de [StalkingBehavior]");

                CancelAndDisposeCts();
            }

            await Task.CompletedTask;
        }

        public void CancelImmediate() => CancelAndDisposeCts();

        // ── Lógica interna ────────────────────────────────────────────────────────

        /// <summary>
        /// Loop principal de stalking.
        /// O inimigo calcula o ponto ideal (na direção oposta ao alvo, a <see cref="StalkingBehaviorContext.StalkingDistance"/>)
        /// e navega até ele somente quando o alvo se move, evitando recálculos desnecessários.
        /// </summary>
        private async Task Stalk(StalkingBehaviorContext context, CancellationTokenSource cts)
        {
            try
            {
                Vector3 lastKnownTargetPosition = context.Target.position;

                while (!cts.IsCancellationRequested)
                {
                    Vector3 targetPos  = context.Target.position;
                    Vector3 toTarget   = context.Enemy.transform.position - targetPos;
                    float   distanceSq = toTarget.sqrMagnitude;
                    float   stalkSq    = context.StalkingDistance * context.StalkingDistance;

                    if (distanceSq > stalkSq)
                    {
                        // Inimigo está mais longe que a distância de stalking;
                        // só recalcula o caminho se o alvo tiver se movido significativamente
                        if ((lastKnownTargetPosition - targetPos).sqrMagnitude > 0.01f)
                        {
                            lastKnownTargetPosition = targetPos;

                            // Posição desejada: atrás do alvo na direção do inimigo
                            Vector3 direction       = toTarget.normalized;
                            Vector3 desiredPosition = targetPos + direction * context.StalkingDistance;

                            LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.Data,
                                $"Nova posição de stalking {desiredPosition} para [{context.Enemy.Data.EnemyId}]");

                            context.Enemy.Data.Agent.SetDestination(desiredPosition);

                            // Aguarda o cálculo do caminho
                            while (context.Enemy.Data.Agent.pathPending)
                            {
                                cts.Token.ThrowIfCancellationRequested();
                                await Task.Yield();
                            }

                            // Navega até a posição de stalking, atualizando o destino se o alvo se mover
                            while (!cts.IsCancellationRequested &&
                                   context.Enemy.Data.Agent.remainingDistance >
                                   context.Enemy.Data.Agent.stoppingDistance)
                            {
                                targetPos  = context.Target.position;
                                toTarget   = context.Enemy.transform.position - targetPos;
                                distanceSq = toTarget.sqrMagnitude;

                                // Já está na distância correta; interrompe o movimento
                                if (distanceSq <= stalkSq)
                                {
                                    context.Enemy.Data.Agent.ResetPath();
                                    break;
                                }

                                // Atualiza o destino se o alvo tiver se movido
                                if ((lastKnownTargetPosition - targetPos).sqrMagnitude > 0.01f)
                                {
                                    lastKnownTargetPosition = targetPos;
                                    direction               = toTarget.normalized;
                                    desiredPosition         = targetPos + direction * context.StalkingDistance;
                                    context.Enemy.Data.Agent.SetDestination(desiredPosition);
                                }

                                await Task.Delay(50, cts.Token);
                            }
                        }
                    }
                    else
                    {
                        // Já dentro da distância de stalking; para e aguarda o alvo se mover
                        context.Enemy.Data.Agent.ResetPath();
                    }

                    // Intervalo de polling para não sobrecarregar o loop
                    await Task.Delay(100, cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Cancelamento esperado ao trocar de estratégia ou destruir o objeto
            }
        }

        private void CancelAndDisposeCts()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
    }
}
