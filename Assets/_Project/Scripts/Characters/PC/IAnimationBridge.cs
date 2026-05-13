// =============================================================================
// IAnimationBridge.cs
// Contrato de comunicação entre a lógica de gameplay (C# State Machine)
// e o Animator Controller da Unity.
//
// PRINCÍPIO FUNDAMENTAL:
//   A State Machine de gameplay NÃO conhece o Animator diretamente.
//   Ela apenas chama métodos semânticos ("PlayRun", "PlayInteract") e o
//   AnimationBridge concreto traduz isso para parâmetros do Animator.
//
//   Isso garante:
//     - Testabilidade: é possível criar um NullAnimationBridge para testes
//     - Desacoplamento: trocar o Animator não quebra a lógica de estados
//     - Single Responsibility: cada classe faz uma coisa só
// =============================================================================

namespace CharacterSystem.Core
{
    // ── Locomotion ──────────────────────────────────────────────────────────

    /// <summary>
    /// Comandos de animação para a camada Locomotion.
    /// Corresponde ao Animator Layer 0 (full body / lower body mask).
    /// </summary>
    public interface ILocomotionAnimations
    {
        /// <summary>Atualiza o blend tree de movimento (0 = parado, 1 = correndo).</summary>
        void SetMoveSpeed(float normalizedSpeed);

        /// <summary>Ativa o trigger de pulo.</summary>
        void PlayJump();

        /// <summary>Notifica o Animator que o personagem aterrisou.</summary>
        void PlayLand();

        /// <summary>Define se o personagem está agachado (blend entre idle/crouch).</summary>
        void SetCrouching(bool isCrouching);
    }

    // ── Interaction ─────────────────────────────────────────────────────────

    /// <summary>
    /// Comandos de animação para a camada Interaction.
    /// Corresponde ao Animator Layer 1 (upper body / right arm mask).
    /// </summary>
    public interface IInteractionAnimations
    {
        /// <summary>Trigger para animação de pegar item do chão.</summary>
        void PlayPickup();

        /// <summary>Trigger para animação de abrir porta.</summary>
        void PlayOpenDoor();

        /// <summary>Retorna a camada para o estado neutro.</summary>
        void PlayInteractionIdle();
    }

    // ── UseItem ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Comandos de animação para a camada UseItem.
    /// Corresponde ao Animator Layer 2 (right hand / full body override conforme item).
    /// </summary>
    public interface IUseItemAnimations
    {
        /// <summary>Define se o item (ex: tocha) está sendo segurado/ativo.</summary>
        void SetItemActive(bool active);

        /// <summary>Trigger para a ação de usar o item (ex: acender tocha).</summary>
        void PlayUseItem();
    }

    // ── Bridge completa ──────────────────────────────────────────────────────

    /// <summary>
    /// Contrato unificado do Animation Bridge.
    /// O PlayerController depende apenas desta interface — nunca do Animator diretamente.
    /// </summary>
    public interface IAnimationBridge
        : ILocomotionAnimations,
          IInteractionAnimations,
          IUseItemAnimations
    {
        /// <summary>
        /// Chamado pelo CharacterSwitcher ao trocar de personagem.
        /// Permite que a implementação troque o RuntimeAnimatorController.
        /// </summary>
        void SwapAnimator(UnityEngine.RuntimeAnimatorController controller);
    }
}
