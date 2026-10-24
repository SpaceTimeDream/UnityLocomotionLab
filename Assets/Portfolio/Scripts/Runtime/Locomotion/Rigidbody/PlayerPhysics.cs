using Portfolio.Locomotion.Core;
using UnityEngine;

namespace Portfolio.Locomotion.Rigidbody
{
    /// <summary>
    /// Owns Rigidbody locomotion's ground probing, gravity and ground-adhesion decisions.
    /// The existing serialized tuning values remain on PlayerController for
    /// scene/prefab compatibility and are passed into these operations.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerPhysics : MonoBehaviour
    {
        private PlayerController _controller;

        public void Initialize(PlayerController controller) => _controller = controller;

        public void ApplyPlatformDelta()
        {
            if (_controller == null) return;
            _controller.BeginPhysicsStep();
            _controller.MovingPlatforms.ApplyPlatformMotion(
                _controller.Body, _controller.Jump.IsInJumpAscentState);
        }

        public void UpdatePhysics()
        {
            if (_controller == null) return;
            CheckGrounded(
                _controller.MainCollider, _controller.Body, _controller.State,
                _controller.SurfaceProbe, _controller.Jump);
            _controller.PlayerJump.UpdateJumpState(
                _controller.Jump, _controller.Body, _controller.State);
            _controller.MovingPlatforms.Track(
                _controller.State, _controller.SurfaceProbe.SurfaceCollider, _controller.Body);
            _controller.RefreshSteepSlopeBlockDirection();
            _controller.State.SetSliding(false);
        }

        public void FinalizePlatformSync()
        {
            if (_controller == null) return;
            _controller.ApplyGravity();
            _controller.ApplyGroundAdhesion();

            bool hadExternalForce = _controller.ExternalForces.HasExternalForce;
            _controller.ExternalForces.UpdateRuntimeState(_controller.Body, _controller.State);
            if (hadExternalForce && !_controller.ExternalForces.HasExternalForce)
                _controller.Movement.ResetRuntimeState();

            _controller.MovingPlatforms.FinalizePlatformSync(_controller.Body);
            _controller.State.SetVerticalMotion(_controller.Body.linearVelocity.y);
            _controller.State.RefreshPhysicsState(
                _controller.MovementInput,
                _controller.Body.linearVelocity,
                _controller.Jump.IsInAnticipationFrame || _controller.Jump.IsInJumpAscentState,
                _controller.ExternalForces.HasExternalForce);
            _controller.SyncGlobalPhysicsStatus();
            _controller.CompletePhysicsStepDiagnostics();
        }
        /// <summary>
        /// Performs the ground check and returns the accepted grounded state.
        /// SurfaceProbe2D performs the filtered Ground-layer contacts/raycasts/capsule cast
        /// and writes the result into LocomotionState; a raw physics hit alone is not enough.
        /// </summary>
        public bool CheckGrounded(
            CapsuleCollider2D capsule,
            UnityEngine.Rigidbody2D body,
            LocomotionState state,
            SurfaceProbe2D surfaceProbe,
            RigidbodyJump jump)
        {
            if (capsule == null || body == null || state == null || surfaceProbe == null || jump == null)
                return false;

            bool grounded = surfaceProbe.CheckGrounded(capsule, body, state, jump.ShouldIgnoreGrounding);
            jump.SetSteepSlopeJumpRestriction(surfaceProbe.IsSteepSlopeJumpRestricted);
            return grounded;
        }

        public void ApplyGravity(
            UnityEngine.Rigidbody2D body,
            float gravityScale,
            float jumpAscentGravityMultiplier,
            float fallGravityMultiplier,
            float maximumFallSpeed)
        {
            if (body == null)
                return;

            float multiplier = body.linearVelocity.y > 0f
                ? jumpAscentGravityMultiplier
                : fallGravityMultiplier;
            body.gravityScale = gravityScale * multiplier;

            if (body.linearVelocity.y >= -maximumFallSpeed)
                return;

            Vector2 velocity = body.linearVelocity;
            velocity.y = -maximumFallSpeed;
            body.linearVelocity = velocity;
        }

        public string GetGroundAdhesionDecision(
            UnityEngine.Rigidbody2D body,
            LocomotionState state,
            SurfaceProbe2D surfaceProbe,
            RigidbodyJump jump,
            ExternalForceReceiver2D externalForces,
            float adhesionForce,
            out bool applied)
        {
            applied = false;

            if (body == null || state == null || surfaceProbe == null || jump == null || externalForces == null)
                return "Skipped: missing physics dependency";
            if (adhesionForce <= 0f)
                return "Skipped: force is zero";
            if (!state.Grounded)
                return "Skipped: not grounded";
            if (!surfaceProbe.HasDirectSurface)
                return "Skipped: no direct surface (grace grounding only)";
            if (jump.IsInJumpAscentState)
                return "Skipped: jump ascent";
            if (externalForces.HasExternalForce)
                return "Skipped: external force active";

            Vector2 surfaceNormal = surfaceProbe.SurfaceNormal.normalized;
            float normalVelocity = Vector2.Dot(body.linearVelocity, surfaceNormal);
            float slopeAngle = Vector2.Angle(surfaceNormal, Vector2.up);

            if (surfaceNormal.y <= 0f)
                return "Skipped: surface normal is not upward";
            if (slopeAngle > surfaceProbe.MaximumWalkableSlopeAngle)
                return "Skipped: slope exceeds walkable angle";
            if (normalVelocity > 0.1f)
                return "Skipped: moving away from surface";

            body.AddForce(-surfaceNormal * adhesionForce, ForceMode2D.Force);
            applied = true;
            return "Applied";
        }
    }
}
