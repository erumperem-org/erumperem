using System.Threading;
using System.Threading.Tasks;
using Core.Exploration.Character.Movement;
using Services.DebugUtilities;
using Services.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Core.Exploration.Character.Movement
{
    /// <summary>
    /// Controller de movimentação do NPC.
    ///
    /// Gerencia a estratégia ativa e garante que as trocas sejam atômicas via
    /// <see cref="SemaphoreSlim"/> — sem corrida mesmo que dois sistemas tentem
    /// trocar a estratégia simultaneamente.
    ///
    /// Diferente da versão original, <see cref="SetStrategy"/> é de instância:
    /// o controller é injetável e testável sem depender de estado estático.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(NavMeshAgentAdapter))]
    public class NpcMovementController : MonoBehaviour
    {
        // ── Estado ────────────────────────────────────────────────────────

        private ICharacterMovementStrategy         _activeStrategy;
        private ICharacterMovementStrategyContext   _currentContext;
        private string                             _activeStrategyName;

        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        // ── Componentes resolvidos no Awake ───────────────────────────────

        public NavMeshAgentAdapter Adapter  { get; private set; }
        public INavMeshService     NavMesh  { get; private set; }

        // ── Propriedade de leitura (debug/UI) ─────────────────────────────

        public string ActiveStrategyName => _activeStrategyName ?? "None";

        // ── Ciclo de vida Unity ───────────────────────────────────────────

        private void Awake()
        {
            Adapter = GetComponent<NavMeshAgentAdapter>();
            NavMesh = new NavMeshService();
        }

        private void OnDestroy()
        {
            if (_activeStrategy is IReversibleCharacterMovementStrategy reversible)
                reversible.CancelImmediate();
        }

        // ── API pública ───────────────────────────────────────────────────

        /// <summary>
        /// Substitui a estratégia ativa de forma atômica.
        /// Aguarda o <see cref="IReversibleCharacterMovementStrategy.UnexecuteBehavior"/>
        /// da estratégia anterior antes de iniciar a nova.
        /// </summary>
        public async Task SetStrategy(
            ICharacterMovementStrategy        newStrategy,
            ICharacterMovementStrategyContext  newContext)
        {
            await _semaphore.WaitAsync();
            try
            {
                if (_activeStrategy == null)
                {
                    LoggerService.PrintLogMessage(
                        LogLevel.Debug,
                        $"[{name}] Estratégia inicial → [{newStrategy.GetType().Name}]", LogCategory.NPC, LogCategory.AI, LogCategory.Navigation);
                }
                else
                {
                    LoggerService.PrintLogMessage(
                        LogLevel.Debug,
                        $"[{name}] [{_activeStrategyName}] → [{newStrategy.GetType().Name}]", LogCategory.NPC, LogCategory.AI, LogCategory.Navigation);

                    if (_activeStrategy is IReversibleCharacterMovementStrategy reversible)
                        await reversible.UnexecuteBehavior(_currentContext);
                }

                _activeStrategy     = newStrategy;
                _activeStrategyName = newStrategy.GetType().Name;
                _currentContext     = newContext;

                // Fire-and-forget intencional: o behavior roda de forma
                // independente e se cancela pelo próprio CTS interno.
                _ = _activeStrategy.ExecuteBehavior(newContext);
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
