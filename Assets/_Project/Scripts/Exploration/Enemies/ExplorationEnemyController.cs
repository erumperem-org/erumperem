using System.Threading;
using System.Threading.Tasks;
using Services.DebugUtilities;
using Services.DebugUtilities.Console;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class ExplorationEnemyController : MonoBehaviour
{
    public ExplorationEnemyData data = new ExplorationEnemyData();

    // FIX (race condition): SemaphoreSlim(1,1) garante que apenas uma chamada
    // a SetEnemyStartegy rode por vez para este controller. Sem isso, dois
    // callers simultâneos (ex: PoolMassiveSpawnTest) leriam _enemyStartegy ao
    // mesmo tempo, ambos fariam UnexecuteBehavior e depois ExecuteBehavior,
    // resultando em dois behavior loops rodando ao mesmo tempo no mesmo inimigo.
    private readonly SemaphoreSlim _strategySemaphore = new SemaphoreSlim(1, 1);

    public static async Task SetEnemyStartegy(
        ExplorationEnemyController controller,
        IEnemyStartegy newEnemyStartegy,
        IEnemyStartegyContext newContext)
    {
        // FIX (race condition): aguarda o semáforo antes de qualquer leitura
        // ou escrita em _enemyStartegy. O bloco try/finally garante a liberação
        // mesmo que UnexecuteBehavior lance uma exceção.
        await controller._strategySemaphore.WaitAsync();
        try
        {
            if (controller.data._enemyStartegy == null)
            {
                LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.AI,
                    $"Setting new strategy [{newEnemyStartegy}], in [{controller.data.enemyId}]");
            }
            else
            {
                LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.AI,
                    $"Setting new strategy [{newEnemyStartegy}] over [{controller.data._enemyStartegy}], in [{controller.data.enemyId}]");

                if (controller.data._enemyStartegy is IReverseableEnemyStartegy reverseable)
                {
                    await reverseable.UnexecuteBehavior(controller.data.currentContext);
                }
            }

            controller.data._enemyStartegy       = newEnemyStartegy;
            controller.data.enemyStartegyExposed  = newEnemyStartegy.GetType().ToString();
            controller.data.currentContext        = newContext;

            // FIX (await ExecuteBehavior bloqueava o caller): ExecuteBehavior
            // inicia um loop infinito. Awaitar isso significava que
            // SetEnemyStartegy nunca retornava enquanto o behavior estivesse
            // ativo, deixando qualquer caller suspenso indefinidamente.
            // Agora disparamos o loop sem awaitar — ele se gerencia via CTS.
            _ = controller.data._enemyStartegy.ExecuteBehavior(newContext);
        }
        finally
        {
            controller._strategySemaphore.Release();
        }
    }

    public static Task SetEnemyLevel(
        ExplorationEnemyController controller,
        ExplorationEnemyLevels newLevel)
    {
        controller.data.enemyLevel = newLevel;
        return Task.CompletedTask;
    }

    // FIX (async void OnDestroy): Unity destrói o GameObject imediatamente ao
    // chamar OnDestroy — não awaita métodos async void. Se houvesse um await
    // aqui, o código continuaria rodando após a destruição e acessaria
    // transform/agent já nulos, causando NullReferenceException.
    // A solução é cancelar de forma síncrona via CancelImmediate(), que apenas
    // chama _cts?.Cancel() — sem nenhum await, sem risco de acesso pós-destruição.
    private void OnDestroy()
    {
        if (data._enemyStartegy is IReverseableEnemyStartegy reverseable)
        {
            reverseable.CancelImmediate();
        }
    }
}
