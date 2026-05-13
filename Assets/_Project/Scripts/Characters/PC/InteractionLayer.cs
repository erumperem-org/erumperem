// =============================================================================
// InteractionLayer.cs
// Camada Interaction da LSM. Gerencia as interações com objetos do mundo:
//   - Pegar itens do chão (PickupState)
//   - Abrir portas (OpenDoorState)
//
// DESIGN:
//   Esta camada roda em paralelo com Locomotion — o personagem PODE caminhar
//   enquanto a câmera detecta objetos interativos. A animação de braço é
//   separada via AvatarMask no Animator (Layer 1 → upper body).
//
//   A exclusão com UseItem é feita via flags no PlayerContext:
//     IsInteracting e IsUsingItem impedem conflito de animações de braço.
//
// DETECÇÃO:
//   Um SphereCast a cada frame detecta o IInteractable mais próximo e escreve
//   em ctx.NearestInteractable. O estado InteractionIdle lê isso para habilitar
//   o prompt de interação na UI (não implementado aqui — use o evento do context).
// =============================================================================

using UnityEngine;
using CharacterSystem.Core;
using CharacterSystem.StateMachine;

namespace CharacterSystem.Layers.Interaction
{
    /// <summary>
    /// Camada que controla interações com o mundo (pegar, abrir porta).
    /// Corresponde ao Animator Layer 1 (upper body mask).
    /// </summary>
    public class InteractionLayer
    {
        // ── Estados ──────────────────────────────────────────────────────────

        private readonly InteractionIdleState _idle     = new();
        private readonly PickupState          _pickup   = new();
        private readonly OpenDoorState        _openDoor = new();

        // ── State Machine interna ────────────────────────────────────────────

        private readonly StateLayer _layer;

        // ── Construtor ───────────────────────────────────────────────────────

        public InteractionLayer()
        {
            _layer = new StateLayer("Interaction", _idle);
        }

        // ── API Pública ──────────────────────────────────────────────────────

        /// <summary>Inicializa a camada. Chamado pelo PlayerController no Start.</summary>
        public void Initialize(PlayerContext ctx) => _layer.Initialize(ctx);

        /// <summary>
        /// Processa a lógica de interação a cada frame.
        /// Deve ser chamado pelo PlayerController.Update() após Locomotion.
        /// </summary>
        public void Update(PlayerContext ctx)
        {
            // 1. Detecta objetos interativos no raio do personagem
            DetectInteractables(ctx);

            // 2. Avalia transições baseado em input e estado do contexto
            EvaluateTransitions(ctx);

            // 3. Executa lógica do estado atual
            _layer.Update(ctx);
        }

        // ── Lógica de Transição ──────────────────────────────────────────────

        /// <summary>
        /// Avalia se uma interação deve ser iniciada ou encerrada.
        /// </summary>
        private void EvaluateTransitions(PlayerContext ctx)
        {
            var current = _layer.CurrentState;

            // Retornar ao idle quando a ação de interação terminar
            // (controlado pelo flag IsInteracting, baixado no OnExit de cada estado)
            if (current is PickupState or OpenDoorState && !ctx.IsInteracting)
            {
                _layer.TryTransition(_idle, ctx);
                return;
            }

            // Iniciar interação ao pressionar o botão (apenas se idle)
            if (ctx.InteractPressed && current is InteractionIdleState)
            {
                // UseItem ocupando os braços? Bloqueia.
                if (ctx.IsUsingItem) return;

                // Decide qual estado iniciar baseado no tipo do interatável
                if (ctx.NearestInteractable != null)
                {
                    var nextState = ctx.NearestInteractable.InteractionType switch
                    {
                        InteractionType.OpenDoor => (ICharacterState)_openDoor,
                        InteractionType.Pickup   => _pickup,
                        _                        => null
                    };

                    if (nextState != null)
                        _layer.TryTransition(nextState, ctx);
                }
                else if (ctx.NearestPickable != null)
                {
                    _layer.TryTransition(_pickup, ctx);
                }
            }
        }

        // ── Detecção de Interatáveis ─────────────────────────────────────────

        /// <summary>
        /// Detecta o IInteractable e IPickable mais próximos via OverlapSphere.
        /// Escreve os resultados no PlayerContext para que os estados e a UI possam ler.
        /// </summary>
        private static void DetectInteractables(PlayerContext ctx)
        {
            float radius = ctx.ActiveCharacterData.InteractionRadius;
            var position = ctx.Agent.transform.position;

            // Reset antes da detecção
            ctx.NearestInteractable = null;
            ctx.NearestPickable     = null;

            // OverlapSphere é eficiente para raios pequenos (<5m)
            var colliders = Physics.OverlapSphere(position, radius);

            float bestInteractDist = float.MaxValue;
            float bestPickDist     = float.MaxValue;

            foreach (var col in colliders)
            {
                float dist = Vector3.Distance(position, col.transform.position);

                // Prioriza o mais próximo de cada tipo
                if (col.TryGetComponent<IInteractable>(out var interactable)
                    && dist < bestInteractDist)
                {
                    ctx.NearestInteractable = interactable;
                    bestInteractDist = dist;
                }
                else if (col.TryGetComponent<IPickable>(out var pickable)
                         && dist < bestPickDist)
                {
                    ctx.NearestPickable = pickable;
                    bestPickDist = dist;
                }
            }
        }
    }
}
