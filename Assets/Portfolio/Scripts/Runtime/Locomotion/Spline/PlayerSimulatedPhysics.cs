using Portfolio.Locomotion.Core;
using UnityEngine;

namespace Portfolio.Locomotion.Spline
{
    /// <summary>
    /// Owns calculated gravity, displacement resolution, grounding correction, and physics debug data
    /// for a simulated player.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSimulatedPhysics : MonoBehaviour
    {
        //------------------------------- Parameters -------------------------------//

        #region Components

        [Header("STATE")]
        [SerializeField] private LocomotionState _state = new LocomotionState();

        [Header("GRAVITY PARAMETERS")]
        [SerializeField] private float _gravity = 33.35f;
        [SerializeField] private float _jumpAscentGravityMultiplier = 1.1f;
        [SerializeField] private float _fallGravityMultiplier = 2.1f;
        [SerializeField] private float _maximumFallSpeed = 22f;

        [Header("SLOPE PARAMETERS")]
        [SerializeField] private float _steepSlopeCheckDistance = 0.35f;

        [Header("DEMO PARAMETERS")]
        [SerializeField] private float _demoResetY = -8f;

        [Header("DEBUG PARAMETERS")]
        [SerializeField] private bool _drawSelectedGizmos = true;
        [SerializeField] private float _velocityGizmoScale = 0.12f;

        #endregion

        #region Flags

        private Vector2 _velocity;
        private Vector2 _lastDebugOrigin;
        private Vector2 _lastIntendedDisplacement;
        private Vector2 _lastResolvedDisplacement;
        private Vector2 _lastGroundSnapDisplacement;
        private RaycastHit2D _lastBlockingHit;
        private RaycastHit2D _lastGroundSnapHit;
        private bool _movementBlockedBySteepSlope;

        #endregion

        #region Properties

        public LocomotionState State => _state;
        public Vector2 Velocity => _velocity;
        internal ref Vector2 VelocityReference => ref _velocity;
        public bool MovementBlockedBySteepSlope => _movementBlockedBySteepSlope;
        public Vector2 LastIntendedDisplacement => _lastIntendedDisplacement;
        public Vector2 LastResolvedDisplacement => _lastResolvedDisplacement;
        public RaycastHit2D LastBlockingHit => _lastBlockingHit;

        #endregion

        //------------------------------- Core / Routines Methods -------------------------------//

        #region Physics

        public void SetVelocity(Vector2 velocity)
        {
            _velocity = velocity;
        }

        public bool ShouldResetToSpawn(float currentY)
        {
            return currentY < _demoResetY;
        }

        public bool ShouldBlockMovementIntoSteepSlope(
            CapsuleCollider2D capsuleCollider,
            PlayerSimulatedGroundProbe surfaceProbe,
            float movementInput,
            float facingDirection)
        {
            if (capsuleCollider == null || surfaceProbe == null)
            {
                return false;
            }

            float movementDirection = Mathf.Abs(movementInput) > 0.01f
                ? movementInput
                : _velocity.x;

            if (movementDirection * facingDirection <= 0.01f)
            {
                return false;
            }

            return surfaceProbe.HasNearbySteepSlope(
                capsuleCollider,
                facingDirection,
                _steepSlopeCheckDistance);
        }

        public void SetMovementBlockedBySteepSlope(bool blocked)
        {
            _movementBlockedBySteepSlope = blocked;
        }

        public void BeginStep(Vector2 origin)
        {
            _lastDebugOrigin = origin;
        }

        public void ApplyGravity(LocomotionState state, float deltaTime)
        {
            if (state == null || state.Grounded)
            {
                return;
            }

            float multiplier = _velocity.y > 0f
                ? _jumpAscentGravityMultiplier
                : _fallGravityMultiplier;

            _velocity.y = Mathf.Max(
                _velocity.y - _gravity * multiplier * deltaTime,
                -_maximumFallSpeed);
        }

        public Vector2 ResolveDisplacement(
            Vector2 bodyPosition,
            CapsuleCollider2D capsuleCollider,
            Vector2 intendedDisplacement,
            LocomotionState state,
            PlayerSimulatedGroundProbe surfaceProbe,
            PlayerSimulatedCollisionResolver collisionResolver)
        {
            if (state == null || surfaceProbe == null || collisionResolver == null)
            {
                _lastBlockingHit = default;
                _lastIntendedDisplacement = intendedDisplacement;
                _lastResolvedDisplacement = intendedDisplacement;
                return intendedDisplacement;
            }

            Vector2 resolvedDisplacement = collisionResolver.Resolve(
                bodyPosition,
                capsuleCollider,
                intendedDisplacement,
                ref _velocity,
                state.Grounded,
                surfaceProbe.MaximumWalkableSlopeAngle,
                out RaycastHit2D blockingHit);

            _lastBlockingHit = blockingHit;
            _lastIntendedDisplacement = intendedDisplacement;
            _lastResolvedDisplacement = resolvedDisplacement;
            return resolvedDisplacement;
        }

        public void ApplyResolvedSurface(
            RaycastHit2D blockingHit,
            Vector2 intendedDisplacement,
            PlayerSimulatedGroundProbe surfaceProbe,
            LocomotionState state)
        {
            if (blockingHit.collider == null ||
                intendedDisplacement.y > 0f ||
                surfaceProbe == null ||
                state == null ||
                !surfaceProbe.IsWalkableSurface(blockingHit.normal))
            {
                return;
            }

            surfaceProbe.ApplySurfaceHit(blockingHit, state);
        }

        public void ApplyGroundSnap(
            Transform characterTransform,
            CapsuleCollider2D capsuleCollider,
            bool wasGrounded,
            bool jumpAscentActive,
            PlayerSimulatedGroundProbe surfaceProbe,
            PlayerSimulatedCollisionResolver collisionResolver,
            LocomotionState state)
        {
            _lastGroundSnapDisplacement = Vector2.zero;
            _lastGroundSnapHit = default;

            if (characterTransform == null ||
                capsuleCollider == null ||
                surfaceProbe == null ||
                collisionResolver == null ||
                state == null ||
                ((!wasGrounded && !state.Grounded) || jumpAscentActive))
            {
                return;
            }

            float verticalRange =
                surfaceProbe.GroundSnapDistance +
                surfaceProbe.ProbeDistance +
                collisionResolver.CollisionSkin;

            if (!surfaceProbe.TryGetGroundHitNearPosition(
                capsuleCollider,
                verticalRange,
                out RaycastHit2D groundHit))
            {
                return;
            }

            Bounds bounds = capsuleCollider.bounds;
            float surfaceSlope = -groundHit.normal.x / Mathf.Max(groundHit.normal.y, 0.01f);
            float groundYAtCenter = groundHit.point.y +
                surfaceSlope * (bounds.center.x - groundHit.point.x);
            float verticalCorrection = groundYAtCenter - bounds.min.y;
            float maximumCorrection = verticalRange - collisionResolver.CollisionSkin;

            if (Mathf.Abs(verticalCorrection) > maximumCorrection)
            {
                return;
            }

            _lastGroundSnapHit = groundHit;

            if (Mathf.Abs(verticalCorrection) > 0.0001f)
            {
                _lastGroundSnapDisplacement = Vector2.up * verticalCorrection;
                characterTransform.Translate(_lastGroundSnapDisplacement, Space.World);
                Physics2D.SyncTransforms();
            }

            Vector2 tangent = surfaceProbe.GetSlopeTangent(groundHit.normal);
            if (Vector2.Dot(tangent, _velocity) < 0f)
            {
                tangent *= -1f;
            }

            _velocity = tangent * Vector2.Dot(_velocity, tangent);
            surfaceProbe.ApplySurfaceHit(groundHit, state);
        }

        public void ResetRuntimeState(Vector2 position)
        {
            _velocity = Vector2.zero;
            _lastDebugOrigin = position;
            _lastIntendedDisplacement = Vector2.zero;
            _lastResolvedDisplacement = Vector2.zero;
            _lastGroundSnapDisplacement = Vector2.zero;
            _lastBlockingHit = default;
            _lastGroundSnapHit = default;
            _movementBlockedBySteepSlope = false;
        }

        public void DrawSelectedGizmos(
            Transform playerTransform,
            CapsuleCollider2D capsuleCollider,
            Vector2 velocity,
            LocomotionState state,
            float facingDirection)
        {
            if (!_drawSelectedGizmos || playerTransform == null)
            {
                return;
            }

            if (capsuleCollider == null)
            {
                capsuleCollider = playerTransform.GetComponent<CapsuleCollider2D>();
            }

            if (capsuleCollider == null || state == null)
            {
                return;
            }

            Bounds bounds = capsuleCollider.bounds;
            Vector3 origin = Application.isPlaying
                ? (Vector3)_lastDebugOrigin
                : bounds.center;
            origin.y = bounds.center.y;

            Gizmos.color = new Color(1f, 0.55f, 0.2f, 1f);
            Gizmos.DrawWireCube(bounds.center, bounds.size);

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(
                origin,
                origin + (Vector3)(velocity * _velocityGizmoScale));

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(
                origin,
                origin + (Vector3)_lastIntendedDisplacement);

            Gizmos.color = Color.green;
            Gizmos.DrawLine(
                origin,
                origin + (Vector3)_lastResolvedDisplacement);

            Vector3 groundOrigin = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(
                groundOrigin,
                groundOrigin + (Vector3)state.SurfaceNormal);

            Gizmos.color = _movementBlockedBySteepSlope
                ? Color.red
                : new Color(1f, 0.6f, 0f, 1f);
            Gizmos.DrawLine(
                bounds.center,
                bounds.center + Vector3.right * facingDirection * _steepSlopeCheckDistance);

            DrawCollisionHitGizmo(_lastBlockingHit, Color.red);
            DrawCollisionHitGizmo(_lastGroundSnapHit, Color.magenta);

            if (_lastGroundSnapDisplacement.sqrMagnitude > 0f)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(
                    origin + (Vector3)_lastResolvedDisplacement,
                    origin + (Vector3)(_lastResolvedDisplacement + _lastGroundSnapDisplacement));
            }
        }

        public void Validate()
        {
            _gravity = Mathf.Max(0f, _gravity);
            _jumpAscentGravityMultiplier = Mathf.Max(0f, _jumpAscentGravityMultiplier);
            _fallGravityMultiplier = Mathf.Max(0f, _fallGravityMultiplier);
            _maximumFallSpeed = Mathf.Max(0f, _maximumFallSpeed);
            _steepSlopeCheckDistance = Mathf.Max(0.01f, _steepSlopeCheckDistance);
            _velocityGizmoScale = Mathf.Max(0f, _velocityGizmoScale);
        }

        private static void DrawCollisionHitGizmo(RaycastHit2D hit, Color color)
        {
            if (hit.collider == null)
            {
                return;
            }

            Gizmos.color = color;
            Gizmos.DrawSphere(hit.point, 0.06f);
            Gizmos.DrawLine(hit.point, hit.point + hit.normal * 0.5f);
        }

        #endregion

#if UNITY_EDITOR
        //------------------------------- Editor -------------------------------//

        #region Editor

        private void OnValidate()
        {
            Validate();
        }

        #endregion
#endif
    }
}
