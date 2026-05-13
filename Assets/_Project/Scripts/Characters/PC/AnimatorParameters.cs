// =============================================================================
// AnimatorParameters.cs
// Centraliza todos os nomes e hashes dos parâmetros do Animator Controller.
//
// POR QUÊ USAR HASHES:
//   animator.SetFloat("MoveSpeed", v) faz uma busca por string a cada frame.
//   animator.SetFloat(AnimatorParameters.MoveSpeed, v) usa um int pré-calculado —
//   é mais performático e evita bugs por typo.
//
// CONVENÇÃO:
//   Os 3 AnimatorControllers (um por personagem) DEVEM ter exatamente estes
//   parâmetros. O AnimationBridge não compila se usar strings fora daqui.
// =============================================================================

using UnityEngine;

namespace CharacterSystem.Animation
{
    /// <summary>
    /// Hashes pré-calculados dos parâmetros do Animator Controller.
    /// Use sempre estes campos — nunca strings literais no código.
    /// </summary>
    public static class AnimatorParameters
    {
        // ── Locomotion Layer ──────────────────────────────────────────────────

        /// <summary>
        /// Float [0..1]: velocidade normalizada (0=idle, 0.5=walk, 1=run).
        /// Alimenta o Blend Tree da camada Locomotion.
        /// </summary>
        public static readonly int MoveSpeed = Animator.StringToHash("MoveSpeed");

        /// <summary>Trigger: dispara a animação de pulo.</summary>
        public static readonly int Jump = Animator.StringToHash("Jump");

        /// <summary>Trigger: dispara a animação de aterrissagem.</summary>
        public static readonly int Land = Animator.StringToHash("Land");

        /// <summary>Bool: true enquanto o personagem está agachado.</summary>
        public static readonly int IsCrouching = Animator.StringToHash("IsCrouching");

        /// <summary>Bool: true enquanto o personagem está no ar.</summary>
        public static readonly int IsAirborne = Animator.StringToHash("IsAirborne");

        // ── Interaction Layer ─────────────────────────────────────────────────

        /// <summary>Trigger: animação de pegar item do chão.</summary>
        public static readonly int Pickup = Animator.StringToHash("Pickup");

        /// <summary>Trigger: animação de abrir porta.</summary>
        public static readonly int OpenDoor = Animator.StringToHash("OpenDoor");

        /// <summary>
        /// Bool: true quando a camada Interaction está em uma ação.
        /// Usado internamente para retornar ao estado neutro via transition condition.
        /// </summary>
        public static readonly int IsInteracting = Animator.StringToHash("IsInteracting");

        // ── UseItem Layer ─────────────────────────────────────────────────────

        /// <summary>Bool: true enquanto o item (ex: tocha) está ativo/segurado.</summary>
        public static readonly int ItemActive = Animator.StringToHash("ItemActive");

        /// <summary>Trigger: ação de usar o item (ex: acender a tocha).</summary>
        public static readonly int UseItem = Animator.StringToHash("UseItem");
    }
}
