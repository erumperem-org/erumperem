// =============================================================================
// LocomotionLayer.cs
// Versão convertida para NavMeshService.
//
// MUDANÇAS PRINCIPAIS:
//   - Remove CharacterController.Move()
//   - Usa INavMeshService para movimentação
//   - Ground check agora usa NavMeshAgent.isOnNavMesh + Physics
//   - Estados manipulam SetDestination / SetVelocity
//   - Rotação delegada ao NavMeshAgent
//
// OBS:
//   O NavMeshAgent deve estar com:
//      updateRotation = true
//      updatePosition = true
// =============================================================================

using UnityEngine;
using CharacterSystem.Core;
using CharacterSystem.StateMachine;

namespace CharacterSystem.Layers.Locomotion
{
    public class LocomotionLayer
    {
        // ── Estados ──────────────────────────────────────────────────────────

        private readonly IdleState      _idle      = new();
        private readonly MovingState    _moving    = new();
        private readonly JumpingState   _jumping   = new();
        private readonly CrouchingState _crouching = new();

        // ── State Machine ────────────────────────────────────────────────────

        private readonly StateLayer _layer;

        // ── Constantes ───────────────────────────────────────────────────────

        private const float GroundCheckDistance = 0.3f;
        private const float GroundCheckRadius   = 0.25f;

        // ── Construtor ───────────────────────────────────────────────────────

        public LocomotionLayer()
        {
            _layer = new StateLayer("Locomotion", _idle);
        }

        // ── API Pública ──────────────────────────────────────────────────────

        public void Initialize(PlayerContext ctx)
        {
            _layer.Initialize(ctx);
        }

        public void Update(PlayerContext ctx)
        {
            ctx.IsGrounded = CheckGrounded(ctx);

            EvaluateTransitions(ctx);

            _layer.Update(ctx);
        }

        // ── Transições ───────────────────────────────────────────────────────

        private void EvaluateTransitions(PlayerContext ctx)
        {
            var current = _layer.CurrentState;

            // LAND
            if (current is JumpingState && ctx.IsGrounded)
            {
                bool hasInput = ctx.MoveInput.sqrMagnitude > 0.01f;

                _layer.TryTransition(
                    hasInput ? _moving : _idle,
                    ctx);

                return;
            }

            // JUMP
            if (ctx.JumpPressed &&
                ctx.IsGrounded &&
                current is not CrouchingState)
            {
                _layer.TryTransition(_jumping, ctx);
                return;
            }

            // CROUCH
            if (ctx.CrouchHeld &&
                current is not CrouchingState &&
                current is not JumpingState)
            {
                _layer.TryTransition(_crouching, ctx);
                return;
            }

            if (!ctx.CrouchHeld &&
                current is CrouchingState)
            {
                _layer.TryTransition(_idle, ctx);
                return;
            }

            // MOVE
            if (current is not JumpingState)
            {
                bool hasInput = ctx.MoveInput.sqrMagnitude > 0.01f;

                if (hasInput && current is IdleState)
                    _layer.TryTransition(_moving, ctx);

                if (!hasInput && current is MovingState)
                    _layer.TryTransition(_idle, ctx);
            }
        }

        // ── Ground Check ─────────────────────────────────────────────────────

        private static bool CheckGrounded(PlayerContext ctx)
        {
            if (!ctx.NavMeshService.IsOnNavMesh())
                return false;

            var transform = ctx.Agent.transform;

            Vector3 origin =
                transform.position + Vector3.up * 0.2f;

            return Physics.SphereCast(
                origin,
                GroundCheckRadius,
                Vector3.down,
                out _,
                GroundCheckDistance,
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore);
        }
    }
}