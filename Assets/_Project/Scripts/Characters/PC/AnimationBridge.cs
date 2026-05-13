// =============================================================================
// AnimationBridge.cs
// Implementação concreta de IAnimationBridge.
// É o ÚNICO lugar no projeto que chama métodos do Animator diretamente.
//
// RESPONSABILIDADE ÚNICA:
//   Traduzir comandos semânticos ("PlayJump") em chamadas ao Animator
//   ("SetTrigger(AnimatorParameters.Jump)"). Não contém lógica de gameplay.
//
// TROCA DE PERSONAGEM:
//   SwapAnimator() é chamado pelo CharacterSwitcher. O bridge troca o
//   RuntimeAnimatorController mantendo todos os parâmetros sincronizados,
//   pois os hashes são iguais entre os 3 controllers.
// =============================================================================

using UnityEngine;
using CharacterSystem.Core;

namespace CharacterSystem.Animation
{
    /// <summary>
    /// Ponte entre a lógica de gameplay e o Animator Controller da Unity.
    /// Componente MonoBehaviour anexado ao mesmo GameObject do Animator.
    /// </summary>
    //[RequireComponent(typeof(Animator))]
    public class AnimationBridge : MonoBehaviour, IAnimationBridge
    {
        // ── Campos privados ──────────────────────────────────────────────────

        [SerializeField] private Animator _animator;

        // Cache dos últimos valores para evitar set desnecessário todo frame
        private float _lastMoveSpeed = -1f;
        private bool _lastIsCrouching;
        private bool _lastItemActive;

        // ── Unity Lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            _animator = GetComponentInChildren<Animator>();
        }

        // ── ILocomotionAnimations ────────────────────────────────────────────

        /// <inheritdoc/>
        public void SetMoveSpeed(float normalizedSpeed)
        {
            // Evita enviar o mesmo valor todo frame (micro-otimização)
            if (Mathf.Approximately(normalizedSpeed, _lastMoveSpeed)) return;
            _lastMoveSpeed = normalizedSpeed;
            _animator.SetFloat(AnimatorParameters.MoveSpeed, normalizedSpeed);
        }

        /// <inheritdoc/>
        public void PlayJump()
        {
            _animator.SetBool(AnimatorParameters.IsAirborne, true);
            _animator.SetTrigger(AnimatorParameters.Jump);
        }

        /// <inheritdoc/>
        public void PlayLand()
        {
            _animator.SetBool(AnimatorParameters.IsAirborne, false);
            _animator.SetTrigger(AnimatorParameters.Land);
        }

        /// <inheritdoc/>
        public void SetCrouching(bool isCrouching)
        {
            if (isCrouching == _lastIsCrouching) return;
            _lastIsCrouching = isCrouching;
            _animator.SetBool(AnimatorParameters.IsCrouching, isCrouching);
        }

        // ── IInteractionAnimations ───────────────────────────────────────────

        /// <inheritdoc/>
        public void PlayPickup()
        {
            _animator.SetBool(AnimatorParameters.IsInteracting, true);

            for (int i = 0; i < _animator.layerCount; i++)
            {
                _animator.Play("Pickup", i, 0f);
            }

            _animator.SetTrigger(AnimatorParameters.Pickup);
        }

        /// <inheritdoc/>
        public void PlayOpenDoor()
        {
            _animator.SetBool(AnimatorParameters.IsInteracting, true);
            _animator.SetTrigger(AnimatorParameters.OpenDoor);
        }

        /// <inheritdoc/>
        public void PlayInteractionIdle()
        {
            _animator.SetBool(AnimatorParameters.IsInteracting, false);
        }

        // ── IUseItemAnimations ───────────────────────────────────────────────

        /// <inheritdoc/>
        public void SetItemActive(bool active)
        {
            if (active == _lastItemActive) return;
            _lastItemActive = active;
            _animator.SetBool(AnimatorParameters.ItemActive, active);
        }

        /// <inheritdoc/>
        public void PlayUseItem()
        {
            _animator.SetTrigger(AnimatorParameters.UseItem);
        }

        // ── IAnimationBridge ─────────────────────────────────────────────────

        /// <inheritdoc/>
        public void SwapAnimator(RuntimeAnimatorController controller)
        {
            if (controller == null)
            {
                Debug.LogWarning("[AnimationBridge] SwapAnimator recebeu controller nulo.");
                return;
            }

            // Preserva valores atuais antes da troca
            float currentSpeed = _animator.GetFloat(AnimatorParameters.MoveSpeed);
            bool currentCrouch = _animator.GetBool(AnimatorParameters.IsCrouching);
            bool currentItem = _animator.GetBool(AnimatorParameters.ItemActive);
            bool currentAirborne = _animator.GetBool(AnimatorParameters.IsAirborne);

            // Troca o controller (redefine todos os parâmetros para default)
            _animator.runtimeAnimatorController = controller;

            // Reaplica os valores preservados para manter continuidade de estado
            _animator.SetFloat(AnimatorParameters.MoveSpeed, currentSpeed);
            _animator.SetBool(AnimatorParameters.IsCrouching, currentCrouch);
            _animator.SetBool(AnimatorParameters.ItemActive, currentItem);
            _animator.SetBool(AnimatorParameters.IsAirborne, currentAirborne);

            // Invalida cache para forçar sync no próximo frame
            _lastMoveSpeed = -1f;
            _lastIsCrouching = !currentCrouch;
            _lastItemActive = !currentItem;

            Debug.Log($"[AnimationBridge] AnimatorController trocado para: {controller.name}");
        }
    }
}
