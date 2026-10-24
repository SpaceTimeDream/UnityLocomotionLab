using UnityEngine;

namespace Portfolio.Locomotion.Rigidbody
{
    /// <summary>
    /// Resolves movement-specific direction decisions independently from the controller's
    /// fixed-step orchestration.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerMovement : MonoBehaviour
    {
        private PlayerController _controller;

        public void Initialize(PlayerController controller) => _controller = controller;
        public void UpdateMovement() => _controller?.StageUpdateMovement();
        public float ResolveSteepSlopeBlockDirection(
            float movementInput,
            float facingDirection,
            UnityEngine.Rigidbody2D body,
            CapsuleCollider2D capsule,
            SurfaceProbe2D surfaceProbe,
            float steepSlopeCheckDistance)
        {
            if (body == null || capsule == null || surfaceProbe == null)
                return 0f;

            float inputDirection = GetDirection(movementInput);
            if (HasNearbySteepSlope(surfaceProbe, capsule, inputDirection, steepSlopeCheckDistance))
                return inputDirection;

            float velocityDirection = GetDirection(body.linearVelocity.x);
            if (!Mathf.Approximately(velocityDirection, inputDirection) &&
                HasNearbySteepSlope(surfaceProbe, capsule, velocityDirection, steepSlopeCheckDistance))
                return velocityDirection;

            if (Mathf.Approximately(inputDirection, 0f) &&
                Mathf.Approximately(velocityDirection, 0f) &&
                HasNearbySteepSlope(surfaceProbe, capsule, facingDirection, steepSlopeCheckDistance))
                return facingDirection;

            return 0f;
        }

        public float ResolveFacingDirection(
            float movementInput,
            UnityEngine.Rigidbody2D body,
            ExternalForceReceiver2D externalForces,
            float currentFacingDirection)
        {
            if (body == null)
                return currentFacingDirection;

            float direction = movementInput;
            if (externalForces != null && externalForces.IsControlProtected &&
                Mathf.Abs(body.linearVelocity.x) > 0.01f)
                direction = body.linearVelocity.x;

            return Mathf.Abs(direction) > 0.01f ? Mathf.Sign(direction) : currentFacingDirection;
        }

        private static bool HasNearbySteepSlope(
            SurfaceProbe2D surfaceProbe,
            CapsuleCollider2D capsule,
            float direction,
            float distance)
        {
            if (Mathf.Approximately(direction, 0f))
                return false;

            return surfaceProbe.HasNearbySteepSlope(capsule, direction, distance);
        }

        private static float GetDirection(float value)
        {
            return Mathf.Abs(value) > 0.01f ? Mathf.Sign(value) : 0f;
        }
    }
}
