using Portfolio.Locomotion.Core;
using UnityEngine;

namespace Portfolio.Locomotion.Rigidbody
{
    /// <summary>
    /// Owns Rigidbody locomotion's ground probing, gravity and ground-adhesion decisions.
    /// The existing serialized tuning values remain on RigidbodyLocomotionController for
    /// scene/prefab compatibility and are passed into these operations.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerPhysics : MonoBehaviour
    {
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

            surfaceProbe.Probe(capsule, body, state, jump.ShouldIgnoreGrounding);
            jump.SetSteepSlopeJumpRestriction(surfaceProbe.IsSteepSlopeJumpRestricted);
            return state.Grounded;
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
