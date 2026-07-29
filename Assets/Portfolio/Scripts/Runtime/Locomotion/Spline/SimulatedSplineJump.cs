using Portfolio.Locomotion.Core;
using UnityEngine;

namespace Portfolio.Locomotion.Spline
{
    [System.Serializable]
    public class SimulatedSplineJump
    {
        //------------------------------- Game Pause -------------------------------//

        //------------------------------- Debugging Methods -------------------------------//

        //------------------------------- Parameters -------------------------------//

        #region Components

        [Header("JUMP PARAMETERS")]
        [SerializeField] private float _jumpVelocity = 12f;
        [SerializeField] private float _maximumJumpVelocity = 12f;
        [SerializeField] private float _jumpBufferTime = 0.12f;
        [SerializeField] private float _coyoteTime = 0.1f;
        [SerializeField] private float _jumpAnticipationDuration = 0.08f;
        [SerializeField] private int _groundCheckDelayFixedFrames = 8;
        [SerializeField] private bool _enableEarlyRelease = true;
        [SerializeField] private float _earlyReleaseVelocityMultiplier = 0.5f;

        private float _jumpBufferRemainingTime;
        private float _coyoteRemainingTime;
        private float _anticipationRemainingTime;
        private int _groundCheckDelayFramesRemaining;
        private bool _jumpConsumed;
        private bool _jumpHeld;
        private bool _inAnticipationFrame;
        private bool _inJumpAscentState;
        private bool _jumpDisabled;
        private bool _steepSlopeJumpRestricted;

        public float JumpVelocity => _jumpVelocity;
        public float MaximumJumpVelocity => _maximumJumpVelocity;
        public float JumpBufferRemainingTime => _jumpBufferRemainingTime;
        public float CoyoteRemainingTime => _coyoteRemainingTime;
        public bool HasBufferedJump => _jumpBufferRemainingTime > 0f;
        public bool CanUseCoyoteTime => _coyoteRemainingTime > 0f;
        public bool IsInAnticipationFrame => _inAnticipationFrame;
        public bool IsInJumpAscentState => _inJumpAscentState;
        public bool IsJumpDisabled => _jumpDisabled;
        public bool IsSteepSlopeJumpRestricted => _steepSlopeJumpRestricted;
        public bool ShouldIgnoreGrounding => _groundCheckDelayFramesRemaining > 0;

        #endregion

        #region Flags

        #endregion

        //------------------------------- Unity Methods -------------------------------//

        #region Unity

        #endregion

        //------------------------------- Initialization Methods -------------------------------//

        #region Initialization

        #endregion

        //------------------------------- Core / Routines Methods -------------------------------//

        public void RequestJump()
        {
            _jumpHeld = true;
            _jumpBufferRemainingTime = _jumpBufferTime;
        }

        public void ReleaseJump(ref Vector2 velocity)
        {
            _jumpHeld = false;

            if (!_enableEarlyRelease || !_inJumpAscentState || velocity.y <= 0f)
            {
                return;
            }

            velocity.y *= _earlyReleaseVelocityMultiplier;
        }

        public void UpdateRuntimeState(Vector2 velocity, LocomotionState state, float deltaTime)
        {
            if (_groundCheckDelayFramesRemaining > 0)
            {
                _groundCheckDelayFramesRemaining--;
            }

            if (_jumpBufferRemainingTime > 0f && !_inAnticipationFrame)
            {
                _jumpBufferRemainingTime -= deltaTime;
            }

            if (state.Grounded && !_inAnticipationFrame && !_inJumpAscentState)
            {
                _coyoteRemainingTime = _coyoteTime;
                _jumpConsumed = false;
            }
            else
            {
                _coyoteRemainingTime = Mathf.Max(0f, _coyoteRemainingTime - deltaTime);
            }

            if (_inJumpAscentState && !_inAnticipationFrame && velocity.y <= 0f)
            {
                _inJumpAscentState = false;
            }
        }

        public bool ApplyJump(
            ref Vector2 velocity,
            LocomotionState state,
            bool blockHorizontalMovement,
            float deltaTime)
        {
            if (_inAnticipationFrame)
            {
                _anticipationRemainingTime -= deltaTime;

                if (_anticipationRemainingTime <= 0f)
                {
                    return ExecuteJump(ref velocity, state, blockHorizontalMovement);
                }

                return false;
            }

            if (!CanStartJump(state))
            {
                return false;
            }

            _jumpConsumed = true;
            _inAnticipationFrame = true;
            _inJumpAscentState = true;
            _anticipationRemainingTime = _jumpAnticipationDuration;
            _jumpBufferRemainingTime = 0f;

            if (_anticipationRemainingTime <= 0f)
            {
                return ExecuteJump(ref velocity, state, blockHorizontalMovement);
            }

            return false;
        }

        public void SetJumpDisabled(bool disabled)
        {
            _jumpDisabled = disabled;
        }

        public void SetSteepSlopeJumpRestriction(bool restricted)
        {
            _steepSlopeJumpRestricted = restricted;
        }

        public void ResetJumpBuffer()
        {
            _jumpBufferRemainingTime = 0f;
        }

        public void ResetRuntimeState()
        {
            _jumpBufferRemainingTime = 0f;
            _coyoteRemainingTime = 0f;
            _anticipationRemainingTime = 0f;
            _groundCheckDelayFramesRemaining = 0;
            _jumpConsumed = false;
            _jumpHeld = false;
            _inAnticipationFrame = false;
            _inJumpAscentState = false;
            _jumpDisabled = false;
            _steepSlopeJumpRestricted = false;
        }

        public void Validate()
        {
            _jumpVelocity = Mathf.Max(0f, _jumpVelocity);
            _maximumJumpVelocity = Mathf.Max(_jumpVelocity, _maximumJumpVelocity);
            _jumpBufferTime = Mathf.Max(0f, _jumpBufferTime);
            _coyoteTime = Mathf.Max(0f, _coyoteTime);
            _jumpAnticipationDuration = Mathf.Max(0f, _jumpAnticipationDuration);
            _groundCheckDelayFixedFrames = Mathf.Max(0, _groundCheckDelayFixedFrames);
            _earlyReleaseVelocityMultiplier = Mathf.Clamp01(_earlyReleaseVelocityMultiplier);
        }

        private bool CanStartJump(LocomotionState state)
        {
            if (!HasBufferedJump || _jumpConsumed || !state.ControlEnabled)
            {
                return false;
            }

            if (_jumpDisabled || _steepSlopeJumpRestricted)
            {
                return false;
            }

            return state.Grounded || CanUseCoyoteTime;
        }

        private bool ExecuteJump(
            ref Vector2 velocity,
            LocomotionState state,
            bool blockHorizontalMovement)
        {
            if (!state.ControlEnabled || _jumpDisabled || _steepSlopeJumpRestricted)
            {
                CancelAnticipation();
                return false;
            }

            if (blockHorizontalMovement)
            {
                velocity.x = 0f;
            }

            float baseVerticalVelocity = state.Grounded
                ? 0f
                : Mathf.Max(velocity.y, 0f);
            velocity.y = Mathf.Min(baseVerticalVelocity + _jumpVelocity, _maximumJumpVelocity);

            if (_enableEarlyRelease && !_jumpHeld)
            {
                velocity.y *= _earlyReleaseVelocityMultiplier;
            }

            state.SetSurface(false, false, false, Vector2.up, 0f);
            _groundCheckDelayFramesRemaining = _groundCheckDelayFixedFrames;
            _inAnticipationFrame = false;
            _inJumpAscentState = true;
            _anticipationRemainingTime = 0f;
            _coyoteRemainingTime = 0f;
            return true;
        }

        private void CancelAnticipation()
        {
            _inAnticipationFrame = false;
            _inJumpAscentState = false;
            _anticipationRemainingTime = 0f;
        }

#if UNITY_EDITOR
        //------------------------------- Editor -------------------------------//
#endif
    }
}
