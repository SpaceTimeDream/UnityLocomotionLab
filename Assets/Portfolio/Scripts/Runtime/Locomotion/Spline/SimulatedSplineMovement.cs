using UnityEngine;

namespace Portfolio.Locomotion.Spline
{
    public class SimulatedSplineMovement : MonoBehaviour
    {
        // Fields
        [SerializeField] private float _maximumGroundSpeed = 7f;
        [SerializeField] private float _groundAcceleration = 45f;
        [SerializeField] private float _groundBraking = 55f;
        [SerializeField] private float _groundFriction = 18f;
        [SerializeField] private float _airAcceleration = 28f;
        [SerializeField] private float _maximumAirHorizontalSpeed = 7f;
        [SerializeField] private float _gravity = 28f;
        [SerializeField] private float _maximumFallSpeed = 22f;
        [SerializeField, Range(0f, 89f)] private float _walkableSlopeAngle = 50f;

        // Properties
        public float Gravity => _gravity;
        public float WalkableSlopeAngle => _walkableSlopeAngle;

        // Public Methods
        public void SimulateGrounded(SplineLocomotionState state, float input, float deltaTime)
        {
            float targetDirection = Mathf.Abs(input) > 0.01f ? Mathf.Sign(input) : 0f;
            float uphillSign = Mathf.Sign(Vector2.Dot(state.SurfaceTangent, Vector2.up));
            bool blocksUphill = state.SteepSlope && Mathf.Approximately(targetDirection, uphillSign);

            if (targetDirection != 0f && !blocksUphill)
            {
                state.TangentialGroundSpeed = Mathf.MoveTowards(state.TangentialGroundSpeed, targetDirection * _maximumGroundSpeed, _groundAcceleration * deltaTime);
            }
            else if (targetDirection == 0f && state.WalkableSlope)
            {
                state.TangentialGroundSpeed = Mathf.MoveTowards(state.TangentialGroundSpeed, 0f, _groundFriction * deltaTime);
            }
            else if (blocksUphill)
            {
                state.TangentialGroundSpeed = Mathf.MoveTowards(state.TangentialGroundSpeed, 0f, _groundBraking * deltaTime);
            }

            Vector2 gravityVector = Vector2.down * _gravity;
            state.TangentialGroundSpeed += Vector2.Dot(gravityVector, state.SurfaceTangent) * deltaTime;
            state.TangentialGroundSpeed = Mathf.Clamp(state.TangentialGroundSpeed, -_maximumGroundSpeed, _maximumGroundSpeed);
        }

        public void SimulateAirborne(SplineLocomotionState state, float input, float deltaTime)
        {
            Vector2 velocity = state.AirborneVelocity;
            float targetX = Mathf.Abs(input) > 0.01f ? Mathf.Sign(input) * _maximumAirHorizontalSpeed : velocity.x;
            velocity.x = Mathf.MoveTowards(velocity.x, targetX, _airAcceleration * deltaTime);
            velocity.y = Mathf.Max(velocity.y - _gravity * deltaTime, -_maximumFallSpeed);
            state.AirborneVelocity = velocity;
        }
    }
}
