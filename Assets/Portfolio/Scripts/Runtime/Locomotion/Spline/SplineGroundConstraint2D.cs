using Portfolio.Locomotion.Spline.Path;
using UnityEngine;

namespace Portfolio.Locomotion.Spline
{
    public class SplineGroundConstraint2D : MonoBehaviour
    {
        //------------------------------- Game Pause -------------------------------//

        //------------------------------- Debugging Methods -------------------------------//

        //------------------------------- Parameters -------------------------------//

        #region Components
        [SerializeField] private SpriteShapeSplinePath2D _path;
        [SerializeField] private SplineGroundProbe2D _groundProbe;
        [SerializeField] private float _maximumLandingSnapDistance = 0.35f;

        // Properties
        public SpriteShapeSplinePath2D Path => _path;
        public SplineGroundProbe2D GroundProbe => _groundProbe;

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
        public bool IsValid()
        {
            return _path != null && _path.IsValid() && _groundProbe != null;
        }

        public void SnapNearest(SplineLocomotionState state, Transform anchor)
        {
            SplinePathSample2D sample = _path.FindNearest(anchor.position);
            ApplySample(state, sample);
            state.Grounded = true;
            state.AirborneVelocity = Vector2.zero;
            TranslateTo(anchor, sample.Position);
        }

        public bool HasGroundContact(Vector2 anchorPosition)
        {
            return _groundProbe != null && _groundProbe.TryGetGroundedHit(anchorPosition, out _);
        }

        public void Detach(SplineLocomotionState state)
        {
            state.AirborneVelocity = state.SurfaceTangent * state.TangentialGroundSpeed;
            state.Grounded = false;
            state.Rising = state.AirborneVelocity.y > 0f;
            state.Falling = !state.Rising;
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
            TranslateTo(anchor, sample.Position);
        }

        public bool TryLand(SplineLocomotionState state, Vector2 previousPosition, Vector2 proposedPosition, float walkableSlopeAngle)
        {
            if (state.AirborneVelocity.y > 0f)
            {
                return false;
            }

            if (_groundProbe == null || !_groundProbe.TryGetLandingHit(previousPosition, proposedPosition, out RaycastHit2D hit))
            {
                return false;
            }

            SplinePathSample2D sample = _path.FindNearest(hit.point);

            if (Vector2.Distance(sample.Position, hit.point) > _maximumLandingSnapDistance)
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

        private void TranslateTo(Transform anchor, Vector2 position)
        {
            Vector2 delta = position - (Vector2)anchor.position;
            anchor.Translate(delta, Space.World);
        }
#if UNITY_EDITOR
        //------------------------------- Editor -------------------------------//

        private void OnValidate()
        {
            _maximumLandingSnapDistance = Mathf.Max(0.01f, _maximumLandingSnapDistance);

            if (_groundProbe == null)
            {
                _groundProbe = GetComponent<SplineGroundProbe2D>();
            }
        }
#endif
    }
}
