using Portfolio.Locomotion.Core;
using UnityEngine;

namespace Portfolio.Locomotion.Rigidbody
{
    [System.Serializable]
    public class PlayerRigidbodySurfaceProbe
    {
        //------------------------------- Parameters -------------------------------//

        #region Components

        [Header("GROUND CHECK PARAMETERS")]
        [SerializeField] private LayerMask _groundLayers = Physics2D.DefaultRaycastLayers;
        [SerializeField] private float _probeDistance = 0.2f;
        [SerializeField, HideInInspector] private Transform _groundPoint;
        [SerializeField] private float _groundPointOriginOffset = 0.03f;
        [SerializeField] private float _probeInset = 0.04f;
        [SerializeField] private float _risingGroundedVelocityThreshold = 0.75f;
        [SerializeField] private float _groundingGraceDuration = 0.06f;

        [Header("SLOPE CHECK PARAMETERS")]
        [SerializeField] private float _slopeAngleThreshold = 1f;
        [SerializeField] private float _maximumWalkableSlopeAngle = 45f;
        [SerializeField] private float _steepSlopeJumpRestrictionAngle = 55f;

        #endregion

        #region Flags

        private readonly RaycastHit2D[] _hits = new RaycastHit2D[32];
        private readonly ContactPoint2D[] _contacts = new ContactPoint2D[32];
        private ContactFilter2D _contactFilter;
        private bool _initialized;
        private Collider2D _surfaceCollider;
        private Vector2 _surfaceNormal = Vector2.up;
        private bool _hasDirectSurface;
        private float _groundedTime;
        private float _groundingGraceRemainingTime;
        private string _lastProbeResult = "Not probed";
        private string _lastProbeDetails = "Not probed";

        #endregion

        #region Properties

        public string LastProbeResult => _lastProbeResult;
        public string LastProbeDetails => _lastProbeDetails;
        public float ProbeDistance
        {
            get => _probeDistance;
            set => _probeDistance = Mathf.Max(0.01f, value);
        }
        public LayerMask GroundLayers
        {
            get => _groundLayers;
            set { _groundLayers = value; _initialized = false; }
        }
        public float ProbeInset
        {
            get => _probeInset;
            set => _probeInset = Mathf.Max(0f, value);
        }
        public float GroundPointOriginOffset
        {
            get => _groundPointOriginOffset;
            set => _groundPointOriginOffset = Mathf.Max(0.001f, value);
        }
        public Transform GroundPoint
        {
            get => _groundPoint;
            set => _groundPoint = value;
        }
        public float RisingGroundedVelocityThreshold
        {
            get => _risingGroundedVelocityThreshold;
            set => _risingGroundedVelocityThreshold = Mathf.Max(0f, value);
        }
        public float GroundingGraceDuration
        {
            get => _groundingGraceDuration;
            set => _groundingGraceDuration = Mathf.Max(0f, value);
        }
        public float SlopeAngleThreshold
        {
            get => _slopeAngleThreshold;
            set => _slopeAngleThreshold = Mathf.Clamp(value, 0f, 89f);
        }
        public float MaximumWalkableSlopeAngle
        {
            get => _maximumWalkableSlopeAngle;
            set => _maximumWalkableSlopeAngle = Mathf.Clamp(value, _slopeAngleThreshold, 89f);
        }
        public float SteepSlopeJumpRestrictionAngle
        {
            get => _steepSlopeJumpRestrictionAngle;
            set => _steepSlopeJumpRestrictionAngle = Mathf.Clamp(value, _maximumWalkableSlopeAngle, 89f);
        }
        public Collider2D SurfaceCollider => _surfaceCollider;
        public Vector2 SurfaceNormal => _surfaceNormal;
        public bool HasDirectSurface => _hasDirectSurface;
        public float GroundedTime => _groundedTime;
        public bool IsSteepSlopeJumpRestricted =>
            _surfaceCollider != null && Vector2.Angle(_surfaceNormal, Vector2.up) >= _steepSlopeJumpRestrictionAngle;

        #endregion

        //------------------------------- Initialization Methods -------------------------------//

        #region Initialization

        public void Initialize()
        {
            int groundLayer = LayerMask.NameToLayer("Ground");

            if (groundLayer >= 0)
            {
                _groundLayers |= 1 << groundLayer;
            }

            _contactFilter = new ContactFilter2D();
            _contactFilter.SetLayerMask(_groundLayers);
            _contactFilter.useTriggers = false;
            _initialized = true;
        }

        #endregion

        //------------------------------- Core / Routines Methods -------------------------------//

        /// <summary>
        /// Explicit grounded query. Probe performs the filtered contact, Ground-layer raycast
        /// array and capsule-cast checks, then records the accepted result in LocomotionState.
        /// </summary>
        public bool CheckGrounded(
            CapsuleCollider2D capsuleCollider,
            UnityEngine.Rigidbody2D body,
            LocomotionState state,
            bool ignoreGrounding)
        {
            if (state == null)
                return false;

            Probe(capsuleCollider, body, state, ignoreGrounding);
            return state.Grounded;
        }

        /// <summary>
        /// Records the accepted result of PlayerRigidbodyPhysics' lightweight circle cast.
        /// Ground-query ownership stays in PlayerRigidbodyPhysics; this module keeps the support normal
        /// and collider required by slope movement and moving-platform synchronization.
        /// </summary>
        public void ApplyGroundHit(RaycastHit2D hit, LocomotionState state)
        {
            if (state == null)
            {
                return;
            }

            if (hit.collider == null)
            {
                ResetGrounding(state);
                return;
            }

            _lastProbeResult = $"CircleCast support: {hit.collider.name}";
            _lastProbeDetails = $"CircleCast collider={hit.collider.name}, point={hit.point}, normal={hit.normal}";
            ApplySurface(hit.collider, hit.normal, state);
        }

        /// <summary>
        /// Uses a walkable Rigidbody contact when the ground cast hits a steep face at its base.
        /// </summary>
        public bool TryApplyWalkableBodyContact(
            UnityEngine.Rigidbody2D body,
            CapsuleCollider2D capsuleCollider,
            LocomotionState state)
        {
            if (state == null || capsuleCollider == null ||
                !TryGetBodyContact(body, capsuleCollider, out Collider2D collider, out Vector2 normal))
            {
                return false;
            }

            float slopeAngle = Vector2.Angle(normal, Vector2.up);
            if (normal.y <= 0f || slopeAngle > _maximumWalkableSlopeAngle)
            {
                return false;
            }

            _lastProbeResult = $"Walkable body contact: {collider.name}";
            _lastProbeDetails = $"Walkable contact collider={collider.name}, normal={normal}";
            ApplySurface(collider, normal, state);
            return true;
        }

        /// <summary>
        /// Keeps the last standable platform as support during the small gap allowed by PlayerRigidbodyPhysics.
        /// </summary>
        public void ApplyPersistentGrounding(Collider2D surfaceCollider, LocomotionState state)
        {
            if (state == null || surfaceCollider == null)
            {
                if (state != null)
                {
                    ResetGrounding(state);
                }

                return;
            }

            Vector2 normal = _surfaceNormal.sqrMagnitude > 0f ? _surfaceNormal.normalized : Vector2.up;
            float slopeAngle = Vector2.Angle(normal, Vector2.up);
            bool walkable = normal.y > 0f && slopeAngle <= _maximumWalkableSlopeAngle;
            bool onWalkableSlope = walkable && slopeAngle > _slopeAngleThreshold;
            bool onSteepSlope = normal.y > 0f && !walkable;

            _hasDirectSurface = false;
            _surfaceCollider = surfaceCollider;
            _surfaceNormal = normal;
            _groundedTime += Time.fixedDeltaTime;
            _lastProbeResult = $"Persistent platform support: {surfaceCollider.name}";
            _lastProbeDetails = $"Platform persistence collider={surfaceCollider.name}, normal={normal}";
            state.SetSurface(true, onWalkableSlope, onSteepSlope, normal, slopeAngle);
        }

        /// <summary>
        /// Clears support data when the PlayerRigidbodyPhysics grounding query is rejected or has no hit.
        /// </summary>
        public void ResetGrounding(LocomotionState state)
        {
            _lastProbeResult = "No support hit: not grounded";
            _lastProbeDetails = "Grounding reset by PlayerRigidbodyPhysics.";
            if (state != null)
            {
                ClearSurface(state);
            }
        }

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

            _lastProbeDetails = "Probe started.";
            if (ignoreGrounding)
            {
                _lastProbeResult = "Grounding ignored";
                _lastProbeDetails = "Grounding ignored by jump suppression.";
                ClearSurface(state);
                return;
            }

            if (TryGetBodyContact(
                body,
                capsuleCollider,
                out Collider2D contactCollider,
                out Vector2 contactNormal))
            {
                if (IsSeparatingFromSurface(body.linearVelocity, contactNormal))
                {
                    _lastProbeResult = "Body contact rejected: moving away from surface";
                    _lastProbeDetails = $"Body contact rejected: collider={contactCollider.name}, normal={contactNormal}, velocity={body.linearVelocity}, velocityAlongNormal={Vector2.Dot(body.linearVelocity, contactNormal):F3}, threshold={_risingGroundedVelocityThreshold:F3}.";
                    ClearSurface(state);
                    return;
                }

                _lastProbeResult = $"Body contact: {contactCollider.name}";
                _lastProbeDetails = $"Body contact collider={contactCollider.name}, normal={contactNormal}";
                ApplySurface(contactCollider, contactNormal, state);
                return;
            }

            Bounds bounds = capsuleCollider.bounds;
            float rayLength = bounds.extents.y + _probeDistance;
            Vector2 originCenter = bounds.center;

            if (_groundPoint != null)
            {
                originCenter = (Vector2)_groundPoint.position + Vector2.up * _groundPointOriginOffset;
                rayLength = _probeDistance + _groundPointOriginOffset;
            }

            Vector2 originLeft = new Vector2(bounds.min.x + _probeInset, originCenter.y);
            Vector2 originRight = new Vector2(bounds.max.x - _probeInset, originCenter.y);

            int groundLayer = LayerMask.NameToLayer("Ground");
            _lastProbeDetails =
                $"Ground layer index={groundLayer}, mask bits={_groundLayers.value}, " +
                $"Ground included={groundLayer >= 0 && (_groundLayers.value & (1 << groundLayer)) != 0}. ";
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
            CollectBestShapeCastHits(
                capsuleCollider,
                _probeDistance,
                ref bestWalkableHit,
                ref bestSteepHit);

            RaycastHit2D bestHit = bestWalkableHit.collider != null
                ? bestWalkableHit
                : bestSteepHit;
            bool hasHit = bestHit.collider != null;

            if (!hasHit)
            {
                HandleMissingSurface(body.linearVelocity, state);
                return;
            }

            if (IsSeparatingFromSurface(body.linearVelocity, bestHit.normal))
            {
                _lastProbeResult = $"Raycast rejected: moving away from {bestHit.collider.name}";
                ClearSurface(state);
                return;
            }

            _lastProbeResult = $"Raycast support: {bestHit.collider.name}";
            ApplySurface(bestHit.collider, bestHit.normal, state);
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
            _groundPointOriginOffset = Mathf.Max(0.001f, _groundPointOriginOffset);
            _probeInset = Mathf.Max(0f, _probeInset);
            _risingGroundedVelocityThreshold = Mathf.Max(0f, _risingGroundedVelocityThreshold);
            _groundingGraceDuration = Mathf.Max(0f, _groundingGraceDuration);
            _slopeAngleThreshold = Mathf.Clamp(_slopeAngleThreshold, 0f, 89f);
            _maximumWalkableSlopeAngle = Mathf.Clamp(_maximumWalkableSlopeAngle, _slopeAngleThreshold, 89f);
            _steepSlopeJumpRestrictionAngle = Mathf.Clamp(
                _steepSlopeJumpRestrictionAngle,
                _maximumWalkableSlopeAngle,
                89f);
        }

        private void ClearSurface(LocomotionState state)
        {
            _hasDirectSurface = false;
            _surfaceCollider = null;
            _surfaceNormal = Vector2.up;
            _groundedTime = 0f;
            _groundingGraceRemainingTime = 0f;
            state.SetSurface(false, false, false, Vector2.up, 0f);
        }

        private void HandleMissingSurface(Vector2 velocity, LocomotionState state)
        {
            _hasDirectSurface = false;

            if (_surfaceCollider == null ||
                _groundingGraceRemainingTime <= 0f ||
                IsSeparatingFromSurface(velocity, _surfaceNormal))
            {
                _lastProbeResult = "No support hit: not grounded";
                ClearSurface(state);
                return;
            }

            _groundingGraceRemainingTime = Mathf.Max(
                0f,
                _groundingGraceRemainingTime - Time.fixedDeltaTime);

            if (_groundingGraceRemainingTime <= 0f)
            {
                _lastProbeResult = "No support hit: grounding grace expired";
                ClearSurface(state);
                return;
            }

            _lastProbeResult = "No direct support hit: using grounding grace";

            float slopeAngle = Vector2.Angle(_surfaceNormal, Vector2.up);
            bool walkable = _surfaceNormal.y > 0f &&
                slopeAngle <= _maximumWalkableSlopeAngle;
            bool onWalkableSlope = walkable && slopeAngle > _slopeAngleThreshold;
            bool onSteepSlope = _surfaceNormal.y > 0f && !walkable;

            state.SetSurface(
                true,
                onWalkableSlope,
                onSteepSlope,
                _surfaceNormal,
                slopeAngle);
        }

        private bool TryGetBodyContact(
            UnityEngine.Rigidbody2D body,
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
            float centerY = capsuleCollider.bounds.center.y;

            for (int i = 0; i < contactCount; i++)
            {
                ContactPoint2D contact = _contacts[i];
                Collider2D collider = contact.collider == capsuleCollider
                    ? contact.otherCollider
                    : contact.collider;

                if (!IsValidCollider(collider, capsuleCollider) ||
                    contact.point.y > centerY ||
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
            _hasDirectSurface = true;
            float slopeAngle = Vector2.Angle(surfaceNormal, Vector2.up);
            bool onWalkableSlope = surfaceNormal.y > 0f &&
                slopeAngle > _slopeAngleThreshold &&
                slopeAngle <= _maximumWalkableSlopeAngle;
            bool onSteepSlope = surfaceNormal.y > 0f &&
                slopeAngle > _maximumWalkableSlopeAngle;

            _surfaceCollider = surfaceCollider;
            _surfaceNormal = surfaceNormal.normalized;
            _groundedTime += Time.fixedDeltaTime;
            _groundingGraceRemainingTime = _groundingGraceDuration;
            state.SetSurface(
                true,
                onWalkableSlope,
                onSteepSlope,
                _surfaceNormal,
                slopeAngle);
        }

        private bool IsValidCollider(
            Collider2D collider,
            Collider2D characterCollider)
        {
            return collider != null &&
                collider != characterCollider &&
                !collider.isTrigger &&
                collider.attachedRigidbody != characterCollider.attachedRigidbody;
        }

        private void CollectBestSupportHits(
            Vector2 origin,
            float distance,
            Collider2D ignoredCollider,
            ref RaycastHit2D bestWalkableHit,
            ref RaycastHit2D bestSteepHit)
        {
            int hitCount = Physics2D.Raycast(origin, Vector2.down, _contactFilter, _hits, distance);
            _lastProbeDetails += $"Ray origin={origin}, length={distance:F3}, hits={hitCount}. ";

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit2D hit = _hits[i];

                if (hit.collider == null)
                {
                    _lastProbeDetails += $"Hit {i}: null collider. ";
                    continue;
                }

                float hitSlopeAngle = Vector2.Angle(hit.normal, Vector2.up);
                _lastProbeDetails +=
                    $"Hit {i}: {hit.collider.name}, layer={LayerMask.LayerToName(hit.collider.gameObject.layer)} " +
                    $"({hit.collider.gameObject.layer}), distance={hit.distance:F3}, normal={hit.normal}, " +
                    $"angle={hitSlopeAngle:F1}, trigger={hit.collider.isTrigger}. ";

                if (!IsValidCollider(hit.collider, ignoredCollider) || hit.normal.y <= 0f)
                {
                    _lastProbeDetails += $"Hit {i} rejected: self/same Rigidbody, trigger, or non-upward normal. ";
                    continue;
                }

                float slopeAngle = hitSlopeAngle;

                if (slopeAngle <= _maximumWalkableSlopeAngle)
                {
                    SetCloserHit(hit, ref bestWalkableHit);
                    continue;
                }

                SetCloserHit(hit, ref bestSteepHit);
            }
        }

        private void CollectBestShapeCastHits(
            CapsuleCollider2D capsuleCollider,
            float distance,
            ref RaycastHit2D bestWalkableHit,
            ref RaycastHit2D bestSteepHit)
        {
            int hitCount = capsuleCollider.Cast(Vector2.down, _contactFilter, _hits, distance);
            _lastProbeDetails += $"Capsule cast direction=down, distance={distance:F3}, hits={hitCount}. ";

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit2D hit = _hits[i];

                if (hit.collider == null)
                {
                    _lastProbeDetails += $"Cast hit {i}: null collider. ";
                    continue;
                }

                float hitSlopeAngle = Vector2.Angle(hit.normal, Vector2.up);
                _lastProbeDetails +=
                    $"Cast hit {i}: {hit.collider.name}, layer={LayerMask.LayerToName(hit.collider.gameObject.layer)} " +
                    $"({hit.collider.gameObject.layer}), distance={hit.distance:F3}, normal={hit.normal}, " +
                    $"angle={hitSlopeAngle:F1}, trigger={hit.collider.isTrigger}. ";

                if (hit.collider.isTrigger || hit.normal.y <= 0f)
                {
                    continue;
                }

                float slopeAngle = hitSlopeAngle;

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

        private bool IsSeparatingFromSurface(Vector2 velocity, Vector2 surfaceNormal)
        {
            // Horizontal locomotion across a slope can have a large velocity component along
            // the surface normal even when the character is not jumping away from the ground.
            // Use upward world-space velocity for jump separation so lateral movement alone
            // cannot invalidate grounding on sloped terrain.
            return velocity.y > _risingGroundedVelocityThreshold;
        }

    }
}
