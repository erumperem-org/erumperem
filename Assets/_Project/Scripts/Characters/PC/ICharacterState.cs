// =============================================================================
// ICharacterState.cs
// Contrato base para todos os estados da máquina de estados do personagem.
// Cada estado pertence a uma camada (IStateLayer) e é responsável por:
//   - Reagir à entrada no estado (OnEnter)
//   - Processar lógica a cada frame (OnUpdate)
//   - Reagir à saída do estado (OnExit)
//   - Declarar se aceita uma transição para outro estado (CanTransitionTo)
// =============================================================================

namespace CharacterSystem.Core
{
    /// <summary>
    /// Contrato base para qualquer estado da máquina de estados em camadas.
    /// Segue o princípio ISP (Interface Segregation): apenas o mínimo necessário.
    /// </summary>
    public interface ICharacterState
    {
        /// <summary>Identificador legível do estado (usado em logs e debug).</summary>
        string StateName { get; }

        /// <summary>
        /// Chamado uma vez ao entrar no estado.
        /// Use para inicializar timers, notificar o AnimationBridge, etc.
        /// </summary>
        void OnEnter(PlayerContext context);

        /// <summary>
        /// Chamado todo frame enquanto este estado está ativo.
        /// Deve ser leve — delegue cálculos pesados a serviços externos.
        /// </summary>
        void OnUpdate(PlayerContext context);

        /// <summary>
        /// Chamado uma vez ao sair do estado.
        /// Use para limpar flags no contexto ou cancelar coroutines.
        /// </summary>
        void OnExit(PlayerContext context);

        /// <summary>
        /// Determina se este estado permite transição para <paramref name="next"/>.
        /// Centraliza a regra de negócio de exclusão mútua dentro do próprio estado.
        /// </summary>
        /// <param name="next">Estado candidato à transição.</param>
        /// <param name="context">Contexto compartilhado para leitura de flags.</param>
        /// <returns>True se a transição for permitida.</returns>
        bool CanTransitionTo(ICharacterState next, PlayerContext context);
    }
}
