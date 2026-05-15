// =============================================================================
// PlayerContext.cs
// Objeto de dados compartilhado entre todas as camadas da State Machine.
//
// PAPEL:
//   Funciona como um "quadro-negro" (blackboard) — camadas leem e escrevem
//   aqui para se comunicar SEM se conhecerem diretamente.
//
// REGRAS:
//   - Nunca coloque lógica aqui. Apenas dados.
//   - Flags booleanas indicam estado atual, não comandos.
//   - Referências a componentes Unity ficam aqui para evitar que estados
//     precisem de [SerializeField] próprios.
// =============================================================================
using CharacterSystem.Character;
using Services.Navigation;

using UnityEngine;
using UnityEngine.AI;

namespace CharacterSystem.Core
{
    /// <summary>
    /// Blackboard compartilhado entre todas as camadas da Layered State Machine.
    /// Passado por referência — mutações são visíveis imediatamente por todas as camadas.
    /// </summary>
    public class PlayerContext
    {
        // ── Referências de componentes ───────────────────────────────────────

        /// <summary>CharacterController da Unity para movimento físico.</summary>
        public NavMeshService NavMeshService { get; set; }
        public NavMeshAgent Agent { get; set; }

        /// <summary>Transform da câmera (usado para calcular direção de movimento).</summary>
        public Transform CameraTransform { get; set; }

        /// <summary>Bridge de animação — único canal de comunicação com o Animator.</summary>
        public IAnimationBridge AnimationBridge { get; set; }

        // ── Input (escrito pelo InputReader, lido pelas camadas) ─────────────

        /// <summary>Vetor de movimento normalizado lido do input (espaço da câmera).</summary>
        public Vector2 MoveInput { get; set; }

        /// <summary>True no frame em que o botão de pulo foi pressionado.</summary>
        public bool JumpPressed { get; set; }

        /// <summary>True enquanto o botão de agachar estiver pressionado.</summary>
        public bool CrouchHeld { get; set; }

        /// <summary>True no frame em que o botão de interação foi pressionado.</summary>
        public bool InteractPressed { get; set; }

        /// <summary>True no frame em que o botão de usar item foi pressionado.</summary>
        public bool UseItemPressed { get; set; }

        // ── Estado físico (escrito pela camada Locomotion) ───────────────────

        /// <summary>Velocidade atual do personagem no mundo.</summary>
        public Vector3 Velocity { get; set; }

        /// <summary>True se o personagem está no chão.</summary>
        public bool IsGrounded { get; set; }

        /// <summary>True se o personagem está agachado.</summary>
        public bool IsCrouching { get; set; }

        // ── Flags de ocupação entre camadas ─────────────────────────────────

        /// <summary>
        /// True quando a camada Interaction está executando uma ação.
        /// Usado por UseItem para bloquear uso simultâneo das mãos.
        /// </summary>
        public bool IsInteracting { get; set; }

        /// <summary>
        /// True quando a camada UseItem está executando uma ação.
        /// Usado por Interaction para evitar conflito de animações de braço.
        /// </summary>
        public bool IsUsingItem { get; set; }

        // ── Dados de interação ───────────────────────────────────────────────

        /// <summary>
        /// Objeto interativo mais próximo dentro do raio de interação.
        /// Null se não houver nenhum.
        /// </summary>
        public IInteractable NearestInteractable { get; set; }

        /// <summary>
        /// Item mais próximo que pode ser coletado.
        /// Null se não houver nenhum.
        /// </summary>
        public IPickable NearestPickable { get; set; }

        // ── Dados do personagem atual ────────────────────────────────────────

        /// <summary>
        /// Dados configuráveis do personagem ativo (stats, velocidade, etc.).
        /// Trocado pelo CharacterSwitcher ao alternar personagens.
        /// </summary>
        public PlayableCharacterData ActiveCharacterData { get; set; }
    }
}
