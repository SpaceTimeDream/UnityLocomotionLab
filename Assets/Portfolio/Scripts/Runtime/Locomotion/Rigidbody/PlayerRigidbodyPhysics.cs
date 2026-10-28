using Portfolio.Locomotion.Core;
using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Portfolio.Locomotion.Rigidbody
{
    /// <summary>
    /// Owns ground checks, slope detection, gravity, external forces, platform synchronization,
    /// and ground adhesion for a Rigidbody2D player.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerRigidbodyPhysics : MonoBehaviour
    {
        //------------------------------- Parameters -------------------------------//

        #region Components

        [Header("PHYSICS STATE")]
        [SerializeField] private LocomotionState _state = new LocomotionState();

        [Header("GROUNDED CHECK PARAMETERS")]
        [SerializeField] private Transform _groundPoint;
        [SerializeField, Min(0f)] private float _minGroundedDistance = 0.1f;
        [SerializeField, Min(0f)] private float _platformPersistenceVerticalThreshold = 0.35f;
        [SerializeField, Min(0f)] private float _platformPersistenceHorizontalPadding = 0.15f;

        [Header("GROUNDING AND SLOPE PROBE")]
        [SerializeField] private PlayerRigidbodySurfaceProbe _surfaceProbe = new PlayerRigidbodySurfaceProbe();

        [Header("EXTERNAL FORCES")]
        [SerializeField] private PlayerRigidbodyExternalForceReceiver _externalForces = new PlayerRigidbodyExternalForceReceiver();

        [Header("MOVING PLATFORMS")]
        [SerializeField] private PlayerRigidbodyPlatformTracker _movingPlatforms = new PlayerRigidbodyPlatformTracker();

        [Header("GRAVITY PARAMETERS")]
        [InspectorName("gravityScale")]
        [SerializeField, Min(0f)] private float _gravityScale = 3.4f;
        [SerializeField, Min(0f)] private float Default_GravityScale = 3.4f;
        [SerializeField, Min(0f)] private float _baseGravity = 9.81f;
        [SerializeField, Min(0f)] private float _jumpAscentGravityMultiplier = 1.1f;
        [SerializeField, Min(0f)] private float _fallGravityMultiplier = 2.1f;
        [FormerlySerializedAs("_maximumFallSpeed")]
        [InspectorName("fallClamp")]
        [SerializeField, Min(0f)] private float _fallClamp = 22f;

        [Header("SLOPE PARAMETERS")]
        [SerializeField, Min(0.01f)] private float _steepSlopeJumpCheckDistance = 0.35f;
        [SerializeField, Min(0f)] private float _groundAdhesionForce = 20f;
        [SerializeField, Min(0f)] private float _slopeAdherenceFactor = 1f;

        public static event Action<PhysicsState> OnPhysicsStateChanged;
        public event Action<PlayerRigidbodyPhysics> OnGroundAction;

        public LocomotionState State => _state;
        public PlayerRigidbodySurfaceProbe SurfaceProbe => _surfaceProbe;
        public PlayerRigidbodyExternalForceReceiver ExternalForces => _externalForces;
        public PlayerRigidbodyPlatformTracker MovingPlatforms => _movingPlatforms;
        public float gravityScale
        {
            get => _gravityScale;
            set => _gravityScale = Mathf.Max(0f, value);
        }
        public float baseGravity
        {
            get => _baseGravity;
            set => _baseGravity = Mathf.Max(0f, value);
        }
        public float jumpAscentGravityMultiplier
        {
            get => _jumpAscentGravityMultiplier;
            set => _jumpAscentGravityMultiplier = Mathf.Max(0f, value);
        }
        public float fallGravityMultiplier
        {
            get => _fallGravityMultiplier;
            set => _fallGravityMultiplier = Mathf.Max(0f, value);
        }
        public float fallClamp
        {
            get => _fallClamp;
            set => _fallClamp = Mathf.Max(0f, value);
        }
        public Transform groundPoint
        {
            get => _groundPoint != null ? _groundPoint : _surfaceProbe != null ? _surfaceProbe.GroundPoint : null;
            set
            {
                _groundPoint = value;
                if (_surfaceProbe != null)
                {
                    _surfaceProbe.GroundPoint = value;
                }
            }
        }

        public float minGroundedDistance
        {
            get => _minGroundedDistance;
            set => _minGroundedDistance = Mathf.Max(0f, value);
        }

        public float platformPersistenceVerticalThreshold
        {
            get => _platformPersistenceVerticalThreshold;
            set => _platformPersistenceVerticalThreshold = Mathf.Max(0f, value);
        }

        public float platformPersistenceHorizontalPadding
        {
            get => _platformPersistenceHorizontalPadding;
            set => _platformPersistenceHorizontalPadding = Mathf.Max(0f, value);
        }

        public float GroundedTime => _groundedTime;
        public GameObject GroundContactObject => _groundContactObject;

        public float steepSlopeJumpCheckDistance
        {
            get => _steepSlopeJumpCheckDistance;
            set => _steepSlopeJumpCheckDistance = Mathf.Max(0.01f, value);
        }

        public float groundAdhesionForce
        {
            get => _groundAdhesionForce;
            set => _groundAdhesionForce = Mathf.Max(0f, value);
        }

        public float slopeAdherenceFactor
        {
            get => _slopeAdherenceFactor;
            set => _slopeAdherenceFactor = Mathf.Max(0f, value);
        }

        public float DefaultGravityScale
        {
            get => Default_GravityScale;
            set => Default_GravityScale = Mathf.Max(0f, value);
        }

        public LayerMask groundLayers
        {
            get => _surfaceProbe.GroundLayers;
            set => _surfaceProbe.GroundLayers = value;
        }

        public float groundProbeDistance
        {
            get => _surfaceProbe.ProbeDistance;
            set => _surfaceProbe.ProbeDistance = value;
        }

        public float groundPointOriginOffset
        {
            get => _surfaceProbe.GroundPointOriginOffset;
            set => _surfaceProbe.GroundPointOriginOffset = value;
        }

        public float groundProbeInset
        {
            get => _surfaceProbe.ProbeInset;
            set => _surfaceProbe.ProbeInset = value;
        }

        public float risingGroundedVelocityThreshold
        {
            get => _surfaceProbe.RisingGroundedVelocityThreshold;
            set => _surfaceProbe.RisingGroundedVelocityThreshold = value;
        }

        public float groundingGraceDuration
        {
            get => _surfaceProbe.GroundingGraceDuration;
            set => _surfaceProbe.GroundingGraceDuration = value;
        }

        public float slopeAngleThreshold
        {
            get => _surfaceProbe.SlopeAngleThreshold;
            set => _surfaceProbe.SlopeAngleThreshold = value;
        }

        public float maximumWalkableSlopeAngle
        {
            get => _surfaceProbe.MaximumWalkableSlopeAngle;
            set => _surfaceProbe.MaximumWalkableSlopeAngle = value;
        }

        public float steepSlopeJumpRestrictionAngle
        {
            get => _surfaceProbe.SteepSlopeJumpRestrictionAngle;
            set => _surfaceProbe.SteepSlopeJumpRestrictionAngle = value;
        }

        public float horizontalProtectionTime
        {
            get => _externalForces.HorizontalProtectionTime;
            set => _externalForces.HorizontalProtectionTime = value;
        }

        public float externalForceResistance
        {
            get => _externalForces.ExternalForceResistance;
            set => _externalForces.ExternalForceResistance = value;
        }

        public float settledSpeedThreshold
        {
            get => _externalForces.SettledSpeedThreshold;
            set => _externalForces.SettledSpeedThreshold = value;
        }

        public bool movingPlatformTrackingEnabled
        {
            get => _movingPlatforms.Enabled;
            set => _movingPlatforms.Enabled = value;
        }

        public bool IsGrounded => _state != null && _state.Grounded;
        public bool IsOnSlope => _state != null && _state.OnWalkableSlope;
        public bool IsOnSteepSlope => _state != null && _state.OnSteepSlope;
        public bool IsMovingUpSlope => _surfaceProbe != null &&
            _surfaceProbe.IsMovingUpSlope(_controller != null ? _controller.MovementInput : 0f, _state != null ? _state.SurfaceNormal : Vector2.up);
        public bool IsSteepSlopeAhead { get; private set; }
        public float FacingDirection => _controller != null ? _controller.FacingDirection : 1f;
        public float HorizontalDistancePerFixedFrame => _horizontalDistancePerFixedFrame;

        #endregion

        #region Flags

        private PlayerRigidbodyController _controller;
        private Vector2 _fixedStepStartPosition;
        private float _steepSlopeBlockDirection;
        private float _horizontalDistancePerFixedFrame;
        private bool _wasGroundedLastFrame;
        private bool _wasOnSlopeLastFrame;
        private float _groundedTime;
        private GameObject _groundContactObject;
        private bool _isInitialized;
        private PhysicsState _lastPublishedPhysicsState = PhysicsState.None;

        public float SteepSlopeBlockDirection => _steepSlopeBlockDirection;

        #endregion

        //------------------------------- Initialization Methods -------------------------------//

        #region Initialization

        /// <summary>
        /// Binds all serialized physics helpers to the player controller.
        /// </summary>
        public void Initialize(PlayerRigidbodyController controller)
        {
            _controller = controller;
            if (_controller == null)
            {
                _isInitialized = false;
                return;
            }

            if (_surfaceProbe == null)
            {
                _surfaceProbe = new PlayerRigidbodySurfaceProbe();
            }

            if (_externalForces == null)
            {
                _externalForces = new PlayerRigidbodyExternalForceReceiver();
            }

            if (_movingPlatforms == null)
            {
                _movingPlatforms = new PlayerRigidbodyPlatformTracker();
            }

            if (_state == null)
            {
                _state = new LocomotionState();
            }

            Validate();
            if (_groundPoint == null)
            {
                _groundPoint = _surfaceProbe.GroundPoint;
            }
            else
            {
                _surfaceProbe.GroundPoint = _groundPoint;
            }

            _surfaceProbe.Initialize();
            _movingPlatforms.ClearRuntimeState();
            _groundContactObject = null;
            _groundedTime = 0f;
            _wasGroundedLastFrame = false;
            _lastPublishedPhysicsState = PhysicsState.None;
            _isInitialized = true;
        }

        #endregion

        //------------------------------- Core / Routines Methods -------------------------------//

        #region Fixed Step

        /// <summary>
        /// Applies moving-platform translation before the physics probe.
        /// </summary>
        public void ApplyPlatformDelta()
        {
            if (!CanRunPhysicsStep())
            {
                return;
            }

            _fixedStepStartPosition = _controller.Body.position;
            _movingPlatforms.ApplyPlatformMotion(
                _controller.Body,
                _controller.PlayerRigidbodyJump.IsInJumpAscentState);
        }

        /// <summary>
        /// Updates grounding, jump-related ground filtering, slope data, and platform tracking.
        /// </summary>
        public void UpdatePhysics()
        {
            if (!CanRunPhysicsStep())
            {
                return;
            }

            UpdateGrounded();

            _controller.PlayerRigidbodyJump.SetSteepSlopeJumpRestriction(_surfaceProbe.IsSteepSlopeJumpRestricted);
            _controller.PlayerRigidbodyJump.UpdateJumpState(_controller.Body, _state);
            _movingPlatforms.Track(_state, _surfaceProbe.SurfaceCollider, _controller.Body);

            _steepSlopeBlockDirection = _controller.PlayerRigidbodyMovement.ResolveSteepSlopeBlockDirection(
                _controller.MovementInput,
                _controller.FacingDirection,
                _controller.Body,
                _controller.MainCollider,
                _surfaceProbe,
                _steepSlopeJumpCheckDistance);

            IsSteepSlopeAhead = !Mathf.Approximately(_steepSlopeBlockDirection, 0f);
            _state.SetSliding(false);
        }

        /// <summary>
        /// Applies gravity, adhesion, external-force state, platform finalization, and status updates.
        /// </summary>
        public void FinalizePlatformSync()
        {
            if (!CanRunPhysicsStep())
            {
                return;
            }

            ApplyGravity();
            ApplyGroundAdhesion();

            bool hadExternalForce = _externalForces.HasExternalForce;
            _externalForces.UpdateRuntimeState(_controller.Body, _state);
            if (hadExternalForce && !_externalForces.HasExternalForce)
            {
                _controller.PlayerRigidbodyMovement.ResetRuntimeState();
            }

            _movingPlatforms.FinalizePlatformSync(_controller.Body);
            _state.SetVerticalMotion(_controller.Body.linearVelocity.y);
            _state.RefreshPhysicsState(
                GetStateMovementInput(),
                _controller.Body.linearVelocity,
                _controller.PlayerRigidbodyJump.IsInAnticipationFrame || _controller.PlayerRigidbodyJump.IsInJumpAscentState,
                _externalForces.HasExternalForce);

            RaisePhysicsStateChanged();
            UpdateHorizontalDistancePerFixedFrame();
        }

        /// <summary>
        /// Performs the compact circle-cast grounding check used by the reference controller.
        /// This method updates the locomotion surface, last ground contact, and grounded duration.
        /// </summary>
        public void UpdateGrounded()
        {
            if (!CanRunPhysicsStep() || _controller.MainCollider == null)
            {
                return;
            }

            if (PlayerRigidbodyStatus.IsRefracted ||
                PlayerRigidbodyStatus.HasMode(Mode.NoClip) ||
                IsGravityBypassedByMode() ||
                _controller.PlayerRigidbodyJump.ShouldIgnoreGrounding)
            {
                ClearGroundedState();
                return;
            }

            CapsuleCollider2D capsule = _controller.MainCollider;
            Bounds bounds = capsule.bounds;
            float radius = capsule.size.x * 0.5f;
            Vector2 groundReference = _groundPoint != null
                ? (Vector2)_groundPoint.position
                : new Vector2(bounds.center.x, bounds.min.y);
            Vector2 castOrigin = _groundPoint != null
                ? (Vector2)_groundPoint.position
                : groundReference + Vector2.up * radius;

            // Match the reference LayerManager.GroundLayer mask. This repository uses
            // the named Ground layer (layer 7); the serialized fallback may also include Default.
            int groundLayer = LayerMask.NameToLayer("Ground");
            int groundingLayerMask = groundLayer >= 0
                ? 1 << groundLayer
                : _surfaceProbe.GroundLayers.value;

            RaycastHit2D hit = Physics2D.CircleCast(
                castOrigin,
                radius,
                -transform.up,
                _minGroundedDistance,
                groundingLayerMask);

            const float SMALL_THRESHOLD = 0.01f;
            bool grounded = hit.collider != null;
            bool isBottomContact = grounded && hit.point.y <= groundReference.y;

            if (_controller.Body.linearVelocity.y > SMALL_THRESHOLD && !isBottomContact)
            {
                grounded = false;
            }

            bool usedWalkableBodyContact = grounded && hit.collider != null &&
                Vector2.Angle(hit.normal, Vector2.up) > _surfaceProbe.MaximumWalkableSlopeAngle &&
                _surfaceProbe.TryApplyWalkableBodyContact(_controller.Body, capsule, _state);

            Collider2D persistedPlatformCollider = null;
            if (!grounded && TryPersistGroundingFromLastPlatform(groundReference, out persistedPlatformCollider))
            {
                grounded = true;
            }

            if (grounded)
            {
                if (usedWalkableBodyContact)
                {
                    _groundContactObject = _surfaceProbe.SurfaceCollider.gameObject;
                }
                else if (hit.collider != null)
                {
                    _groundContactObject = hit.collider.gameObject;
                    _surfaceProbe.ApplyGroundHit(hit, _state);
                }
                else
                {
                    _groundContactObject = persistedPlatformCollider.gameObject;
                    _surfaceProbe.ApplyPersistentGrounding(persistedPlatformCollider, _state);
                }

                _wasOnSlopeLastFrame = PlayerRigidbodyStatus.IsOnSlope || PlayerRigidbodyStatus.IsOnSteepSlope;
                _groundedTime += Time.fixedDeltaTime;
                OnGroundAction?.Invoke(this);
            }
            else
            {
                _groundContactObject = null;
                _groundedTime = 0f;
                _surfaceProbe.ResetGrounding(_state);
            }

            _wasGroundedLastFrame = grounded;
        }

        private float GetStateMovementInput()
        {
            float movementInput = _controller.MovementInput;
            if (!Mathf.Approximately(_steepSlopeBlockDirection, 0f) &&
                movementInput * _steepSlopeBlockDirection > 0.01f)
            {
                return 0f;
            }

            return movementInput;
        }

        private bool TryPersistGroundingFromLastPlatform(
            Vector2 groundReference,
            out Collider2D platformCollider)
        {
            platformCollider = null;

            if (!_wasGroundedLastFrame || _movingPlatforms.CurrentPlatform == null)
            {
                return false;
            }

            platformCollider = _movingPlatforms.CurrentPlatform.GetComponentInChildren<Collider2D>();
            if (platformCollider == null)
            {
                return false;
            }

            Bounds platformBounds = platformCollider.bounds;
            float verticalGap = Mathf.Abs(groundReference.y - platformBounds.max.y);
            if (verticalGap > _platformPersistenceVerticalThreshold)
            {
                platformCollider = null;
                return false;
            }

            float left = platformBounds.min.x - _platformPersistenceHorizontalPadding;
            float right = platformBounds.max.x + _platformPersistenceHorizontalPadding;
            if (groundReference.x < left || groundReference.x > right)
            {
                platformCollider = null;
                return false;
            }

            return true;
        }

        private void ClearGroundedState()
        {
            _groundContactObject = null;
            _groundedTime = 0f;
            _wasGroundedLastFrame = false;
            _wasOnSlopeLastFrame = false;
            _surfaceProbe.ResetGrounding(_state);
        }

        private static bool IsGravityBypassedByMode()
        {
            return PlayerRigidbodyStatus.HasMode(Mode.NoGravity) ||
                PlayerRigidbodyStatus.HasMode(Mode.FloatingArea);
        }

        private bool CanRunPhysicsStep()
        {
            return _isInitialized && _controller != null && _controller.Body != null &&
                _state != null && _surfaceProbe != null && _externalForces != null &&
                _movingPlatforms != null && _controller.PlayerRigidbodyJump != null &&
                _controller.PlayerRigidbodyMovement != null;
        }

        #endregion

        #region Gravity and Ground Adhesion

        /// <summary>
        /// Applies the ascent/fall gravity multiplier and caps terminal fall speed.
        /// </summary>
        public void ApplyGravity()
        {
            if (_controller == null || _controller.Body == null)
            {
                return;
            }

            float multiplier = _controller.Body.linearVelocity.y > 0f
                ? _jumpAscentGravityMultiplier
                : _fallGravityMultiplier;
            float globalGravityMagnitude = Mathf.Max(Physics2D.gravity.magnitude, 0.0001f);
            _controller.Body.gravityScale = (_baseGravity / globalGravityMagnitude) * _gravityScale * multiplier;

            if (_controller.Body.linearVelocity.y >= -_fallClamp)
            {
                return;
            }

            Vector2 velocity = _controller.Body.linearVelocity;
            velocity.y = -_fallClamp;
            _controller.Body.linearVelocity = velocity;
        }

        private void ApplyGroundAdhesion()
        {
            if (_controller == null || _controller.Body == null || _state == null ||
                _surfaceProbe == null || _externalForces == null || _controller.PlayerRigidbodyJump == null ||
                _groundAdhesionForce <= 0f || !_state.Grounded || !_surfaceProbe.HasDirectSurface ||
                _controller.PlayerRigidbodyJump.IsInJumpAscentState || _externalForces.HasExternalForce)
            {
                return;
            }

            Vector2 surfaceNormal = _surfaceProbe.SurfaceNormal.normalized;
            float normalVelocity = Vector2.Dot(_controller.Body.linearVelocity, surfaceNormal);
            float slopeAngle = Vector2.Angle(surfaceNormal, Vector2.up);

            if (surfaceNormal.y <= 0f ||
                slopeAngle > _surfaceProbe.MaximumWalkableSlopeAngle ||
                normalVelocity > 0.1f)
            {
                return;
            }

            _controller.Body.AddForce(-surfaceNormal * _groundAdhesionForce * _slopeAdherenceFactor, ForceMode2D.Force);
        }

        /// <summary>
        /// Restores the saved gravity scale and clears temporary physics runtime state.
        /// </summary>
        public void ResetRuntimeState()
        {
            if (_controller != null && _controller.Body != null)
            {
                _controller.Body.gravityScale = Default_GravityScale;
            }

            _state?.ResetRuntimeState();
            _externalForces?.ClearRuntimeState();
            _movingPlatforms?.ClearRuntimeState();
            _groundContactObject = null;
            _groundedTime = 0f;
            _wasGroundedLastFrame = false;
            _wasOnSlopeLastFrame = false;
            _steepSlopeBlockDirection = 0f;
            IsSteepSlopeAhead = false;
            _lastPublishedPhysicsState = PhysicsState.None;
            OnPhysicsStateChanged?.Invoke(PhysicsState.Idle);
        }

        public void FreezeMovement()
        {
            if (_controller == null || _controller.Body == null || _controller.PlayerRigidbodyHandler == null)
            {
                return;
            }

            _controller.PlayerRigidbodyHandler.Freeze(_controller.Body);
        }

        public void UnFreezeMovement()
        {
            if (_controller == null || _controller.Body == null || _controller.PlayerRigidbodyHandler == null)
            {
                return;
            }

            _controller.PlayerRigidbodyHandler.Unfreeze(_controller.Body);
        }

        public void DisableCollider(bool disabled)
        {
            if (_controller != null && _controller.MainCollider != null)
            {
                _controller.MainCollider.enabled = !disabled;
            }
        }

        public void ZeroVelocity()
        {
            if (_controller == null || _controller.Body == null)
            {
                return;
            }

            _controller.Body.linearVelocity = Vector2.zero;
            _controller.Body.angularVelocity = 0f;
        }

        public void AddImpulse(Vector2 impulse, bool protectHorizontalMomentum = true, bool clearOnGrounded = false)
        {
            if (_controller == null || _controller.Body == null)
            {
                return;
            }

            float expectedHorizontalVelocity = _controller.Body.linearVelocity.x + impulse.x / Mathf.Max(_controller.Body.mass, 0.0001f);
            _externalForces.AddImpulse(_controller.Body, impulse, protectHorizontalMomentum, clearOnGrounded);
            _controller.PlayerRigidbodyMovement.AddMoveSpeedClamp(expectedHorizontalVelocity);
        }

        public void AddExternalVelocity(Vector2 velocity, bool protectHorizontalMomentum = true, bool clearOnGrounded = false)
        {
            if (_controller == null || _controller.Body == null)
            {
                return;
            }

            _externalForces.AddVelocity(_controller.Body, velocity, protectHorizontalMomentum, clearOnGrounded);
            _controller.PlayerRigidbodyMovement.AddMoveSpeedClamp(_controller.Body.linearVelocity.x);
        }

        public void SetExternalVelocity(Vector2 velocity, bool protectHorizontalMomentum = true, bool clearOnGrounded = false)
        {
            if (_controller == null || _controller.Body == null)
            {
                return;
            }

            _externalForces.SetVelocity(_controller.Body, velocity, protectHorizontalMomentum, clearOnGrounded);
            _controller.PlayerRigidbodyMovement.SetMoveSpeedClamp(Mathf.Abs(velocity.x));
        }

        #endregion

        #region Status Integration

        private void RaisePhysicsStateChanged()
        {
            PhysicsState nextState = PhysicsState.None;
            if (_state.IsIdle) nextState |= PhysicsState.Idle;
            if (_state.IsMoving) nextState |= PhysicsState.Moving;
            if (_state.IsWalking) nextState |= PhysicsState.Walking;
            if (_state.IsJumping) nextState |= PhysicsState.Jumping;
            if (_state.Rising) nextState |= PhysicsState.FloatingUp;
            if (_state.Falling) nextState |= PhysicsState.Falling;
            if (_state.Sliding) nextState |= PhysicsState.Sliding;
            if (_state.IsPushed || _externalForces.HasExternalForce) nextState |= PhysicsState.Pushed;
            if (_state.OnWalkableSlope) nextState |= PhysicsState.OnSlope;
            if (_state.OnSteepSlope) nextState |= PhysicsState.OnSteepSlope;

            nextState = nextState == PhysicsState.None ? PhysicsState.Idle : nextState;
            if (_lastPublishedPhysicsState == nextState)
            {
                return;
            }

            _lastPublishedPhysicsState = nextState;
            OnPhysicsStateChanged?.Invoke(nextState);
        }

        #endregion

        #region Runtime Metrics

        private void UpdateHorizontalDistancePerFixedFrame()
        {
            _horizontalDistancePerFixedFrame = Mathf.Abs(_controller.Body.position.x - _fixedStepStartPosition.x);
        }


        #endregion

        //------------------------------- Editor -------------------------------//

        /// <summary>
        /// Clamps physics tuning and validates nested physics helpers.
        /// </summary>
        public void Validate()
        {
            _gravityScale = Mathf.Max(0f, _gravityScale);
            Default_GravityScale = Mathf.Max(0f, Default_GravityScale);
            _baseGravity = Mathf.Max(0f, _baseGravity);
            _jumpAscentGravityMultiplier = Mathf.Max(0f, _jumpAscentGravityMultiplier);
            _fallGravityMultiplier = Mathf.Max(0f, _fallGravityMultiplier);
            _fallClamp = Mathf.Max(0f, _fallClamp);
            _steepSlopeJumpCheckDistance = Mathf.Max(0.01f, _steepSlopeJumpCheckDistance);
            _groundAdhesionForce = Mathf.Max(0f, _groundAdhesionForce);
            _slopeAdherenceFactor = Mathf.Max(0f, _slopeAdherenceFactor);
            _minGroundedDistance = Mathf.Max(0f, _minGroundedDistance);
            _platformPersistenceVerticalThreshold = Mathf.Max(0f, _platformPersistenceVerticalThreshold);
            _platformPersistenceHorizontalPadding = Mathf.Max(0f, _platformPersistenceHorizontalPadding);
            _surfaceProbe?.Validate();
            _externalForces?.Validate();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            Validate();
        }
#endif
    }
}
