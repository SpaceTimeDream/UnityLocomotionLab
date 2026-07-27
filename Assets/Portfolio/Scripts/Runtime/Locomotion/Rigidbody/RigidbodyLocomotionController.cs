using Portfolio.Locomotion.Core;
using UnityEngine;

namespace Portfolio.Locomotion.Rigidbody
{
    [RequireComponent(typeof(UnityEngine.Rigidbody2D))]
    [RequireComponent(typeof(CapsuleCollider2D))]
    public class RigidbodyLocomotionController : MonoBehaviour
    {
        // Fields
        [Header("Modules")]
        [SerializeField] private LocomotionState _state = new LocomotionState();
        [SerializeField] private RigidbodyMovement _movement = new RigidbodyMovement();
        [SerializeField] private RigidbodyJump _jump = new RigidbodyJump();
        [SerializeField] private SurfaceProbe2D _surfaceProbe = new SurfaceProbe2D();
        [SerializeField] private ExternalForceReceiver2D _externalForces = new ExternalForceReceiver2D();
        [SerializeField] private MovingPlatformTracker2D _movingPlatforms = new MovingPlatformTracker2D();

        [Header("Gravity")]
        [SerializeField] private float _gravityScale = 3f;
        [SerializeField] private float _jumpAscentGravityMultiplier = 1f;
        [SerializeField] private float _fallGravityMultiplier = 1.6f;
        [SerializeField] private float _maximumFallSpeed = 18f;

        [Header("Steep Slope Jump Constraint")]
        [SerializeField] private float _steepSlopeJumpCheckDistance = 0.35f;

        private UnityEngine.Rigidbody2D _body;
        private CapsuleCollider2D _capsuleCollider;
        private float _movementInput;
        private float _facingDirection = 1f;

        // Properties
        public LocomotionState State => _state;

        // Unity Methods
        private void Start()
        {
            Initialize();
        }

        private void FixedUpdate()
        {
            UpdatePhysics();
        }

        private void OnDestroy()
        {
            CleanupEvents();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _gravityScale = Mathf.Max(0f, _gravityScale);
            _jumpAscentGravityMultiplier = Mathf.Max(0f, _jumpAscentGravityMultiplier);
            _fallGravityMultiplier = Mathf.Max(0f, _fallGravityMultiplier);
            _maximumFallSpeed = Mathf.Max(0f, _maximumFallSpeed);
            _steepSlopeJumpCheckDistance = Mathf.Max(0.01f, _steepSlopeJumpCheckDistance);
            _movement?.Validate();
            _jump?.Validate();
            _surfaceProbe?.Validate();
            _externalForces?.Validate();
        }
#endif

        // Public Methods
        public void SetMovementInput(float movementInput)
        {
            _movementInput = Mathf.Clamp(movementInput, -1f, 1f);

            if (Mathf.Abs(_movementInput) > 0.01f)
            {
                _facingDirection = Mathf.Sign(_movementInput);
            }
        }

        public void RequestJump()
        {
            _jump.RequestJump();
        }

        public void ReleaseJump()
        {
            _jump.ReleaseJump(_body);
        }

        public void SetControlEnabled(bool controlEnabled)
        {
            _state.SetControlEnabled(controlEnabled);

            if (!controlEnabled)
            {
                _movementInput = 0f;
                _jump.ResetRuntimeState();
            }
        }

        public void ResetLocomotionRuntimeState()
        {
            _movementInput = 0f;
            _facingDirection = 1f;
            _state.ResetRuntimeState();
            _jump.ResetRuntimeState();
            _externalForces.ClearRuntimeState();
            _movingPlatforms.ClearRuntimeState();
        }

        public void AddExternalImpulse(Vector2 impulse, bool protectHorizontalMomentum = true)
        {
            _externalForces.AddImpulse(_body, impulse, protectHorizontalMomentum);
        }

        public void AddExternalVelocity(Vector2 velocity, bool protectHorizontalMomentum = true)
        {
            _externalForces.AddVelocity(_body, velocity, protectHorizontalMomentum);
        }

        public void SetExternalVelocity(Vector2 velocity, bool protectHorizontalMomentum = true)
        {
            _externalForces.SetVelocity(_body, velocity, protectHorizontalMomentum);
        }

        // Private Methods
        private void Initialize()
        {
            InitializeComponents();
            InitializeModules();
            InitializeEvents();
        }

        private void InitializeComponents()
        {
            _body = GetComponent<UnityEngine.Rigidbody2D>();
            _capsuleCollider = GetComponent<CapsuleCollider2D>();
        }

        private void InitializeModules()
        {
            _surfaceProbe.Initialize();
        }

        private void InitializeEvents()
        {
        }

        private void UpdatePhysics()
        {
            _surfaceProbe.Probe(_capsuleCollider, _state);
            _jump.TickGrounded(_state);
            _movingPlatforms.Track(_state, _surfaceProbe.SurfaceCollider);
            _movingPlatforms.ApplyPlatformMotion(_body);
            bool blockMovementIntoSteepSlope = ShouldBlockMovementIntoSteepSlope();
            _movement.ApplyMovement(_body, _state, _surfaceProbe, _movementInput, _externalForces.IsHorizontalProtected, blockMovementIntoSteepSlope, _facingDirection);
            _jump.ApplyJump(_body, _state, blockMovementIntoSteepSlope);
            ApplyGravity();
            _state.SetVerticalMotion(_body.linearVelocity.y);
        }

        private bool ShouldBlockMovementIntoSteepSlope()
        {
            float movementDirection = Mathf.Abs(_movementInput) > 0.01f ? _movementInput : _body.linearVelocity.x;

            if (movementDirection * _facingDirection <= 0.01f)
            {
                return false;
            }

            return _surfaceProbe.HasNearbySteepSlope(_capsuleCollider, _facingDirection, _steepSlopeJumpCheckDistance);
        }

        private void ApplyGravity()
        {
            float multiplier = _body.linearVelocity.y > 0f ? _jumpAscentGravityMultiplier : _fallGravityMultiplier;
            _body.gravityScale = _gravityScale * multiplier;

            if (_body.linearVelocity.y >= -_maximumFallSpeed)
            {
                return;
            }

            Vector2 velocity = _body.linearVelocity;
            velocity.y = -_maximumFallSpeed;
            _body.linearVelocity = velocity;
        }

        private void CleanupEvents()
        {
        }
    }
}
