// =============================================================================
// UseItemLayer.cs
// Camada UseItem da LSM. Gerencia o uso da tocha (e futuros itens).
//
// DESIGN:
//   Esta camada controla a mão direita do personagem — corresponde ao
//   Animator Layer 2 com Avatar Mask apenas no braço direito.
//
//   Dois estados:
//     ItemIdleState   → item inativo / guardado (transparente)
//     ItemActiveState → item ativo (tocha acesa), com sub-ação "usar" (acender)
//
//   A exclusão com Interaction é bilateral via flags no PlayerContext:
//     - UseItemLayer lê ctx.IsInteracting antes de acionar o item
//     - InteractionLayer lê ctx.IsUsingItem antes de iniciar interações
//
// EXTENSIBILIDADE:
//   Para adicionar um segundo item, crie um novo ICharacterState e adicione
//   a lógica de seleção em EvaluateTransitions. O CharacterData pode
//   carregar uma referência ao SO do item para desacoplar ainda mais.
// =============================================================================

using CharacterSystem.Core;
using CharacterSystem.StateMachine;

namespace CharacterSystem.Layers.UseItem
{
    /// <summary>
    /// Camada que controla o uso de itens ativos (ex: tocha).
    /// Corresponde ao Animator Layer 2 (right arm mask).
    /// </summary>
    public class UseItemLayer
    {
        // ── Estados ──────────────────────────────────────────────────────────

        private readonly ItemIdleState   _idle   = new();
        private readonly ItemActiveState _active = new();

        // ── State Machine interna ────────────────────────────────────────────

        private readonly StateLayer _layer;

        // ── Construtor ───────────────────────────────────────────────────────

        public UseItemLayer()
        {
            _layer = new StateLayer("UseItem", _idle);
        }

        // ── API Pública ──────────────────────────────────────────────────────

        /// <summary>Inicializa a camada. Chamado pelo PlayerController no Start.</summary>
        public void Initialize(PlayerContext ctx) => _layer.Initialize(ctx);

        /// <summary>
        /// Processa a lógica de uso de item a cada frame.
        /// Deve ser chamado pelo PlayerController.Update() após Interaction.
        /// </summary>
        public void Update(PlayerContext ctx)
        {
            EvaluateTransitions(ctx);
            _layer.Update(ctx);
        }

        // ── Lógica de Transição ──────────────────────────────────────────────

        /// <summary>
        /// Avalia se o item deve ser ativado/desativado ou usado.
        /// </summary>
        private void EvaluateTransitions(PlayerContext ctx)
        {
            var current = _layer.CurrentState;

            // Toggle do item: pressionar UseItem quando idle → ativa
            if (ctx.UseItemPressed && current is ItemIdleState)
            {
                // Não ativa se os braços estão ocupados com interação
                if (ctx.IsInteracting) return;

                _layer.TryTransition(_active, ctx);
                return;
            }

            // Toggle: pressionar UseItem quando ativo → desativa
            if (ctx.UseItemPressed && current is ItemActiveState)
            {
                _layer.TryTransition(_idle, ctx);
            }
        }
    }
}
