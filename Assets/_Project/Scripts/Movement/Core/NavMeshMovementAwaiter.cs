using System.Threading;
using System.Threading.Tasks;
using Services.Navigation;
using UnityEngine;

namespace Core.Exploration.Character.Movement
{
    /// <summary>
    /// Polling assíncrono compartilhado para movimentação NavMesh nos behaviors.
    /// Centraliza os loops de espera por cálculo de caminho e chegada ao destino.
    /// </summary>
    internal static class NavMeshMovementAwaiter
    {
        public static async Task MoveToDestinationAndAwaitArrivalAsync(
            INavMeshService navMesh,
            NavMeshAgentAdapter adapter,
            Vector3 destination,
            CancellationToken cancellationToken,
            int pathPendingDelayMilliseconds,
            int movementPollDelayMilliseconds)
        {
            navMesh.MoveTo(adapter, destination);
            await AwaitPathCalculationAsync(
                navMesh, adapter, cancellationToken, pathPendingDelayMilliseconds);
            await AwaitDestinationReachedAsync(
                navMesh, adapter, cancellationToken, movementPollDelayMilliseconds);
        }

        public static async Task AwaitPathCalculationAsync(
            INavMeshService navMesh,
            NavMeshAgentAdapter adapter,
            CancellationToken cancellationToken,
            int pathPendingDelayMilliseconds)
        {
            while (navMesh.IsPending(adapter) && !cancellationToken.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(pathPendingDelayMilliseconds, cancellationToken);
            }
        }

        public static async Task AwaitDestinationReachedAsync(
            INavMeshService navMesh,
            NavMeshAgentAdapter adapter,
            CancellationToken cancellationToken,
            int movementPollDelayMilliseconds)
        {
            while (!cancellationToken.IsCancellationRequested
                   && !navMesh.HasReachedDestination(adapter))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(movementPollDelayMilliseconds, cancellationToken);
            }
        }
    }
}
