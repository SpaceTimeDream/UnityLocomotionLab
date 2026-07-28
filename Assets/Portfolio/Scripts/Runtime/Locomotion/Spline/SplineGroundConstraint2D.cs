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

        private void Start()
        {
            Initialize();
        }

        #endregion

        //------------------------------- Initialization Methods -------------------------------//

        #region Initialization

        private void Initialize()
        {
            InitializeModules();
            InitializeComponents();
            InitializeEvents();
        }

        private void InitializeModules()
        {
        }

        private void InitializeComponents()
        {
            if (_groundProbe == null)
            {
                _groundProbe = GetComponent<SplineGroundProbe2D>();
            }
        }

        private void InitializeEvents()
        {
        }

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

        public void ConstrainAtDistance(SplineLocomotionState state, Transform anchor, float walkableSlopeAngle, SimulatedCollisionResolver2D collisionResolver)
        {
            float previousDistance = state.PathDistance;
            float targetDistance = _path.ClampDistance(previousDistance + state.TangentialGroundSpeed * Time.fixedDeltaTime);

            if ((targetDistance <= 0f && state.TangentialGroundSpeed < 0f) || (targetDistance >= _path.TotalLength && state.TangentialGroundSpeed > 0f))
            {
                state.TangentialGroundSpeed = 0f;
            }

            float travelDistance = Mathf.Abs(targetDistance - previousDistance);
            int stepCount = Mathf.Max(1, Mathf.CeilToInt(travelDistance / collisionResolver.MaximumSplineSweepStep));
            float safePathDistance = previousDistance;
            Vector2 safePosition = anchor.position;
            bool blocked = false;

            for (int step = 1; step <= stepCount; step++)
            {
                float stepDistance = Mathf.Lerp(previousDistance, targetDistance, step / (float)stepCount);
                SplinePathSample2D stepSample = _path.Evaluate(stepDistance);
                Vector2 intendedDisplacement = stepSample.Position - safePosition;
                Vector2 velocity = intendedDisplacement / Mathf.Max(Time.fixedDeltaTime, 0.0001f);
                Vector2 resolvedDisplacement = collisionResolver.Resolve(safePosition, intendedDisplacement, ref velocity, out RaycastHit2D hit);
                safePosition += resolvedDisplacement;

                if (hit.collider != null || resolvedDisplacement.sqrMagnitude + 0.000001f < intendedDisplacement.sqrMagnitude)
                {
                    float fraction = intendedDisplacement.sqrMagnitude > 0.000001f
                        ? Mathf.Clamp01(resolvedDisplacement.magnitude / intendedDisplacement.magnitude)
                        : 0f;
                    safePathDistance = Mathf.Lerp(safePathDistance, stepDistance, fraction);
                    blocked = true;
                    break;
                }

                safePathDistance = stepDistance;
            }

            SplinePathSample2D sample = _path.Evaluate(safePathDistance);
            ApplySample(state, sample);
            state.AnchorPosition = safePosition;
            ApplySlopeClassification(state, walkableSlopeAngle);
            state.Grounded = true;
            state.Rising = false;
            state.Falling = false;

            if (blocked)
            {
                state.TangentialGroundSpeed = 0f;
            }

            TranslateTo(anchor, safePosition);
        }

        public bool TryLand(SplineLocomotionState state, RaycastHit2D hit, Vector2 impactVelocity, float walkableSlopeAngle)
        {
            SplinePathSample2D sample = _path.FindNearest(hit.point);

            if (Vector2.Distance(sample.Position, hit.point) > _maximumLandingSnapDistance)
            {
                return false;
            }

            ApplySample(state, sample);
            ApplySlopeClassification(state, walkableSlopeAngle);
            state.TangentialGroundSpeed = Vector2.Dot(impactVelocity, sample.Tangent);
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
