// =============================================================================
// LocomotionStates.cs
// Versão NavMeshService.
//
// MUDANÇAS IMPORTANTES:
//
//   - CharacterController removido
//   - NavMeshAgent movimenta o personagem
//   - Velocity vertical manual apenas para pulo
//   - Movimento horizontal via SetDestination
//   - Controle de velocidade via SetSpeed()
// =============================================================================

using UnityEngine;
using CharacterSystem.Core;

namespace CharacterSystem.Layers.Locomotion
{
    // ══════════════════════════════════════════════════════════════════════════
    // IdleState
    // ══════════════════════════════════════════════════════════════════════════

    public class IdleState : ICharacterState
    {
        public string StateName => "Locomotion.Idle";

        public void OnEnter(PlayerContext ctx)
        {
            ctx.NavMeshService.Stop();

            ctx.AnimationBridge.SetMoveSpeed(0f);
            ctx.AnimationBridge.SetCrouching(false);
        }

        public void OnUpdate(PlayerContext ctx)
        {
            ctx.AnimationBridge.SetMoveSpeed(0f);
        }

        public void OnExit(PlayerContext ctx)
        {
        }

        public bool CanTransitionTo(ICharacterState next, PlayerContext ctx)
        {
            return true;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // MovingState
    // ══════════════════════════════════════════════════════════════════════════

    public class MovingState : ICharacterState
    {
        private const float DestinationDistance = 2f;

        public string StateName => "Locomotion.Moving";

        public void OnEnter(PlayerContext ctx)
        {
            var data = ctx.ActiveCharacterData;

            ctx.NavMeshService.Resume();
            ctx.NavMeshService.SetSpeed(data.RunSpeed);
            ctx.NavMeshService.EnableRotation(true);
        }

        public void OnUpdate(PlayerContext ctx)
        {
            var data = ctx.ActiveCharacterData;

            Vector3 moveDir = CalculateMoveDirection(ctx);

            if (moveDir.sqrMagnitude <= 0.001f)
            {
                ctx.AnimationBridge.SetMoveSpeed(0f);
                return;
            }

            Vector3 destination =
                ctx.Agent.transform.position +
                moveDir * DestinationDistance;

            ctx.NavMeshService.SetDestination(destination);

            Vector3 velocity =
                ctx.NavMeshService.GetCurrentVelocity();

            float normalized =
                velocity.magnitude / data.RunSpeed;

            ctx.AnimationBridge.SetMoveSpeed(
                Mathf.Clamp01(normalized));
        }

        public void OnExit(PlayerContext ctx)
        {
            ctx.NavMeshService.ClearPath();
        }

        public bool CanTransitionTo(ICharacterState next, PlayerContext ctx)
        {
            return true;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static Vector3 CalculateMoveDirection(PlayerContext ctx)
        {
            var cam   = ctx.CameraTransform;
            var input = ctx.MoveInput;

            Vector3 forward =
                Vector3.ProjectOnPlane(
                    cam.forward,
                    Vector3.up).normalized;

            Vector3 right =
                Vector3.ProjectOnPlane(
                    cam.right,
                    Vector3.up).normalized;

            return (forward * input.y + right * input.x).normalized;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // JumpingState
    // ══════════════════════════════════════════════════════════════════════════

    public class JumpingState : ICharacterState
    {
        public string StateName => "Locomotion.Jumping";

        private float _verticalVelocity;

        public void OnEnter(PlayerContext ctx)
        {
            _verticalVelocity =
                ctx.ActiveCharacterData.JumpForce;

            ctx.AnimationBridge.PlayJump();

            // Durante pulo:
            // desabilitamos atualização de posição do agent
            ctx.NavMeshService.EnablePositionUpdate(false);
        }

        public void OnUpdate(PlayerContext ctx)
        {
            var data = ctx.ActiveCharacterData;

            Vector3 moveDir = CalculateMoveDirection(ctx);

            Vector3 horizontal =
                moveDir * (data.RunSpeed * 0.8f);

            // gravidade
            _verticalVelocity +=
                Physics.gravity.y *
                data.GravityMultiplier *
                Time.deltaTime;

            Vector3 finalVelocity =
                horizontal +
                Vector3.up * _verticalVelocity;

            ctx.Agent.transform.position +=
                finalVelocity * Time.deltaTime;

            // animação
            float horizontalSpeed =
                new Vector2(
                    horizontal.x,
                    horizontal.z).magnitude;

            ctx.AnimationBridge.SetMoveSpeed(
                Mathf.Clamp01(horizontalSpeed / data.RunSpeed));
        }

        public void OnExit(PlayerContext ctx)
        {
            ctx.AnimationBridge.PlayLand();

            ctx.NavMeshService.Warp(ctx.Agent.transform.position);

            ctx.NavMeshService.EnablePositionUpdate(true);
        }

        public bool CanTransitionTo(ICharacterState next, PlayerContext ctx)
        {
            return next is IdleState or MovingState;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static Vector3 CalculateMoveDirection(PlayerContext ctx)
        {
            var cam   = ctx.CameraTransform;
            var input = ctx.MoveInput;

            Vector3 forward =
                Vector3.ProjectOnPlane(
                    cam.forward,
                    Vector3.up).normalized;

            Vector3 right =
                Vector3.ProjectOnPlane(
                    cam.right,
                    Vector3.up).normalized;

            return (forward * input.y + right * input.x).normalized;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // CrouchingState
    // ══════════════════════════════════════════════════════════════════════════

    public class CrouchingState : ICharacterState
    {
        private const float CrouchSpeedMultiplier = 0.45f;
        private const float DestinationDistance   = 1.5f;

        public string StateName => "Locomotion.Crouching";

        public void OnEnter(PlayerContext ctx)
        {
            ctx.IsCrouching = true;

            ctx.AnimationBridge.SetCrouching(true);

            ctx.NavMeshService.SetSpeed(
                ctx.ActiveCharacterData.WalkSpeed
                * CrouchSpeedMultiplier);
        }

        public void OnUpdate(PlayerContext ctx)
        {
            var data = ctx.ActiveCharacterData;

            Vector3 moveDir = CalculateMoveDirection(ctx);

            if (moveDir.sqrMagnitude <= 0.001f)
            {
                ctx.AnimationBridge.SetMoveSpeed(0f);
                return;
            }

            Vector3 destination =
                ctx.Agent.transform.position +
                moveDir * DestinationDistance;

            ctx.NavMeshService.SetDestination(destination);

            float normalized =
                ctx.NavMeshService.GetCurrentVelocity().magnitude
                / data.WalkSpeed;

            ctx.AnimationBridge.SetMoveSpeed(
                Mathf.Clamp01(normalized) * 0.5f);
        }

        public void OnExit(PlayerContext ctx)
        {
            ctx.IsCrouching = false;

            ctx.AnimationBridge.SetCrouching(false);

            ctx.NavMeshService.ClearPath();
        }

        public bool CanTransitionTo(ICharacterState next, PlayerContext ctx)
        {
            if (next is JumpingState)
                return false;

            return true;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static Vector3 CalculateMoveDirection(PlayerContext ctx)
        {
            var cam   = ctx.CameraTransform;
            var input = ctx.MoveInput;

            Vector3 forward =
                Vector3.ProjectOnPlane(
                    cam.forward,
                    Vector3.up).normalized;

            Vector3 right =
                Vector3.ProjectOnPlane(
                    cam.right,
                    Vector3.up).normalized;

            return (forward * input.y + right * input.x).normalized;
        }
    }
}