using System;
using Portfolio.Game;
using Portfolio.Input;
using Portfolio.Locomotion.Core;
using UnityEngine;

namespace Portfolio.Locomotion.Spline
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CapsuleCollider2D))]
    public class SimulatedSplineLocomotionController : MonoBehaviour
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
        [SerializeField] private Rigidbody2D _body;
        [SerializeField] private CapsuleCollider2D _capsuleCollider;
        [SerializeField] private Transform _visualRoot;
        [SerializeField] private GameInput _gameInput;

        [Header("MODULES")]
        [SerializeField] private LocomotionState _state = new LocomotionState();
        [SerializeField] private SimulatedSplineMovement _movement = new SimulatedSplineMovement();
        [SerializeField] private SimulatedSplineJump _jump = new SimulatedSplineJump();
        [SerializeField] private SplineGroundProbe2D _surfaceProbe = new SplineGroundProbe2D();
        [SerializeField] private SimulatedCollisionResolver2D _collisionResolver =
            new SimulatedCollisionResolver2D();

        [Header("GRAVITY PARAMETERS")]
        [SerializeField] private float _gravity = 28f;
        [SerializeField] private float _jumpAscentGravityMultiplier = 1f;
        [SerializeField] private float _fallGravityMultiplier = 1.6f;
        [SerializeField] private float _maximumFallSpeed = 22f;

        [Header("SLOPE PARAMETERS")]
        [SerializeField] private float _steepSlopeCheckDistance = 0.35f;

        [Header("VISUAL PARAMETERS")]
        [SerializeField] private float _groundedAlignmentSpeed = 18f;
        [SerializeField] private bool _constrainZRotation = true;
        [SerializeField] private bool _flipVisualWithFacing = true;

        [Header("DEMO PARAMETERS")]
        [SerializeField] private float _demoResetY = -8f;

        private Transform _cachedTransform;
        private Vector2 _velocity;
        private Vector2 _cachedPausedVelocity;
        private Vector2 _spawnPosition;
        private Vector3 _startingVisualScale;
        private float _movementInput;
        private float _facingDirection = 1f;
        private float _startingVisualZRotation;
        private float _horizontalDistancePerFixedFrame;

        public Rigidbody2D Body => _body;
        public CapsuleCollider2D MainCollider => _capsuleCollider;
        public GameInput Input => _gameInput;
        public LocomotionState State => _state;
        public SimulatedSplineMovement Movement => _movement;
        public SimulatedSplineJump Jump => _jump;
        public SplineGroundProbe2D SurfaceProbe => _surfaceProbe;
        public SimulatedCollisionResolver2D CollisionResolver => _collisionResolver;
        public Vector2 Velocity => _velocity;
        public float MovementInput => _movementInput;
        public float FacingDirection => _facingDirection;
        public float HorizontalDistancePerFixedFrame => _horizontalDistancePerFixedFrame;
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
            _spawnPosition = _cachedTransform.position;
            _state.ResetRuntimeState();
            _initialized = true;
        }

        private void InitializeModules()
        {
            if (_gameInput == null)
            {
                _gameInput = GameInput.Instance;
            }

            _surfaceProbe.Initialize();
            _collisionResolver.Initialize();
        }

        private void InitializeComponents()
        {
            _cachedTransform = transform;

            if (_body == null)
            {
                _body = GetComponent<Rigidbody2D>();
            }

            if (_capsuleCollider == null)
            {
                _capsuleCollider = GetComponent<CapsuleCollider2D>();
            }

            if (_visualRoot == null && transform.childCount > 0)
            {
                _visualRoot = transform.GetChild(0);
            }

            ConfigureCollisionBody();

            if (_visualRoot != null)
            {
                _startingVisualZRotation = _visualRoot.eulerAngles.z;
                _startingVisualScale = _visualRoot.localScale;
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

        public void SetMovementInput(Vector2 movementInput)
        {
            SetMovementInput(movementInput.x);
        }

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
            _jump.ReleaseJump(ref _velocity);
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
            if (_movementFrozen)
            {
                return;
            }

            _movementFrozen = true;
            _cachedPausedVelocity = _velocity;
            _velocity = Vector2.zero;
            SetMovementInput(0f);
        }

        public void UnFreezeMovement()
        {
            if (!_movementFrozen)
            {
                return;
            }

            _movementFrozen = false;
            _velocity = _cachedPausedVelocity;
        }

        public void ZeroVelocity()
        {
            _velocity = Vector2.zero;

            if (_body != null)
            {
                _body.linearVelocity = Vector2.zero;
                _body.angularVelocity = 0f;
            }
        }

        public void ResetLocomotionRuntimeState()
        {
            SetMovementInput(0f);
            SetFacingDirection(1f);
            _state.ResetRuntimeState();
            _movement.ResetRuntimeState();
            _jump.ResetRuntimeState();
            ZeroVelocity();
        }

        public void ResetToSpawn()
        {
            TranslateTo(_spawnPosition);
            ResetLocomotionRuntimeState();
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

            if (_cachedTransform.position.y < _demoResetY)
            {
                ResetToSpawn();
                return;
            }

            float deltaTime = Time.fixedDeltaTime;
            Vector2 previousPosition = _cachedTransform.position;
            _body.linearVelocity = Vector2.zero;
            _body.angularVelocity = 0f;
            _surfaceProbe.Probe(
                _capsuleCollider,
                _body,
                _velocity,
                _state,
                _jump.ShouldIgnoreGrounding);
            _jump.SetSteepSlopeJumpRestriction(
                _surfaceProbe.IsSteepSlopeJumpRestricted);
            _jump.UpdateRuntimeState(_velocity, _state, deltaTime);

            bool blockMovementIntoSteepSlope =
                ShouldBlockMovementIntoSteepSlope();
            _state.SetSliding(false);
            _movement.ApplyMovement(
                ref _velocity,
                _state,
                _surfaceProbe,
                _movementInput,
                blockMovementIntoSteepSlope,
                _facingDirection,
                deltaTime);

            if (_jump.ApplyJump(
                ref _velocity,
                _state,
                blockMovementIntoSteepSlope,
                deltaTime))
            {
                JumpTriggered?.Invoke();
            }

            ApplyGravity(deltaTime);
            Vector2 intendedDisplacement = _velocity * deltaTime;

            if (_state.Grounded && !_jump.IsInJumpAscentState)
            {
                intendedDisplacement +=
                    Vector2.down * _surfaceProbe.GroundSnapDistance;
            }

            Vector2 resolvedDisplacement = _collisionResolver.Resolve(
                previousPosition,
                _capsuleCollider,
                intendedDisplacement,
                ref _velocity,
                _state.Grounded,
                _surfaceProbe.MaximumWalkableSlopeAngle,
                out RaycastHit2D blockingHit);
            TranslateBy(resolvedDisplacement);
            ApplyResolvedSurface(blockingHit, intendedDisplacement);
            AlignVisual();
            _state.SetVerticalMotion(_velocity.y);
            _state.RefreshPhysicsState(
                _movementInput,
                _velocity,
                _jump.IsInAnticipationFrame || _jump.IsInJumpAscentState,
                false);
            _horizontalDistancePerFixedFrame =
                Mathf.Abs(_cachedTransform.position.x - previousPosition.x);
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
            float direction = Mathf.Abs(_movementInput) > 0.01f
                ? _movementInput
                : _velocity.x;

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

            Vector3 scale = _startingVisualScale;
            scale.x = Mathf.Abs(scale.x) * _facingDirection;
            _visualRoot.localScale = scale;
        }

        private bool ShouldBlockMovementIntoSteepSlope()
        {
            float movementDirection = Mathf.Abs(_movementInput) > 0.01f
                ? _movementInput
                : _velocity.x;

            if (movementDirection * _facingDirection <= 0.01f)
            {
                return false;
            }

            return _surfaceProbe.HasNearbySteepSlope(
                _capsuleCollider,
                _facingDirection,
                _steepSlopeCheckDistance);
        }

        private void ApplyGravity(float deltaTime)
        {
            if (_state.Grounded)
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

        private void ApplyResolvedSurface(
            RaycastHit2D blockingHit,
            Vector2 intendedDisplacement)
        {
            if (blockingHit.collider == null ||
                intendedDisplacement.y > 0f ||
                !_surfaceProbe.IsWalkableSurface(blockingHit.normal))
            {
                return;
            }

            _surfaceProbe.ApplySurfaceHit(blockingHit, _state);
        }

        private void AlignVisual()
        {
            if (_visualRoot == null)
            {
                return;
            }

            Quaternion targetRotation = _constrainZRotation
                ? Quaternion.Euler(0f, 0f, _startingVisualZRotation)
                : GetTargetVisualRotation();
            _visualRoot.rotation = Quaternion.Lerp(
                _visualRoot.rotation,
                targetRotation,
                _groundedAlignmentSpeed * Time.fixedDeltaTime);
        }

        private Quaternion GetTargetVisualRotation()
        {
            return _state.Grounded
                ? Quaternion.FromToRotation(Vector3.up, _state.SurfaceNormal)
                : Quaternion.identity;
        }

        private void TranslateBy(Vector2 displacement)
        {
            _cachedTransform.Translate(displacement, Space.World);
        }

        private void TranslateTo(Vector2 position)
        {
            TranslateBy(position - (Vector2)_cachedTransform.position);
        }

        private void ConfigureCollisionBody()
        {
            if (_body == null)
            {
                return;
            }

            _body.bodyType = RigidbodyType2D.Kinematic;
            _body.simulated = true;
            _body.useFullKinematicContacts = true;
            _body.gravityScale = 0f;
            _body.constraints |= RigidbodyConstraints2D.FreezeRotation;
            _body.linearVelocity = Vector2.zero;
            _body.angularVelocity = 0f;
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
            _gravity = Mathf.Max(0f, _gravity);
            _jumpAscentGravityMultiplier = Mathf.Max(
                0f,
                _jumpAscentGravityMultiplier);
            _fallGravityMultiplier = Mathf.Max(0f, _fallGravityMultiplier);
            _maximumFallSpeed = Mathf.Max(0f, _maximumFallSpeed);
            _steepSlopeCheckDistance = Mathf.Max(0.01f, _steepSlopeCheckDistance);
            _groundedAlignmentSpeed = Mathf.Max(0f, _groundedAlignmentSpeed);
            _movement?.Validate();
            _jump?.Validate();
            _surfaceProbe?.Validate();
            _collisionResolver?.Validate();

            if (!Application.isPlaying)
            {
                InitializeComponents();
            }
        }
#endif
    }
}
