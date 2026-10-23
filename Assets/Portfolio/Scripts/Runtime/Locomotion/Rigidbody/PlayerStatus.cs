using Portfolio.Locomotion.Core;

namespace Portfolio.Locomotion.Rigidbody
{
    /// <summary>
    /// Runtime locomotion status data. This is deliberately not a MonoBehaviour;
    /// the controller owns it and the custom Inspector reads it through the controller.
    /// </summary>
    [System.Serializable]
    public sealed class PlayerStatus
    {
        private LocomotionState _state;

        public LocomotionState State => _state;
        public bool IsGrounded => _state != null && _state.Grounded;
        public bool IsRising => _state != null && _state.Rising;
        public bool IsFalling => _state != null && _state.Falling;
        public bool IsOnWalkableSlope => _state != null && _state.OnWalkableSlope;
        public bool IsOnSteepSlope => _state != null && _state.OnSteepSlope;
        public bool IsWalking => _state != null && _state.IsWalking;
        public bool IsJumping => _state != null && _state.IsJumping;

        public PlayerStatus(LocomotionState state)
        {
            _state = state ?? new LocomotionState();
        }

        public void Initialize(LocomotionState existingState)
        {
            if (existingState != null)
                _state = existingState;
            else if (_state == null)
                _state = new LocomotionState();
        }

        public void SetControlEnabled(bool enabled) => _state?.SetControlEnabled(enabled);
        public void ResetRuntimeState() => _state?.ResetRuntimeState();
    }
}
