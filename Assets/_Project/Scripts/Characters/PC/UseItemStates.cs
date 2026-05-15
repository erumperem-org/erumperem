// =============================================================================
// UseItemStates.cs
// Estados da camada UseItem (Layer 2 do Animator — right arm mask).
//
// ESTADOS:
//   ItemIdleState   → item inativo; mão em repouso; camada "transparente"
//   ItemActiveState → tocha acesa; mão sustenta o item; sub-ação de "usar"
//                     disponível (ex: acender com efeito especial)
//
// SOBRE ItemActiveState:
//   O "uso" do item (PlayUseItem) é uma sub-ação dentro do estado ativo.
//   O estado não sai de si mesmo ao usar — apenas dispara o trigger de animação
//   e aguarda a próxima pressão de UseItemPressed para desativar.
//   Isso espelha o comportamento de uma tocha: você a mantém acesa e
//   pressionar novamente a apaga/guarda.
// =============================================================================

using CharacterSystem.Core;

namespace CharacterSystem.Layers.UseItem
{
    // ══════════════════════════════════════════════════════════════════════════
    // ItemIdleState
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Item inativo. Mão direita em repouso natural.
    /// Camada "transparente" — não adiciona animação sobre o body inferior/superior.
    /// Transições: → ItemActiveState (ao pressionar UseItem).
    /// </summary>
    public class ItemIdleState : ICharacterState
    {
        public string StateName => "UseItem.Idle";

        public void OnEnter(PlayerContext ctx)
        {
            // Informa o contexto e o Animator que o item foi desativado
            ctx.IsUsingItem = false;
            ctx.AnimationBridge.SetItemActive(false);
        }

        public void OnUpdate(PlayerContext ctx)
        {
            // Nenhuma lógica — aguarda input (gerenciado pela Layer)
        }

        public void OnExit(PlayerContext ctx) { }

        public bool CanTransitionTo(ICharacterState next, PlayerContext ctx)
        {
            // Idle aceita qualquer transição — não há restrição de saída
            return true;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ItemActiveState
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Item ativo (tocha acesa). Mão direita sustenta o item continuamente.
    /// Sub-ação "usar" disponível (ex: girar a tocha, acender algo).
    /// Transições: → ItemIdleState (ao pressionar UseItem novamente).
    /// </summary>
    public class ItemActiveState : ICharacterState
    {
        // Controla se a sub-ação "usar" já foi disparada neste frame
        // (evita múltiplas chamadas se o input persistir)
        private bool _useActionConsumed;

        public string StateName => "UseItem.Active";

        public void OnEnter(PlayerContext ctx)
        {
            _useActionConsumed = false;

            // Informa o contexto que os braços estão ocupados com o item
            ctx.IsUsingItem = true;
            ctx.AnimationBridge.SetItemActive(true);
        }

        public void OnUpdate(PlayerContext ctx)
        {
            // UseItemPressed no estado ativo é tratado pela Layer como toggle de saída.
            // Aqui verificamos se há uma "segunda pressão" de ação especial.
            // Neste design simples, UseItemPressed → desativa (tratado pela Layer).
            // Para uma ação secundária (ex: jogar a tocha), adicione um segundo input.

            // Reset do consumo a cada frame para que a Layer possa detectar o pressed
            _useActionConsumed = false;
        }

        public void OnExit(PlayerContext ctx)
        {
            ctx.IsUsingItem = false;
        }

        public bool CanTransitionTo(ICharacterState next, PlayerContext ctx)
        {
            // Ativo só pode transitar para Idle — não pode ativar outro item
            // diretamente sem passar pelo idle (limpeza de estado)
            return next is ItemIdleState;
        }
    }
}
