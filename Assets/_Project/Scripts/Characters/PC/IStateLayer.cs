// =============================================================================
// IStateLayer.cs
// Contrato de uma camada da Layered State Machine (LSM).
// Cada camada roda sua própria FSM independente todo frame.
// Exemplos de camadas: Locomotion, Interaction, UseItem.
// =============================================================================

namespace CharacterSystem.Core
{
    /// <summary>
    /// Representa uma camada autônoma da Layered State Machine.
    /// Camadas rodam em paralelo e se comunicam apenas via <see cref="PlayerContext"/>.
    /// </summary>
    public interface IStateLayer
    {
        /// <summary>Nome da camada (para logs e debug).</summary>
        string LayerName { get; }

        /// <summary>Estado atualmente ativo nesta camada.</summary>
        ICharacterState CurrentState { get; }

        /// <summary>
        /// Inicializa a camada com seu estado padrão.
        /// Chamado pelo PlayerController no Awake/Start.
        /// </summary>
        void Initialize(PlayerContext context);

        /// <summary>
        /// Processa a lógica da camada a cada frame.
        /// Deve chamar CurrentState.OnUpdate internamente.
        /// </summary>
        void Update(PlayerContext context);

        /// <summary>
        /// Tenta realizar uma transição para <paramref name="nextState"/>.
        /// A transição só ocorre se CurrentState.CanTransitionTo retornar true.
        /// </summary>
        bool TryTransition(ICharacterState nextState, PlayerContext context);
    }
}
