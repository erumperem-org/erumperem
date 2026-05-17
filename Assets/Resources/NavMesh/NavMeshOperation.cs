using System;
using System.Threading;
using System.Threading.Tasks;

namespace Services.Navigation
{
    /// <summary>
    /// Handle retornado por operações assíncronas do <see cref="NavMeshService"/>.
    /// Cada caller possui sua própria instância — o estado não é compartilhado,
    /// eliminando a condição de corrida entre múltiplos consumidores do mesmo agente.
    ///
    /// Uso:
    /// <code>
    /// using var op = navMeshService.MoveToAsync(agent, destination, cts.Token);
    /// bool reached = await op.Task;
    /// </code>
    ///
    /// Descarte (<see cref="Dispose"/>) cancela a operação e libera o CTS interno.
    /// </summary>
    public sealed class NavMeshOperation : IDisposable
    {
        // ─────────────────────────────────────────────────────────────
        // Estado
        // ─────────────────────────────────────────────────────────────

        private readonly CancellationTokenSource _linkedCts;
        private readonly TaskCompletionSource<bool> _tcs;
        private int _disposed; // 0 = vivo, 1 = descartado (Interlocked)

        // ─────────────────────────────────────────────────────────────
        // API pública
        // ─────────────────────────────────────────────────────────────

        /// <summary>Task que representa a conclusão da operação.</summary>
        public Task<bool> Task => _tcs.Task;

        /// <summary>
        /// Token derivado do CTS externo do caller e do CTS interno desta operação.
        /// Todas as coroutines e loops da operação observam este token.
        /// </summary>
        public CancellationToken Token => _linkedCts.Token;

        /// <summary>Indica se a operação ainda está em andamento.</summary>
        public bool IsRunning => !_tcs.Task.IsCompleted;
        /// <summary>Indica se a operação foi concluída (com sucesso, falha ou cancelamento).</summary>
        public bool IsCompleted => _tcs.Task.IsCompleted;

        // ─────────────────────────────────────────────────────────────
        // Construção interna (apenas NavMeshService cria instâncias)
        // ─────────────────────────────────────────────────────────────

        internal NavMeshOperation(CancellationToken callerToken)
        {
            _tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _linkedCts = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
        }

        // ─────────────────────────────────────────────────────────────
        // Conclusão interna (chamada pelo NavMeshService via coroutine)
        // ─────────────────────────────────────────────────────────────

        internal void Complete(bool result)
            => _tcs.TrySetResult(result);

        internal void Fault(Exception ex)
            => _tcs.TrySetException(ex);

        // ─────────────────────────────────────────────────────────────
        // Cancelamento público
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Cancela a operação imediatamente.
        /// A <see cref="Task"/> resolverá com <c>false</c>.
        /// </summary>
        public void Cancel()
        {
            if (Interlocked.CompareExchange(ref _disposed, 0, 0) == 1) return;
            _linkedCts.Cancel();
            _tcs.TrySetResult(false);
        }

        // ─────────────────────────────────────────────────────────────
        // IDisposable
        // ─────────────────────────────────────────────────────────────

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1) return;

            _linkedCts.Cancel();
            _tcs.TrySetResult(false);
            _linkedCts.Dispose();
        }
    }
}
