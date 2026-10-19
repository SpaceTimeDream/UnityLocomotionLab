using Portfolio.Game;
using Portfolio.Input;
using Portfolio.Locomotion.Core;
using System;
using UnityEngine;

namespace Portfolio.Locomotion.Rigidbody
{
    [RequireComponent(typeof(UnityEngine.Rigidbody2D))]
    [RequireComponent(typeof(CapsuleCollider2D))]
    public class RigidbodyLocomotionController : MonoBehaviour
    {
        //------------------------------- Game Pause -------------------------------//

        public virtual void OnGameStateChanged(GameState newGameState)
        {
            if (newGameState == GameState.Paused)
            {
                FreezeMovement();
                return;
            }

            UnFreezeMovement();
        }

        //------------------------------- Debugging Methods -------------------------------//

        //------------------------------- Parameters -------------------------------//

        #region Components

        [Header("COMPONENTS")]
        [SerializeField] private UnityEngine.Rigidbody2D _body;
        [SerializeField] private CapsuleCollider2D _capsuleCollider;
        [SerializeField] private Transform _visualRoot;
        [SerializeField] private GameInput _gameInput;

        [Header("MODULES")]
        [SerializeField] private LocomotionState _state = new LocomotionState();
        [SerializeField] private RigidbodyMovement _movement = new RigidbodyMovement();
        [SerializeField] private RigidbodyJump _jump = new RigidbodyJump();
        [SerializeField] private SurfaceProbe2D _surfaceProbe = new SurfaceProbe2D();
        [SerializeField] private ExternalForceReceiver2D _externalForces = new ExternalForceReceiver2D();
        [SerializeField] private MovingPlatformTracker2D _movingPlatforms = new MovingPlatformTracker2D();

        [Header("GRAVITY PARAMETERS")]
        [SerializeField] private float _gravityScale = 3.4f;
        [SerializeField] private float _jumpAscentGravityMultiplier = 1.1f;
        [SerializeField] private float _fallGravityMultiplier = 2.1f;
        [SerializeField] private float _maximumFallSpeed = 22f;

        [Header("SLOPE PARAMETERS")]
        [SerializeField] private float _steepSlopeJumpCheckDistance = 0.35f;

        [Header("FACING PARAMETERS")]
        [SerializeField] private bool _flipVisualWithFacing = true;

        private float _movementInput;
        private float _facingDirection = 1f;
        private Vector2 _cachedPausedVelocity;
        private float _cachedPausedGravityScale;
        private float _horizontalDistancePerFixedFrame;
        private float _steepSlopeBlockDirection;

        public UnityEngine.Rigidbody2D Body => _body;
        public CapsuleCollider2D MainCollider => _capsuleCollider;
        public GameInput Input => _gameInput;
        public LocomotionState State => _state;
        public RigidbodyMovement Movement => _movement;
        public RigidbodyJump Jump => _jump;
        public SurfaceProbe2D SurfaceProbe => _surfaceProbe;
        public ExternalForceReceiver2D ExternalForces => _externalForces;
        public MovingPlatformTracker2D MovingPlatforms => _movingPlatforms;
        public float MovementInput => _movementInput;
        public float FacingDirection => _facingDirection;
        public Vector2 Velocity => _body != null ? _body.linearVelocity : Vector2.zero;
        public float Speed => Velocity.magnitude;
        public float VerticalVelocity => Velocity.y;
        public float HorizontalDistancePerFixedFrame => _horizontalDistancePerFixedFrame;
        public float SteepSlopeBlockDirection => _steepSlopeBlockDirection;
        public bool IsMovementFrozen => _movementFrozen;

        public event Action<float> DirectionChanged;
        public event Action JumpTriggered;

        #endregion

        #region Flags

        private bool _initialized;
        private bool _movementFrozen;

        #endregion

        //------------------------------- Unity Methods -------------------------------//

        #region Unity

        private void Start()
        {
            Initialize();
        }

        private void OnEnable()
        {
            if (!_initialized)
            {
                return;
            }

            InitializeEvents();
        }

        private void Update()
        {
            UpdateLoop();
        }

        private void FixedUpdate()
        {
            FixedUpdateLoop();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
            SetMovementInput(0f);
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        #endregion

        //------------------------------- Initialization Methods -------------------------------//

        #region Initialization

        private void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            InitializeModules();
            InitializeComponents();
            InitializeEvents();
            _cachedPausedGravityScale = _body.gravityScale;
            _initialized = true;
        }

        private void InitializeModules()
        {
            if (_gameInput == null)
            {
                _gameInput = GameInput.Instance;
            }

            _surfaceProbe.Initialize();
        }

        private void InitializeComponents()
        {
            if (_body == null)
            {
                _body = GetComponent<UnityEngine.Rigidbody2D>();
            }

            if (_capsuleCollider == null)
            {
                _capsuleCollider = GetComponent<CapsuleCollider2D>();
            }

            if (_visualRoot == null)
            {
                _visualRoot = transform;
            }
        }

        private void InitializeEvents()
        {
            UnsubscribeEvents();

            if (_gameInput != null)
            {
                _gameInput.JumpPressed += HandleJumpPressed;
                _gameInput.JumpReleased += HandleJumpReleased;
            }

            GameManager.GameStateChanged += OnGameStateChanged;
        }

        private void UnsubscribeEvents()
        {
            if (_gameInput != null)
            {
                _gameInput.JumpPressed -= HandleJumpPressed;
                _gameInput.JumpReleased -= HandleJumpReleased;
            }

            GameManager.GameStateChanged -= OnGameStateChanged;
        }

        #endregion

        //------------------------------- Core / Routines Methods -------------------------------//

        public void SetMovementInput(float movementInput)
        {
            _movementInput = Mathf.Clamp(movementInput, -1f, 1f);
        }

        public void RequestJump()
        {
            _jump.RequestJump();
        }

        public void ReleaseJump()
        {
            if (_body != null)
            {
                _jump.ReleaseJump(_body);
            }
        }

        public void SetControlEnabled(bool controlEnabled)
        {
            _state.SetControlEnabled(controlEnabled);

            if (!controlEnabled)
            {
                SetMovementInput(0f);
                _jump.ResetRuntimeState();
            }
        }

        public void FreezeMovement()
        {
            if (_movementFrozen || _body == null)
            {
                return;
            }

            _movementFrozen = true;
            _cachedPausedVelocity = _body.linearVelocity;
            _cachedPausedGravityScale = _body.gravityScale;
            _body.linearVelocity = Vector2.zero;
            _body.gravityScale = 0f;
            SetMovementInput(0f);
        }

        public void UnFreezeMovement()
        {
            if (!_movementFrozen || _body == null)
            {
                return;
            }

            _movementFrozen = false;
            _body.linearVelocity = _cachedPausedVelocity;
            _body.gravityScale = _cachedPausedGravityScale;
        }

        public void ZeroVelocity()
        {
            if (_body == null)
            {
                return;
            }

            _body.linearVelocity = Vector2.zero;
            _body.angularVelocity = 0f;
        }

        public void ResetLocomotionRuntimeState()
        {
            SetMovementInput(0f);
            SetFacingDirection(1f);
            _state.ResetRuntimeState();
            _movement.ResetRuntimeState();
            _jump.ResetRuntimeState();
            _externalForces.ClearRuntimeState();
            _movingPlatforms.ClearRuntimeState();
            _steepSlopeBlockDirection = 0f;
            ZeroVelocity();
        }

        public void AddExternalImpulse(
            Vector2 impulse,
            bool protectHorizontalMomentum = true,
            bool clearOnGrounded = false)
        {
            float previousHorizontalVelocity = _body.linearVelocity.x;
            _externalForces.AddImpulse(_body, impulse, protectHorizontalMomentum, clearOnGrounded);
            float expectedHorizontalVelocity =
                previousHorizontalVelocity + impulse.x / Mathf.Max(_body.mass, 0.0001f);
            _movement.AddMoveSpeedClamp(expectedHorizontalVelocity);
        }

        public void AddExternalVelocity(
            Vector2 velocity,
            bool protectHorizontalMomentum = true,
            bool clearOnGrounded = false)
        {
            _externalForces.AddVelocity(_body, velocity, protectHorizontalMomentum, clearOnGrounded);
            _movement.AddMoveSpeedClamp(_body.linearVelocity.x);
        }

        public void SetExternalVelocity(
            Vector2 velocity,
            bool protectHorizontalMomentum = true,
            bool clearOnGrounded = false)
        {
            _externalForces.SetVelocity(_body, velocity, protectHorizontalMomentum, clearOnGrounded);
            _movement.SetMoveSpeedClamp(Mathf.Abs(velocity.x));
        }

        private void UpdateLoop()
        {
            if (!_initialized)
            {
                return;
            }

            RefreshGameInput();
            UpdateMoveInput();
            UpdateFacingDirection();
        }

        private void FixedUpdateLoop()
        {
            if (!_initialized || _movementFrozen)
            {
                return;
            }

            Vector2 previousPosition = _body.position;
            _movingPlatforms.ApplyPlatformMotion(_body, _jump.IsInJumpAscentState);
            _surfaceProbe.Probe(_capsuleCollider, _body, _state, _jump.ShouldIgnoreGrounding);
            _jump.SetSteepSlopeJumpRestriction(_surfaceProbe.IsSteepSlopeJumpRestricted);
            _jump.UpdateRuntimeState(_body, _state);
            _movingPlatforms.Track(_state, _surfaceProbe.SurfaceCollider, _body);

            _steepSlopeBlockDirection = GetSteepSlopeBlockDirection();
            _state.SetSliding(false);
            _movement.ApplyMovement(
                _body,
                _state,
                _surfaceProbe,
                _externalForces,
                _movementInput,
                _steepSlopeBlockDirection,
                _jump.IsInJumpAscentState);

            if (_jump.ApplyJump(_body, _state, _steepSlopeBlockDirection))
            {
                JumpTriggered?.Invoke();
            }

            ApplyGravity();

            bool hadExternalForce = _externalForces.HasExternalForce;
            _externalForces.UpdateRuntimeState(_body, _state);

            if (hadExternalForce && !_externalForces.HasExternalForce)
            {
                _movement.ResetRuntimeState();
            }

            _movingPlatforms.FinalizePlatformSync(_body);
            _state.SetVerticalMotion(_body.linearVelocity.y);
            _state.RefreshPhysicsState(
                _movementInput,
                _body.linearVelocity,
                _jump.IsInAnticipationFrame || _jump.IsInJumpAscentState,
                _externalForces.HasExternalForce);
            _horizontalDistancePerFixedFrame = Mathf.Abs(_body.position.x - previousPosition.x);
        }

        private void RefreshGameInput()
        {
            if (_gameInput != null)
            {
                return;
            }

            _gameInput = GameInput.Instance;

            if (_gameInput != null)
            {
                InitializeEvents();
            }
        }

        private void UpdateMoveInput()
        {
            if (!_state.ControlEnabled || _movementFrozen || _gameInput == null)
            {
                SetMovementInput(0f);
                return;
            }

            SetMovementInput(_gameInput.MovementValue.x);
        }

        private void UpdateFacingDirection()
        {
            float direction = _movementInput;

            if (_externalForces.IsControlProtected && Mathf.Abs(_body.linearVelocity.x) > 0.01f)
            {
                direction = _body.linearVelocity.x;
            }

            if (Mathf.Abs(direction) <= 0.01f)
            {
                return;
            }

            SetFacingDirection(Mathf.Sign(direction));
        }

        private void SetFacingDirection(float direction)
        {
            float nextDirection = direction >= 0f ? 1f : -1f;

            if (Mathf.Approximately(_facingDirection, nextDirection))
            {
                return;
            }

            _facingDirection = nextDirection;
            ApplyVisualFacing();
            DirectionChanged?.Invoke(_facingDirection);
        }

        private void ApplyVisualFacing()
        {
            if (!_flipVisualWithFacing || _visualRoot == null)
            {
                return;
            }

            Vector3 eulerAngles = _visualRoot.localEulerAngles;
            eulerAngles.y = _facingDirection > 0f ? 0f : 180f;
            _visualRoot.localEulerAngles = eulerAngles;
        }

        private float GetSteepSlopeBlockDirection()
        {
            float inputDirection = GetDirection(_movementInput);

            if (HasNearbySteepSlope(inputDirection))
            {
                return inputDirection;
            }

            float velocityDirection = GetDirection(_body.linearVelocity.x);

            if (!Mathf.Approximately(velocityDirection, inputDirection) &&
                HasNearbySteepSlope(velocityDirection))
            {
                return velocityDirection;
            }

            if (Mathf.Approximately(inputDirection, 0f) &&
                Mathf.Approximately(velocityDirection, 0f) &&
                HasNearbySteepSlope(_facingDirection))
            {
                return _facingDirection;
            }

            return 0f;
        }

        private bool HasNearbySteepSlope(float direction)
        {
            if (Mathf.Approximately(direction, 0f))
            {
                return false;
            }

            return _surfaceProbe.HasNearbySteepSlope(
                _capsuleCollider,
                direction,
                _steepSlopeJumpCheckDistance);
        }

        private float GetDirection(float value)
        {
            return Mathf.Abs(value) > 0.01f ? Mathf.Sign(value) : 0f;
        }

        private void ApplyGravity()
        {
            float multiplier = _body.linearVelocity.y > 0f
                ? _jumpAscentGravityMultiplier
                : _fallGravityMultiplier;
            _body.gravityScale = _gravityScale * multiplier;

            if (_body.linearVelocity.y >= -_maximumFallSpeed)
            {
                return;
            }

            Vector2 velocity = _body.linearVelocity;
            velocity.y = -_maximumFallSpeed;
            _body.linearVelocity = velocity;
        }

        private void HandleJumpPressed()
        {
            if (_state.ControlEnabled && !_movementFrozen)
            {
                RequestJump();
            }
        }

        private void HandleJumpReleased()
        {
            ReleaseJump();
        }

#if UNITY_EDITOR
        //------------------------------- Editor -------------------------------//

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

            if (!Application.isPlaying)
            {
                InitializeComponents();
            }
        }
#endif
    }
}
