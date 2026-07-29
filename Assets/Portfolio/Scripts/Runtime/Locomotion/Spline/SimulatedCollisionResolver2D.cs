using UnityEngine;

namespace Portfolio.Locomotion.Spline
{
    public class SimulatedCollisionResolver2D : MonoBehaviour
    {
        //------------------------------- Game Pause -------------------------------//

        //------------------------------- Debugging Methods -------------------------------//

        //------------------------------- Parameters -------------------------------//

        #region Components

        [Header("COMPONENTS")]
        [SerializeField] private CapsuleCollider2D _capsuleCollider;

        [Header("COLLISION")]
        [SerializeField] private LayerMask _collisionLayers = Physics2D.DefaultRaycastLayers;
        [SerializeField, Min(0.001f)] private float _collisionSkin = 0.02f;
        [SerializeField, Min(0f)] private float _separationPushDistance = 0.005f;
        [SerializeField, Min(0f)] private float _surfaceNormalTolerance = 0.001f;
        [SerializeField, Min(1)] private int _maximumResolutionIterations = 4;
        [SerializeField, Min(0f)] private float _maximumDepenetrationDistance = 0.05f;
        [SerializeField, Min(0.01f)] private float _groundedSplineSubdivisionDistance = 0.25f;
        [SerializeField] private bool _drawCollisionGizmos;

        private readonly RaycastHit2D[] _hitBuffer = new RaycastHit2D[16];
        private readonly Collider2D[] _overlapBuffer = new Collider2D[16];
        private ContactFilter2D _contactFilter;
        private Vector2 _lastSweepStart;
        private Vector2 _lastIntendedDisplacement;
        private Vector2 _lastResolvedDisplacement;
        private Vector2 _lastContactPoint;
        private Vector2 _lastContactNormal;
        private Vector2 _lastSeparationPush;
        private bool _hasLastContact;

        public float CollisionSkin => _collisionSkin;
        public float MaximumSplineSweepStep => _groundedSplineSubdivisionDistance;

        #endregion

        #region Flags

        #endregion

        //------------------------------- Unity Methods -------------------------------//

        #region Unity

        private void Start()
        {
            Initialize();
        }

        private void OnDrawGizmosSelected()
        {
            DrawSelectedGizmos();
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
            if (_capsuleCollider == null)
            {
                _capsuleCollider = GetComponent<CapsuleCollider2D>();
            }

            InitializeContactFilter();
        }

        private void InitializeEvents()
        {
        }

        #endregion

        //------------------------------- Core / Routines Methods -------------------------------//

        public Vector2 Resolve(Vector2 bodyPosition, Vector2 intendedDisplacement, ref Vector2 velocity,
            float maximumWalkableSlopeAngle, bool grounded, out RaycastHit2D blockingHit)
        {
            InitializeContactFilter();
            ResetDebugState(bodyPosition, intendedDisplacement);
            blockingHit = default;

            if (_capsuleCollider == null || !_capsuleCollider.enabled)
            {
                _lastResolvedDisplacement = intendedDisplacement;
                return intendedDisplacement;
            }

            Vector2 resolved = ResolveDepenetration(bodyPosition, grounded, maximumWalkableSlopeAngle);
            Vector2 remaining = intendedDisplacement;

            for (int iteration = 0; iteration < _maximumResolutionIterations &&
                remaining.sqrMagnitude > _surfaceNormalTolerance * _surfaceNormalTolerance; iteration++)
            {
                Vector2 origin = bodyPosition + resolved;

                if (!TryFindEarliestHit(origin, remaining, grounded, maximumWalkableSlopeAngle, out RaycastHit2D hit))
                {
                    resolved += remaining;
                    break;
                }

                blockingHit = hit;
                RecordContact(hit);
                float movementDistance = remaining.magnitude;
                float safeDistance = Mathf.Clamp(hit.distance - _collisionSkin, 0f, movementDistance);
                Vector2 safeMovement = remaining.normalized * safeDistance;
                resolved += safeMovement;
                Vector2 unresolved = remaining - safeMovement;
                RemoveVelocityIntoSurface(ref velocity, hit.normal);

                if (!IsWalkableSupport(hit.normal, grounded, maximumWalkableSlopeAngle))
                {
                    Vector2 separation = ResolveSeparationPush(bodyPosition + resolved, hit.normal);
                    resolved += separation;
                    _lastSeparationPush = separation;
                }

                remaining = Vector2.ProjectOnPlane(unresolved, hit.normal);
            }

            _lastResolvedDisplacement = resolved;
            return resolved;
        }

        private void InitializeContactFilter()
        {
            _contactFilter.SetLayerMask(_collisionLayers);
            _contactFilter.useLayerMask = true;
            _contactFilter.useTriggers = false;
        }

        private Vector2 ResolveDepenetration(Vector2 bodyPosition, bool grounded, float maximumWalkableSlopeAngle)
        {
            if (_maximumDepenetrationDistance <= 0f || bodyPosition != (Vector2)transform.position)
            {
                return Vector2.zero;
            }

            GetCapsuleShape(bodyPosition, out Vector2 center, out Vector2 size, out CapsuleDirection2D direction, out float angle);
            int overlapCount = Physics2D.OverlapCapsule(center, size, direction, angle, _contactFilter, _overlapBuffer);
            Vector2 smallestCorrection = Vector2.zero;
            float smallestDistance = float.PositiveInfinity;

            for (int index = 0; index < overlapCount; index++)
            {
                Collider2D overlap = _overlapBuffer[index];

                if (!IsValidCollider(overlap))
                {
                    continue;
                }

                ColliderDistance2D distance = Physics2D.Distance(_capsuleCollider, overlap);

                if (!distance.isOverlapped || IsWalkableSupport(distance.normal, grounded, maximumWalkableSlopeAngle))
                {
                    continue;
                }

                float correctionDistance = Mathf.Min(Mathf.Abs(distance.distance), _maximumDepenetrationDistance);

                if (correctionDistance < smallestDistance)
                {
                    smallestDistance = correctionDistance;
                    smallestCorrection = -distance.normal * correctionDistance;
                }
            }

            return smallestCorrection;
        }

        private bool TryFindEarliestHit(Vector2 bodyPosition, Vector2 displacement, bool grounded,
            float maximumWalkableSlopeAngle, out RaycastHit2D earliestHit)
        {
            earliestHit = default;
            float movementDistance = displacement.magnitude;

            if (movementDistance <= _surfaceNormalTolerance)
            {
                return false;
            }

            GetCapsuleShape(bodyPosition, out Vector2 center, out Vector2 size, out CapsuleDirection2D capsuleDirection, out float angle);
            Vector2 direction = displacement / movementDistance;
            int hitCount = Physics2D.CapsuleCast(center, size, capsuleDirection, angle, direction, _contactFilter,
                _hitBuffer, movementDistance + _collisionSkin);
            float earliestDistance = float.PositiveInfinity;

            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit2D hit = _hitBuffer[index];

                if (!IsBlockingHit(hit, displacement, grounded, maximumWalkableSlopeAngle) || hit.distance >= earliestDistance)
                {
                    continue;
                }

                earliestDistance = hit.distance;
                earliestHit = hit;
            }

            return earliestHit.collider != null;
        }

        private bool IsBlockingHit(RaycastHit2D hit, Vector2 displacement, bool grounded, float maximumWalkableSlopeAngle)
        {
            if (!IsValidCollider(hit.collider))
            {
                return false;
            }

            float inwardMotion = Vector2.Dot(displacement, hit.normal);

            if (inwardMotion >= -_surfaceNormalTolerance)
            {
                return false;
            }

            return !IsWalkableSupport(hit.normal, grounded, maximumWalkableSlopeAngle);
        }

        private bool IsValidCollider(Collider2D collider)
        {
            return collider != null && collider != _capsuleCollider && !collider.isTrigger &&
                !collider.transform.IsChildOf(transform);
        }

        private bool IsWalkableSupport(Vector2 normal, bool grounded, float maximumWalkableSlopeAngle)
        {
            return grounded && Vector2.Angle(normal, Vector2.up) <= maximumWalkableSlopeAngle;
        }

        private Vector2 ResolveSeparationPush(Vector2 bodyPosition, Vector2 normal)
        {
            if (_separationPushDistance <= 0f)
            {
                return Vector2.zero;
            }

            Vector2 intendedPush = normal * _separationPushDistance;

            if (!TryFindEarliestHit(bodyPosition, intendedPush, false, 0f, out RaycastHit2D hit))
            {
                return intendedPush;
            }

            float safeDistance = Mathf.Clamp(hit.distance - _collisionSkin, 0f, _separationPushDistance);
            return normal * safeDistance;
        }

        private void RemoveVelocityIntoSurface(ref Vector2 velocity, Vector2 normal)
        {
            float inwardVelocity = Vector2.Dot(velocity, normal);

            if (inwardVelocity < -_surfaceNormalTolerance)
            {
                velocity -= normal * inwardVelocity;
            }
        }

        private void GetCapsuleShape(Vector2 bodyPosition, out Vector2 center, out Vector2 size,
            out CapsuleDirection2D direction, out float angle)
        {
            Vector3 scale = transform.lossyScale;
            center = (Vector2)transform.TransformPoint(_capsuleCollider.offset) + bodyPosition - (Vector2)transform.position;
            size = Vector2.Scale(_capsuleCollider.size, new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y)));
            direction = _capsuleCollider.direction;
            angle = transform.eulerAngles.z;
        }

        private void ResetDebugState(Vector2 bodyPosition, Vector2 intendedDisplacement)
        {
            _lastSweepStart = bodyPosition;
            _lastIntendedDisplacement = intendedDisplacement;
            _lastResolvedDisplacement = Vector2.zero;
            _lastSeparationPush = Vector2.zero;
            _hasLastContact = false;
        }

        private void RecordContact(RaycastHit2D hit)
        {
            _hasLastContact = true;
            _lastContactPoint = hit.point;
            _lastContactNormal = hit.normal;
        }

        private void DrawSelectedGizmos()
        {
            if (!_drawCollisionGizmos)
            {
                return;
            }

            if (_capsuleCollider == null)
            {
                _capsuleCollider = GetComponent<CapsuleCollider2D>();
            }

            if (_capsuleCollider != null)
            {
                Gizmos.color = Color.cyan;
                DrawCapsuleGizmo();
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(_lastSweepStart, _lastSweepStart + _lastIntendedDisplacement);
            Gizmos.color = Color.green;
            Gizmos.DrawLine(_lastSweepStart, _lastSweepStart + _lastResolvedDisplacement);

            if (_hasLastContact)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(_lastContactPoint, _collisionSkin);
                Gizmos.DrawLine(_lastContactPoint, _lastContactPoint + _lastContactNormal * 0.35f);
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(_lastContactPoint, _lastContactPoint + _lastSeparationPush);
            }
        }

        private void DrawCapsuleGizmo()
        {
            Bounds bounds = _capsuleCollider.bounds;
            bool vertical = _capsuleCollider.direction == CapsuleDirection2D.Vertical;
            float radius = vertical ? bounds.extents.x : bounds.extents.y;
            float halfAxis = vertical ? bounds.extents.y : bounds.extents.x;
            float centerOffset = Mathf.Max(0f, halfAxis - radius);
            Vector2 axis = vertical ? Vector2.up : Vector2.right;
            Vector2 side = vertical ? Vector2.right : Vector2.up;
            Vector2 firstCenter = (Vector2)bounds.center - axis * centerOffset;
            Vector2 secondCenter = (Vector2)bounds.center + axis * centerOffset;
            Gizmos.DrawWireSphere(firstCenter, radius);
            Gizmos.DrawWireSphere(secondCenter, radius);
            Gizmos.DrawLine(firstCenter + side * radius, secondCenter + side * radius);
            Gizmos.DrawLine(firstCenter - side * radius, secondCenter - side * radius);
        }

#if UNITY_EDITOR
        //------------------------------- Editor -------------------------------//

        private void OnValidate()
        {
            _collisionSkin = Mathf.Max(0.001f, _collisionSkin);
            _separationPushDistance = Mathf.Max(0f, _separationPushDistance);
            _surfaceNormalTolerance = Mathf.Max(0f, _surfaceNormalTolerance);
            _maximumResolutionIterations = Mathf.Max(1, _maximumResolutionIterations);
            _maximumDepenetrationDistance = Mathf.Max(0f, _maximumDepenetrationDistance);
            _groundedSplineSubdivisionDistance = Mathf.Max(0.01f, _groundedSplineSubdivisionDistance);
            InitializeComponents();
        }
#endif
    }
}
