using Portfolio.Locomotion.Core;
using UnityEngine;

namespace Portfolio.Locomotion.Spline
{
    [System.Serializable]
    public sealed class PlayerSimulatedGroundProbe
    {
        //------------------------------- Parameters -------------------------------//

        #region Components

        [Header("GROUND CHECK PARAMETERS")]
        [SerializeField] private LayerMask _groundLayers = Physics2D.DefaultRaycastLayers;
        [SerializeField] private float _probeDistance = 0.12f;
        [SerializeField] private float _groundSnapDistance = 0.08f;
        [SerializeField] private float _risingGroundedVelocityThreshold = 0.1f;
        [SerializeField] private float _groundingGraceDuration = 0.06f;

        [Header("SLOPE CHECK PARAMETERS")]
        [SerializeField] private float _slopeAngleThreshold = 1f;
        [SerializeField] private float _maximumWalkableSlopeAngle = 45f;
        [SerializeField] private float _steepSlopeJumpRestrictionAngle = 55f;

        #endregion

        #region Flags

        private readonly RaycastHit2D[] _hits = new RaycastHit2D[8];
        private readonly ContactPoint2D[] _contacts = new ContactPoint2D[8];
        private ContactFilter2D _contactFilter;
        private Collider2D _surfaceCollider;
        private Vector2 _surfaceNormal = Vector2.up;
        private bool _initialized;
        private float _groundingGraceRemainingTime;

        #endregion

        #region Properties

        public float GroundSnapDistance => _groundSnapDistance;
        public float ProbeDistance => _probeDistance;
        public float MaximumWalkableSlopeAngle => _maximumWalkableSlopeAngle;
        public Collider2D SurfaceCollider => _surfaceCollider;
        public Vector2 SurfaceNormal => _surfaceNormal;
        public bool IsSteepSlopeJumpRestricted =>
            _surfaceCollider != null &&
            Vector2.Angle(_surfaceNormal, Vector2.up) >= _steepSlopeJumpRestrictionAngle;

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
            Rigidbody2D body,
            Vector2 velocity,
            LocomotionState state,
            bool ignoreGrounding)
        {
            EnsureInitialized();

            if (ignoreGrounding)
            {
                ClearSurface(state);
                return;
            }

            if (TryGetBodyContact(
                body,
                capsuleCollider,
                out Collider2D contactCollider,
                out Vector2 contactNormal))
            {
                if (IsSeparatingFromSurface(velocity, contactNormal))
                {
                    ClearSurface(state);
                    return;
                }

                ApplySurface(contactCollider, contactNormal, state);
                return;
            }

            if (TryGetGroundHit(capsuleCollider, _probeDistance, out RaycastHit2D hit))
            {
                if (IsSeparatingFromSurface(velocity, hit.normal))
                {
                    ClearSurface(state);
                    return;
                }

                ApplySurface(hit.collider, hit.normal, state);
                return;
            }

            HandleMissingSurface(velocity, state);
        }

        public bool TryGetGroundHit(
            CapsuleCollider2D capsuleCollider,
            float distance,
            out RaycastHit2D bestHit)
        {
            EnsureInitialized();
            bestHit = default;
            int hitCount = capsuleCollider.Cast(
                Vector2.down,
                _contactFilter,
                _hits,
                Mathf.Max(0f, distance));
            bool hasHit = false;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit2D hit = _hits[i];

                if (!IsValidCollider(hit.collider, capsuleCollider) || hit.normal.y <= 0.01f)
                {
                    continue;
                }

                if (!hasHit || hit.distance < bestHit.distance)
                {
                    bestHit = hit;
                    hasHit = true;
                }
            }

            return hasHit;
        }

        public bool TryGetGroundHitNearPosition(
            CapsuleCollider2D capsuleCollider,
            float verticalRange,
            out RaycastHit2D bestHit)
        {
            EnsureInitialized();
            bestHit = default;

            if (capsuleCollider == null || verticalRange <= 0f)
            {
                return false;
            }

            Bounds bounds = capsuleCollider.bounds;
            float originY = bounds.min.y + verticalRange;
            float castDistance = verticalRange * 2f;
            for (int i = 0; i < 3; i++)
            {
                float sampleOffset = i == 0
                    ? 0f
                    : (i == 1 ? -bounds.extents.x : bounds.extents.x) * 0.75f;
                Vector2 origin = new Vector2(bounds.center.x + sampleOffset, originY);
                int hitCount = Physics2D.Raycast(
                    origin,
                    Vector2.down,
                    _contactFilter,
                    _hits,
                    castDistance);
                RaycastHit2D closestHit = default;

                for (int j = 0; j < hitCount; j++)
                {
                    RaycastHit2D hit = _hits[j];

                    if (!IsValidCollider(hit.collider, capsuleCollider) ||
                        !IsWalkableSurface(hit.normal))
                    {
                        continue;
                    }

                    if (closestHit.collider == null || hit.distance < closestHit.distance)
                    {
                        closestHit = hit;
                    }
                }

                if (closestHit.collider == null)
                {
                    continue;
                }

                bestHit = closestHit;
                return true;
            }

            return false;
        }

        public void ApplySurfaceHit(RaycastHit2D hit, LocomotionState state)
        {
            if (hit.collider == null)
            {
                return;
            }

            ApplySurface(hit.collider, hit.normal, state);
        }

        public Vector2 GetSlopeTangent(Vector2 surfaceNormal)
        {
            return new Vector2(surfaceNormal.y, -surfaceNormal.x).normalized;
        }

        public bool HasNearbySteepSlope(
            CapsuleCollider2D capsuleCollider,
            float facingDirection,
            float distance)
        {
            EnsureInitialized();
            Vector2 direction = Vector2.right * Mathf.Sign(facingDirection);
            int hitCount = capsuleCollider.Cast(
                direction,
                _contactFilter,
                _hits,
                Mathf.Max(0f, distance));

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit2D hit = _hits[i];

                if (!IsValidCollider(hit.collider, capsuleCollider))
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

        public bool IsWalkableSurface(Vector2 normal)
        {
            return normal.y > 0.01f &&
                Vector2.Angle(normal, Vector2.up) <= _maximumWalkableSlopeAngle;
        }

        public void Validate()
        {
            _probeDistance = Mathf.Max(0.01f, _probeDistance);
            _groundSnapDistance = Mathf.Max(0f, _groundSnapDistance);
            _risingGroundedVelocityThreshold = Mathf.Max(0f, _risingGroundedVelocityThreshold);
            _groundingGraceDuration = Mathf.Max(0f, _groundingGraceDuration);
            _slopeAngleThreshold = Mathf.Clamp(_slopeAngleThreshold, 0f, 89f);
            _maximumWalkableSlopeAngle = Mathf.Clamp(
                _maximumWalkableSlopeAngle,
                _slopeAngleThreshold,
                89f);
            _steepSlopeJumpRestrictionAngle = Mathf.Clamp(
                _steepSlopeJumpRestrictionAngle,
                _maximumWalkableSlopeAngle,
                89f);
        }

        private void EnsureInitialized()
        {
            if (!_initialized)
            {
                Initialize();
            }
        }

        private bool TryGetBodyContact(
            Rigidbody2D body,
            CapsuleCollider2D capsuleCollider,
            out Collider2D surfaceCollider,
            out Vector2 surfaceNormal)
        {
            surfaceCollider = null;
            surfaceNormal = Vector2.up;

            if (body == null)
            {
                return false;
            }

            int contactCount = body.GetContacts(_contactFilter, _contacts);
            float bestNormalY = 0.01f;

            for (int i = 0; i < contactCount; i++)
            {
                ContactPoint2D contact = _contacts[i];
                Collider2D collider = contact.collider == capsuleCollider
                    ? contact.otherCollider
                    : contact.collider;

                if (!IsValidCollider(collider, capsuleCollider) ||
                    contact.point.y > capsuleCollider.bounds.center.y ||
                    contact.normal.y <= bestNormalY)
                {
                    continue;
                }

                bestNormalY = contact.normal.y;
                surfaceCollider = collider;
                surfaceNormal = contact.normal;
            }

            return surfaceCollider != null;
        }

        private void ApplySurface(
            Collider2D surfaceCollider,
            Vector2 surfaceNormal,
            LocomotionState state)
        {
            float slopeAngle = Vector2.Angle(surfaceNormal, Vector2.up);
            bool walkable = surfaceNormal.y > 0.01f &&
                slopeAngle <= _maximumWalkableSlopeAngle;
            bool onWalkableSlope = walkable && slopeAngle > _slopeAngleThreshold;
            bool onSteepSlope = surfaceNormal.y > 0.01f && !walkable;
            _surfaceCollider = surfaceCollider;
            _surfaceNormal = surfaceNormal;
            _groundingGraceRemainingTime = _groundingGraceDuration;
            state.SetSurface(
                walkable,
                onWalkableSlope,
                onSteepSlope,
                surfaceNormal,
                slopeAngle);
        }

        private void ClearSurface(LocomotionState state)
        {
            _surfaceCollider = null;
            _surfaceNormal = Vector2.up;
            _groundingGraceRemainingTime = 0f;
            state.SetSurface(false, false, false, Vector2.up, 0f);
        }

        private void HandleMissingSurface(Vector2 velocity, LocomotionState state)
        {
            if (_surfaceCollider == null ||
                _groundingGraceRemainingTime <= 0f ||
                IsSeparatingFromSurface(velocity, _surfaceNormal))
            {
                ClearSurface(state);
                return;
            }

            _groundingGraceRemainingTime = Mathf.Max(
                0f,
                _groundingGraceRemainingTime - Time.fixedDeltaTime);

            if (_groundingGraceRemainingTime <= 0f)
            {
                ClearSurface(state);
                return;
            }

            float slopeAngle = Vector2.Angle(_surfaceNormal, Vector2.up);
            bool walkable = _surfaceNormal.y > 0.01f &&
                slopeAngle <= _maximumWalkableSlopeAngle;
            bool onWalkableSlope = walkable && slopeAngle > _slopeAngleThreshold;
            bool onSteepSlope = _surfaceNormal.y > 0.01f && !walkable;

            state.SetSurface(
                walkable,
                onWalkableSlope,
                onSteepSlope,
                _surfaceNormal,
                slopeAngle);
        }

        private bool IsValidCollider(
            Collider2D collider,
            CapsuleCollider2D capsuleCollider)
        {
            return collider != null &&
                collider != capsuleCollider &&
                !collider.isTrigger &&
                collider.attachedRigidbody != capsuleCollider.attachedRigidbody;
        }

        private bool IsSeparatingFromSurface(Vector2 velocity, Vector2 surfaceNormal)
        {
            return Vector2.Dot(velocity, surfaceNormal) > _risingGroundedVelocityThreshold;
        }

    }
}
