using UnityEngine;

namespace Portfolio.Locomotion.Spline
{
    [System.Serializable]
    public sealed class PlayerSimulatedCollisionResolver
    {
        //------------------------------- Parameters -------------------------------//

        #region Components

        [Header("COLLISION PARAMETERS")]
        [SerializeField] private LayerMask _collisionLayers = Physics2D.DefaultRaycastLayers;
        [SerializeField] private float _collisionSkin = 0.02f;
        [SerializeField] private float _surfaceNormalTolerance = 0.001f;
        [SerializeField] private int _maximumResolutionIterations = 4;

        #endregion

        #region Flags

        private readonly RaycastHit2D[] _hitBuffer = new RaycastHit2D[16];
        private ContactFilter2D _contactFilter;
        private bool _initialized;

        #endregion

        #region Properties

        public float CollisionSkin => _collisionSkin;

        #endregion

        //------------------------------- Initialization Methods -------------------------------//

        #region Initialization

        public void Initialize()
        {
            _contactFilter = new ContactFilter2D();
            _contactFilter.SetLayerMask(_collisionLayers);
            _contactFilter.useTriggers = false;
            _initialized = true;
        }

        #endregion

        //------------------------------- Core / Routines Methods -------------------------------//

        public Vector2 Resolve(
            Vector2 bodyPosition,
            CapsuleCollider2D capsuleCollider,
            Vector2 intendedDisplacement,
            ref Vector2 velocity,
            bool grounded,
            float maximumWalkableSlopeAngle,
            out RaycastHit2D blockingHit)
        {
            EnsureInitialized();
            blockingHit = default;

            if (capsuleCollider == null ||
                !capsuleCollider.enabled ||
                intendedDisplacement.sqrMagnitude <=
                _surfaceNormalTolerance * _surfaceNormalTolerance)
            {
                return intendedDisplacement;
            }

            Vector2 resolvedDisplacement = Vector2.zero;
            Vector2 remainingDisplacement = intendedDisplacement;

            for (int iteration = 0;
                iteration < _maximumResolutionIterations &&
                remainingDisplacement.sqrMagnitude >
                _surfaceNormalTolerance * _surfaceNormalTolerance;
                iteration++)
            {
                Vector2 castPosition = bodyPosition + resolvedDisplacement;

                if (!TryFindEarliestHit(
                    castPosition,
                    capsuleCollider,
                    remainingDisplacement,
                    out RaycastHit2D hit))
                {
                    resolvedDisplacement += remainingDisplacement;
                    break;
                }

                blockingHit = hit;
                float movementDistance = remainingDisplacement.magnitude;
                float safeDistance = Mathf.Clamp(
                    hit.distance - _collisionSkin,
                    0f,
                    movementDistance);
                Vector2 safeMovement = remainingDisplacement.normalized * safeDistance;
                resolvedDisplacement += safeMovement;
                Vector2 unresolvedDisplacement = remainingDisplacement - safeMovement;
                RemoveVelocityIntoSurface(ref velocity, hit.normal);

                if (grounded &&
                    !IsWalkableSurface(hit.normal, maximumWalkableSlopeAngle))
                {
                    StopHorizontalVelocityIntoSurface(ref velocity, hit.normal);
                    break;
                }

                remainingDisplacement =
                    unresolvedDisplacement -
                    hit.normal * Vector2.Dot(unresolvedDisplacement, hit.normal);
            }

            return resolvedDisplacement;
        }

        public void Validate()
        {
            _collisionSkin = Mathf.Max(0.001f, _collisionSkin);
            _surfaceNormalTolerance = Mathf.Max(0.0001f, _surfaceNormalTolerance);
            _maximumResolutionIterations = Mathf.Max(1, _maximumResolutionIterations);
        }

        private void EnsureInitialized()
        {
            if (!_initialized)
            {
                Initialize();
            }
        }

        private bool TryFindEarliestHit(
            Vector2 bodyPosition,
            CapsuleCollider2D capsuleCollider,
            Vector2 displacement,
            out RaycastHit2D earliestHit)
        {
            earliestHit = default;
            float movementDistance = displacement.magnitude;

            if (movementDistance <= _surfaceNormalTolerance)
            {
                return false;
            }

            GetCapsuleShape(
                bodyPosition,
                capsuleCollider,
                out Vector2 center,
                out Vector2 size,
                out CapsuleDirection2D capsuleDirection,
                out float angle);
            Vector2 direction = displacement / movementDistance;
            int hitCount = Physics2D.CapsuleCast(
                center,
                size,
                capsuleDirection,
                angle,
                direction,
                _contactFilter,
                _hitBuffer,
                movementDistance + _collisionSkin);
            float earliestDistance = float.PositiveInfinity;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit2D hit = _hitBuffer[i];

                if (!IsBlockingHit(hit, capsuleCollider, displacement) ||
                    hit.distance >= earliestDistance)
                {
                    continue;
                }

                earliestDistance = hit.distance;
                earliestHit = hit;
            }

            return earliestHit.collider != null;
        }

        private bool IsBlockingHit(
            RaycastHit2D hit,
            CapsuleCollider2D capsuleCollider,
            Vector2 displacement)
        {
            if (!IsValidCollider(hit.collider, capsuleCollider))
            {
                return false;
            }

            return Vector2.Dot(displacement, hit.normal) <
                -_surfaceNormalTolerance;
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

        private bool IsWalkableSurface(
            Vector2 normal,
            float maximumWalkableSlopeAngle)
        {
            return normal.y > 0.01f &&
                Vector2.Angle(normal, Vector2.up) <= maximumWalkableSlopeAngle;
        }

        private void RemoveVelocityIntoSurface(
            ref Vector2 velocity,
            Vector2 normal)
        {
            float inwardVelocity = Vector2.Dot(velocity, normal);

            if (inwardVelocity < -_surfaceNormalTolerance)
            {
                velocity -= normal * inwardVelocity;
            }
        }

        private void StopHorizontalVelocityIntoSurface(
            ref Vector2 velocity,
            Vector2 normal)
        {
            if (Mathf.Abs(normal.x) <= _surfaceNormalTolerance ||
                velocity.x * normal.x >= 0f)
            {
                return;
            }

            velocity.x = 0f;
        }

        private void GetCapsuleShape(
            Vector2 bodyPosition,
            CapsuleCollider2D capsuleCollider,
            out Vector2 center,
            out Vector2 size,
            out CapsuleDirection2D direction,
            out float angle)
        {
            Transform capsuleTransform = capsuleCollider.transform;
            Vector3 scale = capsuleTransform.lossyScale;
            center =
                (Vector2)capsuleTransform.TransformPoint(capsuleCollider.offset) +
                bodyPosition -
                (Vector2)capsuleTransform.position;
            size = Vector2.Scale(
                capsuleCollider.size,
                new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y)));
            direction = capsuleCollider.direction;
            angle = capsuleTransform.eulerAngles.z;
        }

    }
}
