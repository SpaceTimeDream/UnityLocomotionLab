using UnityEngine;

namespace Portfolio.Locomotion.Spline
{
    public class SimulatedCollisionResolver2D : MonoBehaviour
    {
        //------------------------------- Game Pause -------------------------------//

        //------------------------------- Debugging Methods -------------------------------//

        //------------------------------- Parameters -------------------------------//

        #region Components

        [Header("COLLISION")]
        [SerializeField] private LayerMask _collisionLayers = Physics2D.DefaultRaycastLayers;
        [SerializeField, Min(0.001f)] private float _collisionSkin = 0.03f;
        [SerializeField, Min(0.05f)] private float _bodyWidth = 0.72f;
        [SerializeField, Min(0.05f)] private float _bodyHeight = 1.68f;
        [SerializeField] private Vector2 _bodyCenterOffset = new Vector2(0f, 0.84f);
        [SerializeField, Min(0f)] private float _rayInset = 0.04f;
        [SerializeField, Min(2)] private int _horizontalRayCount = 3;
        [SerializeField, Min(2)] private int _verticalRayCount = 3;
        [SerializeField, Min(1)] private int _maximumResolutionIterations = 3;
        [SerializeField] private bool _drawCollisionGizmos;

        private readonly RaycastHit2D[] _hitBuffer = new RaycastHit2D[16];
        private readonly Collider2D[] _overlapBuffer = new Collider2D[16];
        private ContactFilter2D _contactFilter;
        private Vector2 _lastSweepStart;
        private Vector2 _lastSweepDisplacement;
        private Vector2 _lastContactPoint;
        private Vector2 _lastContactNormal;
        private bool _hasLastContact;

        public float CollisionSkin => _collisionSkin;
        public float MaximumSplineSweepStep => Mathf.Max(_collisionSkin, Mathf.Min(_bodyWidth, _bodyHeight) * 0.5f);

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
            InitializeContactFilter();
        }

        private void InitializeEvents()
        {
        }

        #endregion

        //------------------------------- Core / Routines Methods -------------------------------//

        public Vector2 Resolve(Vector2 bodyPosition, Vector2 intendedDisplacement, ref Vector2 velocity, out RaycastHit2D blockingHit)
        {
            InitializeContactFilter();
            _lastSweepStart = bodyPosition;
            _lastSweepDisplacement = intendedDisplacement;
            _hasLastContact = false;
            blockingHit = default;
            Vector2 resolved = Vector2.zero;
            Vector2 remaining = intendedDisplacement;

            for (int iteration = 0; iteration < _maximumResolutionIterations && remaining.sqrMagnitude > 0.0000001f; iteration++)
            {
                Vector2 origin = bodyPosition + resolved;

                if (!TryFindEarliestHit(origin + _bodyCenterOffset, remaining, out RaycastHit2D hit))
                {
                    if (!WouldOverlap(origin + remaining + _bodyCenterOffset))
                    {
                        resolved += remaining;
                        break;
                    }

                    _hasLastContact = true;
                    _lastContactPoint = origin + remaining;
                    _lastContactNormal = -remaining.normalized;
                    break;
                }

                blockingHit = hit;
                _hasLastContact = true;
                _lastContactPoint = hit.collider != null ? hit.point : origin + remaining;
                _lastContactNormal = hit.normal;
                float movementDistance = remaining.magnitude;
                float safeDistance = hit.collider != null ? Mathf.Max(0f, hit.distance - _collisionSkin) : 0f;
                Vector2 safeMovement = remaining.normalized * Mathf.Min(safeDistance, movementDistance);
                resolved += safeMovement;
                Vector2 unresolved = remaining - safeMovement;
                float inwardVelocity = Vector2.Dot(velocity, hit.normal);

                if (inwardVelocity < 0f)
                {
                    velocity -= hit.normal * inwardVelocity;
                }

                remaining = Vector2.ProjectOnPlane(unresolved, hit.normal);
            }

            return resolved;
        }

        private void InitializeContactFilter()
        {
            _contactFilter.SetLayerMask(_collisionLayers);
            _contactFilter.useLayerMask = true;
            _contactFilter.useTriggers = false;
        }

        private bool TryFindEarliestHit(Vector2 bodyPosition, Vector2 displacement, out RaycastHit2D earliestHit)
        {
            earliestHit = default;
            float earliestDistance = float.PositiveInfinity;
            Vector2 direction = displacement.normalized;
            float castDistance = displacement.magnitude + _collisionSkin;

            if (Mathf.Abs(displacement.x) > 0.00001f)
            {
                float x = bodyPosition.x + Mathf.Sign(displacement.x) * (_bodyWidth * 0.5f - _rayInset);
                CastEdgeRays(new Vector2(x, bodyPosition.y - _bodyHeight * 0.5f + _rayInset), Vector2.up, _bodyHeight - _rayInset * 2f,
                    _horizontalRayCount, direction, castDistance, ref earliestHit, ref earliestDistance);
            }

            if (Mathf.Abs(displacement.y) > 0.00001f)
            {
                float y = bodyPosition.y + Mathf.Sign(displacement.y) * (_bodyHeight * 0.5f - _rayInset);
                CastEdgeRays(new Vector2(bodyPosition.x - _bodyWidth * 0.5f + _rayInset, y), Vector2.right, _bodyWidth - _rayInset * 2f,
                    _verticalRayCount, direction, castDistance, ref earliestHit, ref earliestDistance);
            }

            return earliestHit.collider != null;
        }

        private void CastEdgeRays(Vector2 firstOrigin, Vector2 edgeDirection, float edgeLength, int rayCount, Vector2 direction,
            float castDistance, ref RaycastHit2D earliestHit, ref float earliestDistance)
        {
            float interval = rayCount > 1 ? edgeLength / (rayCount - 1) : 0f;

            for (int rayIndex = 0; rayIndex < rayCount; rayIndex++)
            {
                Vector2 origin = firstOrigin + edgeDirection * (interval * rayIndex);
                int hitCount = Physics2D.Raycast(origin, direction, _contactFilter, _hitBuffer, castDistance);

                for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
                {
                    RaycastHit2D hit = _hitBuffer[hitIndex];

                    if (!IsBlockingHit(hit, direction) || hit.distance >= earliestDistance)
                    {
                        continue;
                    }

                    earliestDistance = hit.distance;
                    earliestHit = hit;
                }
            }
        }

        private bool IsBlockingHit(RaycastHit2D hit, Vector2 direction)
        {
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform) || hit.collider.isTrigger)
            {
                return false;
            }

            return Vector2.Dot(direction, hit.normal) < -0.0001f;
        }

        private bool WouldOverlap(Vector2 bodyPosition)
        {
            Vector2 size = new Vector2(Mathf.Max(0.01f, _bodyWidth - _collisionSkin * 2f), Mathf.Max(0.01f, _bodyHeight - _collisionSkin * 2f));
            int overlapCount = Physics2D.OverlapBox(bodyPosition, size, 0f, _contactFilter, _overlapBuffer);

            for (int i = 0; i < overlapCount; i++)
            {
                Collider2D overlap = _overlapBuffer[i];

                if (overlap != null && !overlap.isTrigger && !overlap.transform.IsChildOf(transform))
                {
                    return true;
                }
            }

            return false;
        }

        private void DrawSelectedGizmos()
        {
            if (!_drawCollisionGizmos)
            {
                return;
            }

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube((Vector2)transform.position + _bodyCenterOffset, new Vector3(_bodyWidth, _bodyHeight, 0f));
            Gizmos.color = _hasLastContact ? Color.red : Color.yellow;
            Gizmos.DrawLine(_lastSweepStart, _lastSweepStart + _lastSweepDisplacement);

            if (_hasLastContact)
            {
                Gizmos.DrawSphere(_lastContactPoint, _collisionSkin);
                Gizmos.DrawLine(_lastContactPoint, _lastContactPoint + _lastContactNormal * 0.35f);
            }
        }

#if UNITY_EDITOR
        //------------------------------- Editor -------------------------------//

        private void OnValidate()
        {
            _collisionSkin = Mathf.Max(0.001f, _collisionSkin);
            _bodyWidth = Mathf.Max(0.05f, _bodyWidth);
            _bodyHeight = Mathf.Max(0.05f, _bodyHeight);
            _rayInset = Mathf.Clamp(_rayInset, 0f, Mathf.Min(_bodyWidth, _bodyHeight) * 0.49f);
            _horizontalRayCount = Mathf.Max(2, _horizontalRayCount);
            _verticalRayCount = Mathf.Max(2, _verticalRayCount);
            _maximumResolutionIterations = Mathf.Max(1, _maximumResolutionIterations);
            InitializeContactFilter();
        }
#endif
    }
}
