using System;
using Portfolio.Game;
using Portfolio.Input;
using Portfolio.Locomotion.Core;
using Portfolio.Locomotion.Rigidbody;
using UnityEngine;

namespace Portfolio.Locomotion.Spline
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CapsuleCollider2D))]
    [RequireComponent(typeof(PlayerSimulatedMovement))]
    [RequireComponent(typeof(PlayerSimulatedJump))]
    [RequireComponent(typeof(PlayerSimulatedPhysics))]
    [RequireComponent(typeof(PlayerSimulatedHandler))]
    public sealed class PlayerSimulatedController : MonoBehaviour
    {
        //------------------------------- Game Pause -------------------------------//

        public void OnGameStateChanged(GameState newGameState)
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
        [SerializeField] private PlayerSimulatedMovement _movement;
        [SerializeField] private PlayerSimulatedJump _jump;
        [SerializeField] private PlayerSimulatedGroundProbe _surfaceProbe = new PlayerSimulatedGroundProbe();
        [SerializeField] private PlayerSimulatedCollisionResolver _collisionResolver =
            new PlayerSimulatedCollisionResolver();
        [SerializeField] private PlayerSimulatedPlatformTracker _movingPlatforms =
            new PlayerSimulatedPlatformTracker();

        [Header("PHYSICS")]
        [SerializeField] private PlayerSimulatedPhysics _physics;
        [SerializeField] private PlayerSimulatedHandler _playerHandler;

        [Header("VISUALS")]
        [SerializeField] private PlayerSimulatedVisuals _visuals = new PlayerSimulatedVisuals();

        private Transform _cachedTransform;
        private Vector2 _spawnPosition;
        private float _movementInput;
        private float _facingDirection = 1f;
        private float _horizontalDistancePerFixedFrame;

        public Rigidbody2D Body => _body;
        public CapsuleCollider2D MainCollider => _capsuleCollider;
        public Transform VisualRoot => _visualRoot;
        public GameInput Input => _gameInput;
        public LocomotionState State => _physics != null ? _physics.State : null;
        public PlayerSimulatedMovement Movement => _movement;
        public PlayerSimulatedJump Jump => _jump;
        public PlayerSimulatedGroundProbe SurfaceProbe => _surfaceProbe;
        public PlayerSimulatedCollisionResolver CollisionResolver => _collisionResolver;
        public PlayerSimulatedPlatformTracker MovingPlatforms => _movingPlatforms;
        public PlayerSimulatedHandler PlayerHandler => _playerHandler;
        public Vector2 Velocity => _physics != null ? _physics.Velocity : Vector2.zero;
        public float MovementInput => _movementInput;
        public float FacingDirection => _facingDirection;
        public float HorizontalDistancePerFixedFrame => _horizontalDistancePerFixedFrame;
        public bool IsMovementFrozen => _playerHandler != null && _playerHandler.IsMovementFrozen;
        public bool IsScriptedControlActive => _playerHandler != null && _playerHandler.IsScriptedControlActive;
        public bool MovementBlockedBySteepSlope => _physics != null && _physics.MovementBlockedBySteepSlope;
        public Vector2 LastIntendedDisplacement => _physics != null ? _physics.LastIntendedDisplacement : Vector2.zero;
        public Vector2 LastResolvedDisplacement => _physics != null ? _physics.LastResolvedDisplacement : Vector2.zero;

        public event Action<float> DirectionChanged;
        public event Action JumpTriggered;

        #endregion

        #region Flags

        private bool _initialized;

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
            State.ResetRuntimeState();
            _initialized = true;
            _playerHandler.Initialize(this, State);
        }

        private void InitializeModules()
        {
            if (_gameInput == null)
            {
                _gameInput = GameInput.Instance;
            }

            if (_movement == null)
            {
                _movement = GetComponent<PlayerSimulatedMovement>();
            }

            if (_movement == null)
            {
                _movement = gameObject.AddComponent<PlayerSimulatedMovement>();
            }

            if (_jump == null)
            {
                _jump = GetComponent<PlayerSimulatedJump>();
            }

            if (_jump == null)
            {
                _jump = gameObject.AddComponent<PlayerSimulatedJump>();
            }

            if (_physics == null)
            {
                _physics = GetComponent<PlayerSimulatedPhysics>();
            }

            if (_physics == null)
            {
                _physics = gameObject.AddComponent<PlayerSimulatedPhysics>();
            }

            if (_playerHandler == null)
            {
                _playerHandler = GetComponent<PlayerSimulatedHandler>();
            }

            if (_playerHandler == null)
            {
                _playerHandler = gameObject.AddComponent<PlayerSimulatedHandler>();
            }

            if (_visuals == null)
            {
                _visuals = new PlayerSimulatedVisuals();
            }

            _movement.Validate();
            _jump.Validate();
            _physics.Validate();
            _visuals.Validate();
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

            if (_movement == null)
            {
                _movement = GetComponent<PlayerSimulatedMovement>();
            }

            if (_jump == null)
            {
                _jump = GetComponent<PlayerSimulatedJump>();
            }

            if (_physics == null)
            {
                _physics = GetComponent<PlayerSimulatedPhysics>();
            }

            if (_playerHandler == null)
            {
                _playerHandler = GetComponent<PlayerSimulatedHandler>();
            }

            if (_visualRoot == null && transform.childCount > 0)
            {
                _visualRoot = transform.GetChild(0);
            }

            ConfigureCollisionBody();

            if (_visuals == null)
            {
                _visuals = new PlayerSimulatedVisuals();
            }

            _visuals.Initialize(_visualRoot);
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
        public void BeginScriptedControl()
        {
            _playerHandler?.BeginScriptedControl();
        }

        public void SetScriptedMoveInput(Vector2 input)
        {
            _playerHandler?.SetScriptedMoveInput(input);
        }

        public void StopScriptedHorizontalMotion()
        {
            Vector2 velocity = Velocity;
            velocity.x = 0f;
            _physics.SetVelocity(velocity);

            if (_body != null)
            {
                Vector2 bodyVelocity = _body.linearVelocity;
                bodyVelocity.x = 0f;
                _body.linearVelocity = bodyVelocity;
            }
        }

        public void EndScriptedControl()
        {
            _playerHandler?.EndScriptedControl();
            SetMovementInput(0f);
        }

        public void RequestJump()
        {
            _jump.RequestJump();
        }

        public void ReleaseJump()
        {
            _jump.ReleaseJump(ref _physics.VelocityReference);
        }

        public void SetControlEnabled(bool controlEnabled)
        {
            if (controlEnabled &&
                (PlayerRigidbodyStatus.IsDead || PlayerRigidbodyStatus.IsResetting))
            {
                return;
            }

            State.SetControlEnabled(controlEnabled);

            if (!controlEnabled)
            {
                SetMovementInput(0f);
                _jump.ResetRuntimeState();
            }

            PlayerRigidbodyStatus.SetStatus(controlEnabled ? Status.Normal : Status.Disabled);
            _playerHandler?.NotifyControlStateChanged();
        }

        public void FreezeMovement()
        {
            _playerHandler?.FreezeMovement();
        }

        public void UnFreezeMovement()
        {
            _playerHandler?.UnfreezeMovement();
        }

        internal void SetSimulatedVelocity(Vector2 velocity)
        {
            _physics.SetVelocity(velocity);
        }

        public void ZeroVelocity()
        {
            _physics.SetVelocity(Vector2.zero);

            if (_body != null)
            {
                _body.linearVelocity = Vector2.zero;
                _body.angularVelocity = 0f;
            }
        }

        public void ResetLocomotionRuntimeState()
        {
            _playerHandler?.BeginReset();
            SetMovementInput(0f);
            _playerHandler?.ResetRuntimeState();
            SetFacingDirection(1f);
            State.ResetRuntimeState();
            _jump.ResetRuntimeState();
            _movingPlatforms.ClearRuntimeState();
            _physics.ResetRuntimeState(_cachedTransform != null ? _cachedTransform.position : Vector2.zero);
            ZeroVelocity();
            _playerHandler?.CompleteReset();
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
            if (!_initialized || IsMovementFrozen)
            {
                return;
            }

            if (_physics.ShouldResetToSpawn(_cachedTransform.position.y))
            {
                ResetToSpawn();
                return;
            }

            _movingPlatforms.ApplyPlatformMotion(
                _cachedTransform,
                State.Grounded,
                _jump.IsInJumpAscentState);

            float deltaTime = Time.fixedDeltaTime;
            Vector2 previousPosition = _cachedTransform.position;
            _physics.BeginStep(previousPosition);
            bool wasGrounded = State.Grounded;
            _body.linearVelocity = Vector2.zero;
            _body.angularVelocity = 0f;
            _surfaceProbe.Probe(
                _capsuleCollider,
                _body,
                Velocity,
                State,
                _jump.ShouldIgnoreGrounding);
            _jump.SetSteepSlopeJumpRestriction(
                _surfaceProbe.IsSteepSlopeJumpRestricted);
            _jump.UpdateRuntimeState(Velocity, State, deltaTime);

            bool blockMovementIntoSteepSlope = _physics.ShouldBlockMovementIntoSteepSlope(
                _capsuleCollider,
                _surfaceProbe,
                _movementInput,
                _facingDirection);
            _physics.SetMovementBlockedBySteepSlope(blockMovementIntoSteepSlope);
            State.SetSliding(false);
            _movement.ApplyMovement(
                ref _physics.VelocityReference,
                State,
                _surfaceProbe,
                _movementInput,
                blockMovementIntoSteepSlope,
                _facingDirection,
                deltaTime);

            if (_jump.ApplyJump(
                ref _physics.VelocityReference,
                State,
                blockMovementIntoSteepSlope,
                deltaTime))
            {
                JumpTriggered?.Invoke();
            }

            _physics.ApplyGravity(State, deltaTime);
            Vector2 intendedDisplacement = Velocity * deltaTime;
            Vector2 resolvedDisplacement = _physics.ResolveDisplacement(
                previousPosition,
                _capsuleCollider,
                intendedDisplacement,
                State,
                _surfaceProbe,
                _collisionResolver);
            TranslateBy(resolvedDisplacement);
            _physics.ApplyResolvedSurface(
                _physics.LastBlockingHit,
                intendedDisplacement,
                _surfaceProbe,
                State);
            _physics.ApplyGroundSnap(
                _cachedTransform,
                _capsuleCollider,
                wasGrounded,
                _jump.IsInJumpAscentState,
                _surfaceProbe,
                _collisionResolver,
                State);
            _movingPlatforms.Track(State, _surfaceProbe.SurfaceCollider);
            _movingPlatforms.FinalizePlatformSync();
            _visuals.Align(_visualRoot, State, deltaTime);
            State.SetVerticalMotion(Velocity.y);
            State.RefreshPhysicsState(
                _movementInput,
                Velocity,
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
            if (!State.ControlEnabled || IsMovementFrozen ||
                PlayerRigidbodyStatus.IsBusy || PlayerRigidbodyStatus.IsDead ||
                PlayerRigidbodyStatus.IsResetting || PlayerRigidbodyStatus.IsDisabled ||
                PlayerRigidbodyStatus.IsCutscene)
            {
                SetMovementInput(0f);
                return;
            }

            if (IsScriptedControlActive)
            {
                SetMovementInput(_playerHandler.ScriptedMoveInput.x);
                return;
            }

            if (_gameInput == null)
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
                : Velocity.x;

            if (Mathf.Abs(direction) <= 0.01f)
            {
                return;
            }

            SetFacingDirection(Mathf.Sign(direction));
        }

        public void SetFacingDirection(float direction)
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
            _visuals.ApplyFacing(_visualRoot, _facingDirection);
        }

        private void TranslateBy(Vector2 displacement)
        {
            _cachedTransform.Translate(displacement, Space.World);
            Physics2D.SyncTransforms();
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
            if (State.ControlEnabled && !IsMovementFrozen &&
                !PlayerRigidbodyStatus.IsBusy && !PlayerRigidbodyStatus.IsDead &&
                !PlayerRigidbodyStatus.IsResetting && !PlayerRigidbodyStatus.IsDisabled &&
                !PlayerRigidbodyStatus.IsCutscene)
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

        private void OnDrawGizmosSelected()
        {
            if (_physics == null)
            {
                return;
            }

            CapsuleCollider2D capsuleCollider = _capsuleCollider != null
                ? _capsuleCollider
                : GetComponent<CapsuleCollider2D>();
            _physics.DrawSelectedGizmos(
                _cachedTransform != null ? _cachedTransform : transform,
                capsuleCollider,
                Velocity,
                State,
                _facingDirection);
        }

        private void OnValidate()
        {
            _physics?.Validate();
            _visuals?.Validate();
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
