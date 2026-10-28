using System;
using UnityEngine;

namespace Portfolio.Locomotion.Core
{
    [Flags]
    public enum LocomotionPhysicsState
    {
        None = 0,
        Idle = 1 << 0,
        Moving = 1 << 1,
        Walking = 1 << 2,
        Grounded = 1 << 3,
        Jumping = 1 << 4,
        Rising = 1 << 5,
        Falling = 1 << 6,
        Sliding = 1 << 7,
        Pushed = 1 << 8,
        OnSlope = 1 << 9,
        OnSteepSlope = 1 << 10
    }

    [Serializable]
    public class LocomotionState
    {
        //------------------------------- Parameters -------------------------------//

        #region Components

        [Header("STATE")]
        [SerializeField] private bool _controlEnabled = true;
        [SerializeField] private bool _grounded;
        [SerializeField] private bool _rising;
        [SerializeField] private bool _falling;
        [SerializeField] private bool _onWalkableSlope;
        [SerializeField] private bool _onSteepSlope;
        [SerializeField] private bool _sliding;
        [SerializeField] private Vector2 _surfaceNormal = Vector2.up;
        [SerializeField] private float _slopeAngle;
        [SerializeField] private LocomotionPhysicsState _currentState = LocomotionPhysicsState.Idle;

        #endregion

        #region Flags

        private int _transitionCount;

        #endregion

        #region Properties

        public bool ControlEnabled => _controlEnabled;
        public bool Grounded => _grounded;
        public bool Rising => _rising;
        public bool Falling => _falling;
        public bool OnWalkableSlope => _onWalkableSlope;
        public bool OnSteepSlope => _onSteepSlope;
        public bool Sliding => _sliding;
        public Vector2 SurfaceNormal => _surfaceNormal;
        public float SlopeAngle => _slopeAngle;
        public LocomotionPhysicsState CurrentState => _currentState;
        public int TransitionCount => _transitionCount;

        public bool IsIdle => HasState(LocomotionPhysicsState.Idle);
        public bool IsMoving => HasState(LocomotionPhysicsState.Moving);
        public bool IsWalking => HasState(LocomotionPhysicsState.Walking);
        public bool IsJumping => HasState(LocomotionPhysicsState.Jumping);
        public bool IsPushed => HasState(LocomotionPhysicsState.Pushed);

        public event Action<LocomotionPhysicsState> StateChanged;

        #endregion

        //------------------------------- Core / Routines Methods -------------------------------//

        public void SetControlEnabled(bool controlEnabled)
        {
            _controlEnabled = controlEnabled;
        }

        public void SetSurface(bool grounded, bool onWalkableSlope, bool onSteepSlope, Vector2 surfaceNormal, float slopeAngle)
        {
            _grounded = grounded;
            _onWalkableSlope = onWalkableSlope;
            _onSteepSlope = onSteepSlope;
            _surfaceNormal = surfaceNormal.sqrMagnitude > 0f ? surfaceNormal.normalized : Vector2.up;
            _slopeAngle = slopeAngle;
        }

        public void SetVerticalMotion(float velocityY)
        {
            _rising = !_grounded && velocityY > 0.01f;
            _falling = !_grounded && velocityY < -0.01f;
        }

        public void SetSliding(bool sliding)
        {
            _sliding = sliding;
        }

        public void RefreshPhysicsState(float movementInput, Vector2 velocity, bool jumpActive, bool externalForceActive)
        {
            LocomotionPhysicsState nextState = LocomotionPhysicsState.None;
            bool hasHorizontalMovement = Mathf.Abs(velocity.x) > 0.01f;
            bool walking = _grounded && hasHorizontalMovement && Mathf.Abs(movementInput) > 0.01f;

            SetFlag(ref nextState, LocomotionPhysicsState.Grounded, _grounded);
            SetFlag(ref nextState, LocomotionPhysicsState.Idle, _grounded && !walking && !hasHorizontalMovement);
            SetFlag(ref nextState, LocomotionPhysicsState.Moving, hasHorizontalMovement);
            SetFlag(ref nextState, LocomotionPhysicsState.Walking, walking);
            SetFlag(ref nextState, LocomotionPhysicsState.Jumping, jumpActive);
            SetFlag(ref nextState, LocomotionPhysicsState.Rising, _rising);
            SetFlag(ref nextState, LocomotionPhysicsState.Falling, _falling);
            SetFlag(ref nextState, LocomotionPhysicsState.Sliding, _sliding);
            SetFlag(ref nextState, LocomotionPhysicsState.Pushed, externalForceActive);
            SetFlag(ref nextState, LocomotionPhysicsState.OnSlope, _onWalkableSlope || _onSteepSlope);
            SetFlag(ref nextState, LocomotionPhysicsState.OnSteepSlope, _onSteepSlope);
            SetCurrentState(nextState);
        }

        public bool HasState(LocomotionPhysicsState state)
        {
            return (_currentState & state) == state;
        }

        public void ResetRuntimeState()
        {
            _grounded = false;
            _rising = false;
            _falling = false;
            _onWalkableSlope = false;
            _onSteepSlope = false;
            _sliding = false;
            _surfaceNormal = Vector2.up;
            _slopeAngle = 0f;
            SetCurrentState(LocomotionPhysicsState.Idle);
        }

        private void SetFlag(ref LocomotionPhysicsState state, LocomotionPhysicsState flag, bool active)
        {
            if (active)
            {
                state |= flag;
            }
        }

        private void SetCurrentState(LocomotionPhysicsState state)
        {
            if (_currentState == state)
            {
                return;
            }

            _currentState = state;
            _transitionCount++;
            StateChanged?.Invoke(_currentState);
        }

    }
}
