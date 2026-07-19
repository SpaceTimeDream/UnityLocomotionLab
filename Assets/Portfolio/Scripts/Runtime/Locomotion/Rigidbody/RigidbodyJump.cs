using Portfolio.Locomotion.Core;
using UnityEngine;

namespace Portfolio.Locomotion.Rigidbody
{
    [System.Serializable]
    public class RigidbodyJump
    {
        // Fields
        [SerializeField] private float _jumpVelocity = 12f;
        [SerializeField] private float _jumpBufferTime = 0.12f;
        [SerializeField] private float _coyoteTime = 0.1f;
        [SerializeField] private bool _enableEarlyRelease = true;
        [SerializeField] private float _earlyReleaseVelocityMultiplier = 0.5f;

        private float _lastJumpRequestTime = -999f;
        private float _lastGroundedTime = -999f;
        private bool _jumpConsumed;
        private bool _jumpHeld;

        // Public Methods
        public void RequestJump()
        {
            _lastJumpRequestTime = Time.time;
            _jumpHeld = true;
        }

        public void ReleaseJump(UnityEngine.Rigidbody2D body)
        {
            _jumpHeld = false;

            if (!_enableEarlyRelease || body.velocity.y <= 0f)
            {
                return;
            }

            Vector2 velocity = body.velocity;
            velocity.y *= _earlyReleaseVelocityMultiplier;
            body.velocity = velocity;
        }

        public void TickGrounded(LocomotionState state)
        {
            if (!state.Grounded)
            {
                return;
            }

            _lastGroundedTime = Time.time;
            _jumpConsumed = false;
        }

        public void ApplyJump(UnityEngine.Rigidbody2D body, LocomotionState state)
        {
            bool hasBufferedJump = Time.time <= _lastJumpRequestTime + _jumpBufferTime;
            bool hasCoyoteTime = Time.time <= _lastGroundedTime + _coyoteTime;

            if (!hasBufferedJump || _jumpConsumed || !hasCoyoteTime || !state.ControlEnabled)
            {
                return;
            }

            Vector2 velocity = body.velocity;
            velocity.y = _jumpVelocity;
            body.velocity = velocity;
            _jumpConsumed = true;
            _lastJumpRequestTime = -999f;
            _lastGroundedTime = -999f;
        }

        public void ResetRuntimeState()
        {
            _lastJumpRequestTime = -999f;
            _lastGroundedTime = -999f;
            _jumpConsumed = false;
            _jumpHeld = false;
        }

        public void Validate()
        {
            _jumpVelocity = Mathf.Max(0f, _jumpVelocity);
            _jumpBufferTime = Mathf.Max(0f, _jumpBufferTime);
            _coyoteTime = Mathf.Max(0f, _coyoteTime);
            _earlyReleaseVelocityMultiplier = Mathf.Clamp01(_earlyReleaseVelocityMultiplier);
        }
    }
}
