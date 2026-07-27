using Portfolio.Locomotion.Core;
using UnityEngine;

namespace Portfolio.Locomotion.Rigidbody
{
    [System.Serializable]
    public class RigidbodyMovement
    {
        // Fields
        [SerializeField] private float _maximumGroundSpeed = 8f;
        [SerializeField] private float _groundAcceleration = 70f;
        [SerializeField] private float _groundDeceleration = 90f;
        [SerializeField] private float _airAcceleration = 35f;
        [SerializeField] private float _airDeceleration = 25f;
        [SerializeField] private float _steepSlopeSlideSpeed = 4f;

        // Public Methods
        public void ApplyMovement(UnityEngine.Rigidbody2D body, LocomotionState state, SurfaceProbe2D surfaceProbe, float movementInput, bool protectHorizontalMomentum, bool blockMovementIntoSteepSlope, float facingDirection)
        {
            if (blockMovementIntoSteepSlope)
            {
                movementInput = RemoveInputIntoSlope(movementInput, facingDirection);
                RemoveVelocityIntoSlope(body, facingDirection);
            }

            if (protectHorizontalMomentum)
            {
                return;
            }

            Vector2 velocity = body.linearVelocity;

            if (!state.ControlEnabled)
            {
                body.linearVelocity = ApplyHorizontalSpeed(velocity, 0f, state.Grounded ? _groundDeceleration : _airDeceleration);
                return;
            }

            if (state.OnSteepSlope)
            {
                body.linearVelocity = ApplySteepSlope(velocity, state, surfaceProbe, movementInput);
                return;
            }

            if (state.Grounded && state.OnWalkableSlope)
            {
                body.linearVelocity = ApplySlopeMovement(velocity, state, surfaceProbe, movementInput);
                return;
            }

            float acceleration = Mathf.Abs(movementInput) > 0.01f
                ? (state.Grounded ? _groundAcceleration : _airAcceleration)
                : (state.Grounded ? _groundDeceleration : _airDeceleration);
            float targetSpeed = movementInput * _maximumGroundSpeed;
            body.linearVelocity = ApplyHorizontalSpeed(velocity, targetSpeed, acceleration);
        }

        public void Validate()
        {
            _maximumGroundSpeed = Mathf.Max(0f, _maximumGroundSpeed);
            _groundAcceleration = Mathf.Max(0f, _groundAcceleration);
            _groundDeceleration = Mathf.Max(0f, _groundDeceleration);
            _airAcceleration = Mathf.Max(0f, _airAcceleration);
            _airDeceleration = Mathf.Max(0f, _airDeceleration);
            _steepSlopeSlideSpeed = Mathf.Max(0f, _steepSlopeSlideSpeed);
        }

        // Private Methods
        private Vector2 ApplyHorizontalSpeed(Vector2 velocity, float targetSpeed, float acceleration)
        {
            velocity.x = Mathf.MoveTowards(velocity.x, targetSpeed, acceleration * Time.fixedDeltaTime);
            return velocity;
        }

        private float RemoveInputIntoSlope(float movementInput, float facingDirection)
        {
            return Mathf.Sign(movementInput) == Mathf.Sign(facingDirection) ? 0f : movementInput;
        }

        private void RemoveVelocityIntoSlope(UnityEngine.Rigidbody2D body, float facingDirection)
        {
            if (body.linearVelocity.x * facingDirection <= 0f)
            {
                return;
            }

            Vector2 velocity = body.linearVelocity;
            velocity.x = 0f;
            body.linearVelocity = velocity;
        }

        private Vector2 ApplySlopeMovement(Vector2 velocity, LocomotionState state, SurfaceProbe2D surfaceProbe, float movementInput)
        {
            Vector2 tangent = surfaceProbe.GetSlopeTangent(state.SurfaceNormal);

            if (tangent.x < 0f)
            {
                tangent *= -1f;
            }

            float currentSpeed = Vector2.Dot(velocity, tangent);
            float targetSpeed = movementInput * _maximumGroundSpeed;
            float acceleration = Mathf.Abs(movementInput) > 0.01f ? _groundAcceleration : _groundDeceleration;
            float nextSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.fixedDeltaTime);
            return tangent * nextSpeed;
        }

        private Vector2 ApplySteepSlope(Vector2 velocity, LocomotionState state, SurfaceProbe2D surfaceProbe, float movementInput)
        {
            Vector2 tangent = surfaceProbe.GetSlopeTangent(state.SurfaceNormal);

            if (tangent.y > 0f)
            {
                tangent *= -1f;
            }

            bool movingDownhill = Mathf.Sign(movementInput) == Mathf.Sign(tangent.x);
            float targetSpeed = movingDownhill ? Mathf.Abs(movementInput) * _maximumGroundSpeed : 0f;
            targetSpeed = Mathf.Max(targetSpeed, _steepSlopeSlideSpeed);
            state.SetSliding(targetSpeed > 0.01f);
            return tangent * targetSpeed;
        }
    }
}
