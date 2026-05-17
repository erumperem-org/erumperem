using System.Threading.Tasks;

namespace Core.Exploration.Character.Movement
{
    /// <summary>
    /// Contrato base de um comportamento de movimentação.
    /// </summary>
    public interface ICharacterMovementStrategy
    {
        Task ExecuteBehavior(ICharacterMovementStrategyContext context);
    }

    /// <summary>
    /// Comportamento que pode ser revertido de forma assíncrona antes de ser substituído.
    /// </summary>
    public interface IReversibleCharacterMovementStrategy : ICharacterMovementStrategy
    {
        /// <summary>Chamado pelo controller antes de trocar de estratégia.</summary>
        Task UnexecuteBehavior(ICharacterMovementStrategyContext context);

        /// <summary>Cancelamento síncrono imediato (OnDestroy, erros críticos).</summary>
        void CancelImmediate();
    }

    /// <summary>
    /// Marcador base para todos os contextos de comportamento.
    /// </summary>
    public interface ICharacterMovementStrategyContext { }
}
