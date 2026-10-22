using Portfolio.Locomotion.Core;
using UnityEngine;

namespace Portfolio.Locomotion.Rigidbody
{
    /// <summary>
    /// Per-player status component. Owns the runtime LocomotionState reference used by
    /// this Rigidbody player; deliberately avoids static/global status.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerStatus : MonoBehaviour
    {
        [SerializeField] private LocomotionState _state = new LocomotionState();

        public LocomotionState State => _state;
        public bool IsGrounded => _state != null && _state.Grounded;
        public bool IsRising => _state != null && _state.Rising;
        public bool IsFalling => _state != null && _state.Falling;
        public bool IsOnWalkableSlope => _state != null && _state.OnWalkableSlope;
        public bool IsOnSteepSlope => _state != null && _state.OnSteepSlope;

        public void Initialize(LocomotionState existingState)
        {
            // Preserve serialized state data from existing RigidbodyLocomotionController scenes.
            if (existingState != null)
                _state = existingState;

            if (_state == null)
                _state = new LocomotionState();
        }

        public void SetControlEnabled(bool enabled)
        {
            if (_state != null)
                _state.SetControlEnabled(enabled);
        }

        public void ResetRuntimeState()
        {
            if (_state != null)
                _state.ResetRuntimeState();
        }
    }
}
