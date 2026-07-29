using Portfolio.Locomotion.Core;
using UnityEngine;

namespace Portfolio.Locomotion.Spline
{
    [System.Serializable]
    public class SimulatedSplineMovement
    {
        //------------------------------- Game Pause -------------------------------//

        //------------------------------- Debugging Methods -------------------------------//

        //------------------------------- Parameters -------------------------------//

        #region Components

        [Header("MOVE PARAMETERS")]
        [SerializeField] private float _maximumGroundSpeed = 8f;
        [SerializeField] private float _groundAcceleration = 70f;
        [SerializeField] private float _groundDeceleration = 90f;
        [SerializeField] private float _slopeAcceleration = 70f;
        [SerializeField] private float _slopeDeceleration = 90f;
        [SerializeField] private float _airAcceleration = 35f;
        [SerializeField] private float _airDeceleration = 25f;
        [SerializeField] private float _minimumInput = 0.01f;
        [SerializeField] private float _groundedStopSpeed = 0.05f;

        public float MaximumGroundSpeed => _maximumGroundSpeed;
        public float GroundAcceleration => _groundAcceleration;
        public float GroundDeceleration => _groundDeceleration;
        public float SlopeAcceleration => _slopeAcceleration;
        public float SlopeDeceleration => _slopeDeceleration;
        public float AirAcceleration => _airAcceleration;
        public float AirDeceleration => _airDeceleration;
        public float GroundedStopSpeed => _groundedStopSpeed;

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

        public void ApplyMovement(
            ref Vector2 velocity,
            LocomotionState state,
            SplineGroundProbe2D surfaceProbe,
            float movementInput,
            bool blockMovementIntoSteepSlope,
            float facingDirection,
            float deltaTime)
        {
            if (blockMovementIntoSteepSlope)
            {
                movementInput = RemoveInputIntoSlope(movementInput, facingDirection);
                RemoveVelocityIntoSlope(ref velocity, facingDirection);
            }

            if (!state.ControlEnabled)
            {
                if (state.Grounded && state.OnWalkableSlope)
                {
                    ApplySlopeMovement(
                        ref velocity,
                        state,
                        surfaceProbe,
                        0f,
                        deltaTime);
                    return;
                }

                ApplyHorizontalDeceleration(
                    ref velocity,
                    state.Grounded ? _groundDeceleration : _airDeceleration,
                    deltaTime);

                if (state.Grounded)
                {
                    velocity.y = 0f;
                }

                return;
            }

            if (state.Grounded && state.OnWalkableSlope)
            {
                ApplySlopeMovement(ref velocity, state, surfaceProbe, movementInput, deltaTime);
                return;
            }

            if (state.Grounded)
            {
                ApplyHorizontalMovement(
                    ref velocity,
                    movementInput,
                    _groundAcceleration,
                    _groundDeceleration,
                    deltaTime,
                    true);
                return;
            }

            ApplyHorizontalMovement(
                ref velocity,
                movementInput,
                _airAcceleration,
                _airDeceleration,
                deltaTime,
                false);
        }

        public void ResetRuntimeState()
        {
        }

        public void Validate()
        {
            _maximumGroundSpeed = Mathf.Max(0f, _maximumGroundSpeed);
            _groundAcceleration = Mathf.Max(0f, _groundAcceleration);
            _groundDeceleration = Mathf.Max(0f, _groundDeceleration);
            _slopeAcceleration = Mathf.Max(0f, _slopeAcceleration);
            _slopeDeceleration = Mathf.Max(0f, _slopeDeceleration);
            _airAcceleration = Mathf.Max(0f, _airAcceleration);
            _airDeceleration = Mathf.Max(0f, _airDeceleration);
            _minimumInput = Mathf.Max(0.0001f, _minimumInput);
            _groundedStopSpeed = Mathf.Max(0f, _groundedStopSpeed);
        }

        private void ApplyHorizontalMovement(
            ref Vector2 velocity,
            float movementInput,
            float acceleration,
            float deceleration,
            float deltaTime,
            bool grounded)
        {
            bool hasInput = Mathf.Abs(movementInput) > _minimumInput;
            float targetSpeed = hasInput
                ? movementInput * _maximumGroundSpeed
                : 0f;
            float rate = hasInput
                ? acceleration
                : deceleration;
            velocity.x = Mathf.MoveTowards(velocity.x, targetSpeed, rate * deltaTime);

            if (grounded)
            {
                if (!hasInput && Mathf.Abs(velocity.x) <= _groundedStopSpeed)
                {
                    velocity.x = 0f;
                }

                velocity.y = 0f;
            }
        }

        private void ApplySlopeMovement(
            ref Vector2 velocity,
            LocomotionState state,
            SplineGroundProbe2D surfaceProbe,
            float movementInput,
            float deltaTime)
        {
            Vector2 tangent = surfaceProbe.GetSlopeTangent(state.SurfaceNormal);
            bool hasInput = Mathf.Abs(movementInput) > _minimumInput;

            if (hasInput)
            {
                Vector2 requestedDirection = Vector2.right * Mathf.Sign(movementInput);

                if (Vector2.Dot(tangent, requestedDirection) < 0f)
                {
                    tangent *= -1f;
                }
            }
            else if (Vector2.Dot(tangent, velocity) < 0f)
            {
                tangent *= -1f;
            }

            float currentSpeed = Vector2.Dot(velocity, tangent);
            float targetSpeed = hasInput
                ? Mathf.Abs(movementInput) * _maximumGroundSpeed
                : 0f;
            float rate = hasInput
                ? _slopeAcceleration
                : _slopeDeceleration;
            float nextSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, rate * deltaTime);

            if (!hasInput && Mathf.Abs(nextSpeed) <= _groundedStopSpeed)
            {
                nextSpeed = 0f;
            }

            velocity = tangent * nextSpeed;
        }

        private void ApplyHorizontalDeceleration(ref Vector2 velocity, float deceleration, float deltaTime)
        {
            velocity.x = Mathf.MoveTowards(velocity.x, 0f, deceleration * deltaTime);
        }

        private float RemoveInputIntoSlope(float movementInput, float facingDirection)
        {
            return Mathf.Sign(movementInput) == Mathf.Sign(facingDirection)
                ? 0f
                : movementInput;
        }

        private void RemoveVelocityIntoSlope(ref Vector2 velocity, float facingDirection)
        {
            if (velocity.x * facingDirection <= 0f)
            {
                return;
            }

            velocity.x = 0f;
        }

#if UNITY_EDITOR
        //------------------------------- Editor -------------------------------//
#endif
    }
}
