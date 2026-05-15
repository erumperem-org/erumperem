// =============================================================================
// InteractionStates.cs
// Estados da camada Interaction (Layer 1 do Animator — upper body mask).
//
// ESTADOS:
//   InteractionIdleState → braços em repouso, detectando interatáveis
//   PickupState          → animação de pegar item do chão
//   OpenDoorState        → animação de abrir porta
//
// DURAÇÃO DE ANIMAÇÃO:
//   Ações como pickup e open door têm duração fixa. O estado usa um timer
//   interno baseado na duração configurada para retornar ao idle automaticamente.
//   Em produção, prefira Animation Events para marcar o fim — mais robusto
//   contra mudanças de velocidade do Animator. O timer é um fallback seguro.
//
// EXCLUSÃO MÚTUA COM UseItem:
//   Antes de entrar em PickupState ou OpenDoorState, InteractionLayer verifica
//   ctx.IsUsingItem. Aqui, os estados apenas declaram que bloqueiam transições
//   entre si enquanto a ação está em curso.
// =============================================================================

using UnityEngine;
using CharacterSystem.Core;
using CharacterSystem.Animation;

namespace CharacterSystem.Layers.Interaction
{
    // ══════════════════════════════════════════════════════════════════════════
    // InteractionIdleState
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Estado neutro da camada Interaction.
    /// Braços em repouso; camada "transparente" — não interfere com Locomotion.
    /// Transições: → PickupState, → OpenDoorState.
    /// </summary>
    public class InteractionIdleState : ICharacterState
    {
        public string StateName => "Interaction.Idle";

        public void OnEnter(PlayerContext ctx)
        {
            // Garante que o flag de interação está baixo ao voltar para idle
            ctx.IsInteracting = false;
            ctx.AnimationBridge.PlayInteractionIdle();
        }

        public void OnUpdate(PlayerContext ctx)
        {
            // Nenhuma lógica — apenas aguarda input (gerenciado pela Layer)
        }

        public void OnExit(PlayerContext ctx) { }

        public bool CanTransitionTo(ICharacterState next, PlayerContext ctx)
        {
            // Idle aceita qualquer transição da camada Interaction
            return true;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PickupState
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Personagem executando a animação de pegar um item do chão.
    /// Efetua o pickup no meio da animação (via timer interno ou Animation Event).
    /// Transições: → InteractionIdleState (ao concluir).
    /// </summary>
    public class PickupState : ICharacterState
    {
        // Duração total da animação de pickup em segundos.
        // Ajuste para corresponder à duração real do clipe de animação.
        private const float AnimationDuration = 1.2f;

        // Ponto da animação em que o item é coletado (ex: 60% do clipe)
        private const float PickupMoment = 0.6f;

        private float _timer;
        private bool  _itemCollected;

        public string StateName => "Interaction.Pickup";

        public void OnEnter(PlayerContext ctx)
        {
            _timer        = 0f;
            _itemCollected = false;

            ctx.IsInteracting = true;
            ctx.AnimationBridge.PlayPickup();
        }

        public void OnUpdate(PlayerContext ctx)
        {
            _timer += Time.deltaTime;

            // Aplica o pickup no momento correto da animação
            if (!_itemCollected && _timer >= AnimationDuration * PickupMoment)
            {
                _itemCollected = true;
                ctx.NearestPickable?.Pickup(ctx);
            }

            // Sinaliza fim da animação para que a Layer possa transitar para Idle
            if (_timer >= AnimationDuration)
            {
                ctx.IsInteracting = false; // Layer vai detectar isso e transitar
            }
        }

        public void OnExit(PlayerContext ctx)
        {
            ctx.IsInteracting = false;
        }

        public bool CanTransitionTo(ICharacterState next, PlayerContext ctx)
        {
            // Enquanto animando, só permite retornar para Idle
            // Não pode encadear outro Pickup ou OpenDoor sem terminar o atual
            return next is InteractionIdleState;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // OpenDoorState
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Personagem abrindo uma porta ou objeto interativo.
    /// Bloqueia Locomotion de entrar em MovingState enquanto abre? Não —
    /// essa decisão é do jogo. Aqui apenas gerenciamos o braço.
    /// Transições: → InteractionIdleState (ao concluir).
    /// </summary>
    public class OpenDoorState : ICharacterState
    {
        // Duração da animação de abrir porta
        private const float AnimationDuration = 1.5f;

        // Ponto da animação em que a porta é efetivamente aberta
        private const float InteractMoment = 0.5f;

        private float _timer;
        private bool  _interacted;

        public string StateName => "Interaction.OpenDoor";

        public void OnEnter(PlayerContext ctx)
        {
            _timer     = 0f;
            _interacted = false;

            ctx.IsInteracting = true;
            ctx.AnimationBridge.PlayOpenDoor();
        }

        public void OnUpdate(PlayerContext ctx)
        {
            _timer += Time.deltaTime;

            // Dispara a interação no momento certo da animação
            if (!_interacted && _timer >= AnimationDuration * InteractMoment)
            {
                _interacted = true;
                ctx.NearestInteractable?.Interact();
            }

            // Marca fim da animação
            if (_timer >= AnimationDuration)
            {
                ctx.IsInteracting = false;
            }
        }

        public void OnExit(PlayerContext ctx)
        {
            ctx.IsInteracting = false;
        }

        public bool CanTransitionTo(ICharacterState next, PlayerContext ctx)
        {
            // Enquanto abrindo porta, só pode ir para Idle
            return next is InteractionIdleState;
        }
    }
}
