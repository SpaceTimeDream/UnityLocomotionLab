using Portfolio.Locomotion.Core;
using UnityEngine;

namespace Portfolio.Locomotion.Rigidbody
{
    [System.Serializable]
    public class SurfaceProbe2D
    {
        // Fields
        [SerializeField] private LayerMask _groundLayers = Physics2D.DefaultRaycastLayers;
        [SerializeField] private float _probeDistance = 0.08f;
        [SerializeField] private float _probeInset = 0.04f;
        [SerializeField] private float _maximumWalkableSlopeAngle = 45f;

        private readonly RaycastHit2D[] _hits = new RaycastHit2D[6];
        private ContactFilter2D _contactFilter;
        private bool _initialized;
        private Collider2D _surfaceCollider;

        // Properties
        public float MaximumWalkableSlopeAngle => _maximumWalkableSlopeAngle;
        public Collider2D SurfaceCollider => _surfaceCollider;

        // Public Methods
        public void Initialize()
        {
            _contactFilter = new ContactFilter2D();
            _contactFilter.SetLayerMask(_groundLayers);
            _contactFilter.useTriggers = false;
            _initialized = true;
        }

        public void Probe(CapsuleCollider2D capsuleCollider, LocomotionState state)
        {
            if (!_initialized)
            {
                Initialize();
            }

            Bounds bounds = capsuleCollider.bounds;
            float rayLength = _probeDistance + Mathf.Max(0.01f, bounds.extents.y);
            Vector2 originCenter = bounds.center;
            Vector2 originLeft = new Vector2(bounds.min.x + _probeInset, bounds.center.y);
            Vector2 originRight = new Vector2(bounds.max.x - _probeInset, bounds.center.y);

            RaycastHit2D bestHit = default;
            bool hasHit = false;
            hasHit |= TryGetBestHit(originCenter, rayLength, capsuleCollider, ref bestHit);
            hasHit |= TryGetBestHit(originLeft, rayLength, capsuleCollider, ref bestHit);
            hasHit |= TryGetBestHit(originRight, rayLength, capsuleCollider, ref bestHit);

            if (!hasHit)
            {
                _surfaceCollider = null;
                state.SetSurface(false, false, false, Vector2.up, 0f);
                return;
            }

            _surfaceCollider = bestHit.collider;
            float slopeAngle = Vector2.Angle(bestHit.normal, Vector2.up);
            bool walkable = slopeAngle <= _maximumWalkableSlopeAngle;
            state.SetSurface(true, walkable && slopeAngle > 0.01f, !walkable, bestHit.normal, slopeAngle);
        }

        public Vector2 GetSlopeTangent(Vector2 surfaceNormal)
        {
            return new Vector2(surfaceNormal.y, -surfaceNormal.x).normalized;
        }

        public bool HasNearbySteepSlope(CapsuleCollider2D capsuleCollider, float facingDirection, float distance)
        {
            if (!_initialized)
            {
                Initialize();
            }

            int hitCount = capsuleCollider.Cast(Vector2.right * Mathf.Sign(facingDirection), _contactFilter, _hits, distance);

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit2D hit = _hits[i];

                if (hit.collider != null && Vector2.Angle(hit.normal, Vector2.up) > _maximumWalkableSlopeAngle)
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
            _maximumWalkableSlopeAngle = Mathf.Clamp(_maximumWalkableSlopeAngle, 0f, 89f);
        }

        // Private Methods
        private bool TryGetBestHit(Vector2 origin, float distance, Collider2D ignoredCollider, ref RaycastHit2D bestHit)
        {
            int hitCount = Physics2D.Raycast(origin, Vector2.down, _contactFilter, _hits, distance);
            bool foundHit = bestHit.collider != null;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit2D hit = _hits[i];

                if (hit.collider == null || hit.collider == ignoredCollider)
                {
                    continue;
                }

                if (!foundHit || hit.distance < bestHit.distance)
                {
                    bestHit = hit;
                    foundHit = true;
                }
            }

            return foundHit;
        }
    }
}
