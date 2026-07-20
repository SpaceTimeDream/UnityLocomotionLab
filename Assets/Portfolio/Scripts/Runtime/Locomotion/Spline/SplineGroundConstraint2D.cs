using Portfolio.Locomotion.Spline.Path;
using UnityEngine;

namespace Portfolio.Locomotion.Spline
{
    public class SplineGroundConstraint2D : MonoBehaviour
    {
        // Fields
        [SerializeField] private SpriteShapeSplinePath2D _path;
        [SerializeField] private float _landingTolerance = 0.08f;

        // Properties
        public SpriteShapeSplinePath2D Path => _path;
        public float LandingTolerance => _landingTolerance;

        // Public Methods
        public bool IsValid()
        {
            return _path != null && _path.IsValid();
        }

        public void SnapNearest(SplineLocomotionState state, Transform anchor)
        {
            SplinePathSample2D sample = _path.FindNearest(anchor.position);
            ApplySample(state, sample);
            state.Grounded = true;
            state.AirborneVelocity = Vector2.zero;
            anchor.position = sample.Position;
        }

        public void ConstrainAtDistance(SplineLocomotionState state, Transform anchor, float walkableSlopeAngle)
        {
            float previousDistance = state.PathDistance;
            float distance = _path.ClampDistance(previousDistance + state.TangentialGroundSpeed * Time.fixedDeltaTime);

            if ((distance <= 0f && state.TangentialGroundSpeed < 0f) || (distance >= _path.TotalLength && state.TangentialGroundSpeed > 0f))
            {
                state.TangentialGroundSpeed = 0f;
            }

            SplinePathSample2D sample = _path.Evaluate(distance);
            ApplySample(state, sample);
            ApplySlopeClassification(state, walkableSlopeAngle);
            state.Grounded = true;
            state.Rising = false;
            state.Falling = false;
            anchor.position = sample.Position;
        }

        public bool TryLand(SplineLocomotionState state, Vector2 previousPosition, Vector2 proposedPosition, float walkableSlopeAngle)
        {
            if (state.AirborneVelocity.y > 0f)
            {
                return false;
            }

            if (!_path.TryFindLanding(previousPosition, proposedPosition, _landingTolerance, out SplinePathSample2D sample))
            {
                return false;
            }

            ApplySample(state, sample);
            ApplySlopeClassification(state, walkableSlopeAngle);
            state.TangentialGroundSpeed = Vector2.Dot(state.AirborneVelocity, sample.Tangent);
            state.AirborneVelocity = Vector2.zero;
            state.Grounded = true;
            state.Rising = false;
            state.Falling = false;
            return true;
        }

        // Private Methods
        private void ApplySample(SplineLocomotionState state, SplinePathSample2D sample)
        {
            state.PathDistance = sample.Distance;
            state.AnchorPosition = sample.Position;
            state.SurfacePoint = sample.Position;
            state.SurfaceTangent = sample.Tangent;
            state.SurfaceNormal = sample.Normal;
            state.SignedSlopeAngle = sample.SlopeAngle;
        }

        private void ApplySlopeClassification(SplineLocomotionState state, float walkableSlopeAngle)
        {
            state.WalkableSlope = Mathf.Abs(state.SignedSlopeAngle) <= walkableSlopeAngle;
            state.SteepSlope = !state.WalkableSlope;
        }
    }
}
