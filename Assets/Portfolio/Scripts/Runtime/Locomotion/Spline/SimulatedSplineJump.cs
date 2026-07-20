using UnityEngine;

namespace Portfolio.Locomotion.Spline
{
    public class SimulatedSplineJump : MonoBehaviour
    {
        // Fields
        [SerializeField] private float _jumpSpeed = 11f;
        [SerializeField] private float _jumpBufferTime = 0.12f;
        [SerializeField] private float _coyoteTime = 0.1f;
        [SerializeField, Range(0f, 1f)] private float _earlyReleaseVelocityScale = 0.45f;

        private float _lastJumpPressedTime = -100f;
        private float _lastGroundedTime = -100f;
        private bool _jumpConsumed;
        private bool _releaseQueued;

        // Public Methods
        public void NotifyGrounded(bool grounded)
        {
            if (grounded)
            {
                _lastGroundedTime = Time.time;
                _jumpConsumed = false;
            }
        }

        public void RequestJump()
        {
            _lastJumpPressedTime = Time.time;
        }

        public void ReleaseJump()
        {
            _releaseQueued = true;
        }

        public bool CanJump()
        {
            return !_jumpConsumed && Time.time - _lastJumpPressedTime <= _jumpBufferTime && Time.time - _lastGroundedTime <= _coyoteTime;
        }

        public void ConsumeJump(SplineLocomotionState state)
        {
            Vector2 momentum = state.SurfaceTangent * state.TangentialGroundSpeed;
            state.AirborneVelocity = momentum + state.SurfaceNormal * _jumpSpeed;
            state.Grounded = false;
            state.Rising = true;
            state.Falling = false;
            _jumpConsumed = true;
            _lastJumpPressedTime = -100f;
        }

        public void ApplyEarlyRelease(SplineLocomotionState state)
        {
            if (!_releaseQueued)
            {
                return;
            }

            _releaseQueued = false;
            if (state.AirborneVelocity.y <= 0f)
            {
                return;
            }

            state.AirborneVelocity = new Vector2(state.AirborneVelocity.x, state.AirborneVelocity.y * _earlyReleaseVelocityScale);
        }

        public bool HasBufferedJump()
        {
            return Time.time - _lastJumpPressedTime <= _jumpBufferTime;
        }

        public void ResetRuntime()
        {
            _lastJumpPressedTime = -100f;
            _lastGroundedTime = -100f;
            _jumpConsumed = false;
            _releaseQueued = false;
        }
    }
}
