using Portfolio.Locomotion.Core;
using UnityEngine;

namespace Portfolio.Locomotion.Rigidbody
{
    [System.Serializable]
    public class SurfaceProbe2D
    {
        //------------------------------- Game Pause -------------------------------//

        //------------------------------- Debugging Methods -------------------------------//

        //------------------------------- Parameters -------------------------------//

        #region Components

        [Header("GROUND CHECK PARAMETERS")]
        [SerializeField] private LayerMask _groundLayers = Physics2D.DefaultRaycastLayers;
        [SerializeField] private float _probeDistance = 0.08f;
        [SerializeField] private float _probeInset = 0.04f;
        [SerializeField] private float _risingGroundedVelocityThreshold = 0.1f;

        [Header("SLOPE CHECK PARAMETERS")]
        [SerializeField] private float _slopeAngleThreshold = 1f;
        [SerializeField] private float _maximumWalkableSlopeAngle = 45f;
        [SerializeField] private float _steepSlopeJumpRestrictionAngle = 55f;

        private readonly RaycastHit2D[] _hits = new RaycastHit2D[8];
        private ContactFilter2D _contactFilter;
        private bool _initialized;
        private Collider2D _surfaceCollider;
        private Vector2 _surfaceNormal = Vector2.up;
        private float _groundedTime;

        public float MaximumWalkableSlopeAngle => _maximumWalkableSlopeAngle;
        public Collider2D SurfaceCollider => _surfaceCollider;
        public float GroundedTime => _groundedTime;
        public bool IsSteepSlopeJumpRestricted =>
            _surfaceCollider != null && Vector2.Angle(_surfaceNormal, Vector2.up) >= _steepSlopeJumpRestrictionAngle;

        #endregion

        #region Flags

        #endregion

        //------------------------------- Unity Methods -------------------------------//

        #region Unity

        #endregion

        //------------------------------- Initialization Methods -------------------------------//

        #region Initialization

        public void Initialize()
        {
            _contactFilter = new ContactFilter2D();
            _contactFilter.SetLayerMask(_groundLayers);
            _contactFilter.useTriggers = false;
            _initialized = true;
        }

        #endregion

        //------------------------------- Core / Routines Methods -------------------------------//

        public void Probe(
            CapsuleCollider2D capsuleCollider,
            UnityEngine.Rigidbody2D body,
            LocomotionState state,
            bool ignoreGrounding)
        {
            if (!_initialized)
            {
                Initialize();
            }

            if (ignoreGrounding || body.linearVelocity.y > _risingGroundedVelocityThreshold)
            {
                ClearSurface(state);
                return;
            }

            Bounds bounds = capsuleCollider.bounds;
            float rayLength = bounds.extents.y + _probeDistance;
            Vector2 originCenter = bounds.center;
            Vector2 originLeft = new Vector2(bounds.min.x + _probeInset, bounds.center.y);
            Vector2 originRight = new Vector2(bounds.max.x - _probeInset, bounds.center.y);

            RaycastHit2D bestWalkableHit = default;
            RaycastHit2D bestSteepHit = default;
            CollectBestSupportHits(
                originCenter,
                rayLength,
                capsuleCollider,
                ref bestWalkableHit,
                ref bestSteepHit);
            CollectBestSupportHits(
                originLeft,
                rayLength,
                capsuleCollider,
                ref bestWalkableHit,
                ref bestSteepHit);
            CollectBestSupportHits(
                originRight,
                rayLength,
                capsuleCollider,
                ref bestWalkableHit,
                ref bestSteepHit);

            RaycastHit2D bestHit = bestWalkableHit.collider != null
                ? bestWalkableHit
                : bestSteepHit;
            bool hasHit = bestHit.collider != null;

            if (!hasHit)
            {
                ClearSurface(state);
                return;
            }

            _surfaceCollider = bestHit.collider;
            _surfaceNormal = bestHit.normal;
            _groundedTime += Time.fixedDeltaTime;
            float slopeAngle = Vector2.Angle(bestHit.normal, Vector2.up);
            bool onWalkableSlope = slopeAngle > _slopeAngleThreshold && slopeAngle <= _maximumWalkableSlopeAngle;
            bool onSteepSlope = slopeAngle > _maximumWalkableSlopeAngle;
            state.SetSurface(true, onWalkableSlope, onSteepSlope, bestHit.normal, slopeAngle);
        }

        public Vector2 GetSlopeTangent(Vector2 surfaceNormal)
        {
            return new Vector2(surfaceNormal.y, -surfaceNormal.x).normalized;
        }

        public bool IsMovingUpSlope(float movementInput, Vector2 surfaceNormal)
        {
            if (Mathf.Abs(movementInput) <= 0.01f)
            {
                return false;
            }

            Vector2 tangent = GetSlopeTangent(surfaceNormal);

            if (Vector2.Dot(tangent, new Vector2(Mathf.Sign(movementInput), 0f)) < 0f)
            {
                tangent *= -1f;
            }

            return tangent.y > 0.01f;
        }

        public bool HasNearbySteepSlope(
            CapsuleCollider2D capsuleCollider,
            float horizontalDirection,
            float distance)
        {
            if (!_initialized)
            {
                Initialize();
            }

            Vector2 direction = Vector2.right * Mathf.Sign(horizontalDirection);
            int hitCount = capsuleCollider.Cast(direction, _contactFilter, _hits, distance);

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit2D hit = _hits[i];

                if (hit.collider == null || hit.collider == capsuleCollider || hit.collider.isTrigger)
                {
                    continue;
                }

                float slopeAngle = Vector2.Angle(hit.normal, Vector2.up);
                bool opposesMovement = Vector2.Dot(direction, hit.normal) < -0.01f;

                if (opposesMovement && slopeAngle > _maximumWalkableSlopeAngle)
                {
                    return true;
                }
            }

            return false;
        }

        public void Validate()
        {
            _probeDistance = Mathf.Max(0.01f, _probeDistance);
            _probeInset = Mathf.Max(0f, _probeInset);
            _risingGroundedVelocityThreshold = Mathf.Max(0f, _risingGroundedVelocityThreshold);
            _slopeAngleThreshold = Mathf.Clamp(_slopeAngleThreshold, 0f, 89f);
            _maximumWalkableSlopeAngle = Mathf.Clamp(_maximumWalkableSlopeAngle, _slopeAngleThreshold, 89f);
            _steepSlopeJumpRestrictionAngle = Mathf.Clamp(
                _steepSlopeJumpRestrictionAngle,
                _maximumWalkableSlopeAngle,
                89f);
        }

        private void ClearSurface(LocomotionState state)
        {
            _surfaceCollider = null;
            _surfaceNormal = Vector2.up;
            _groundedTime = 0f;
            state.SetSurface(false, false, false, Vector2.up, 0f);
        }

        private void CollectBestSupportHits(
            Vector2 origin,
            float distance,
            Collider2D ignoredCollider,
            ref RaycastHit2D bestWalkableHit,
            ref RaycastHit2D bestSteepHit)
        {
            int hitCount = Physics2D.Raycast(origin, Vector2.down, _contactFilter, _hits, distance);

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit2D hit = _hits[i];

                if (hit.collider == null || hit.collider == ignoredCollider || hit.collider.isTrigger)
                {
                    continue;
                }

                if (hit.normal.y <= 0f)
                {
                    continue;
                }

                float slopeAngle = Vector2.Angle(hit.normal, Vector2.up);

                if (slopeAngle <= _maximumWalkableSlopeAngle)
                {
                    SetCloserHit(hit, ref bestWalkableHit);
                    continue;
                }

                SetCloserHit(hit, ref bestSteepHit);
            }
        }

        private void SetCloserHit(RaycastHit2D hit, ref RaycastHit2D bestHit)
        {
            if (bestHit.collider == null || hit.distance < bestHit.distance)
            {
                bestHit = hit;
            }
        }

#if UNITY_EDITOR
        //------------------------------- Editor -------------------------------//
#endif
    }
}
