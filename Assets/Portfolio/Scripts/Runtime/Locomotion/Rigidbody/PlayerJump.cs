using UnityEngine;

namespace Portfolio.Locomotion.Rigidbody
{
    /// <summary>
    /// Coordinates the Rigidbody jump module with the player physics update.
    /// Jump buffering, coyote time and jump velocity remain implemented by RigidbodyJump.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerJump : MonoBehaviour
    {
        public void UpdateJumpState(RigidbodyJump jump, UnityEngine.Rigidbody2D body, Portfolio.Locomotion.Core.LocomotionState state)
        {
            if (jump != null && body != null && state != null)
                jump.UpdateRuntimeState(body, state);
        }

        public bool TryApplyJump(
            RigidbodyJump jump,
            UnityEngine.Rigidbody2D body,
            Portfolio.Locomotion.Core.LocomotionState state,
            float steepSlopeBlockDirection)
        {
            return jump != null && body != null && state != null &&
                   jump.ApplyJump(body, state, steepSlopeBlockDirection);
        }

        public void RequestJump(RigidbodyJump jump)
        {
            if (jump != null)
                jump.RequestJump();
        }

        public void ReleaseJump(RigidbodyJump jump, UnityEngine.Rigidbody2D body)
        {
            if (jump != null && body != null)
                jump.ReleaseJump(body);
        }
    }
}
