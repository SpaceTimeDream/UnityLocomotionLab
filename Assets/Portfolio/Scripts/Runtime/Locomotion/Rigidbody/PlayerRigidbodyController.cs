using System;
using Portfolio.Game;
using Portfolio.Input;
using Portfolio.Locomotion.Core;
using UnityEngine;

namespace Portfolio.Locomotion.Rigidbody
{
    [RequireComponent(typeof(UnityEngine.Rigidbody2D))]
    [RequireComponent(typeof(CapsuleCollider2D))]
    [DisallowMultipleComponent]
    public class PlayerRigidbodyController : MonoBehaviour
    {
        //------------------------------- Game Pause -------------------------------//

        public virtual void OnGameStateChanged(GameState newGameState)
        {
            if (newGameState == GameState.Paused)
            {
                _playerPhysics?.FreezeMovement();
                _playerHandler?.SetAnimationFrozen(true);
                return;
            }

            _playerPhysics?.UnFreezeMovement();
            _playerHandler?.SetAnimationFrozen(false);
        }

        //------------------------------- Debugging Methods -------------------------------//

        //------------------------------- Parameters -------------------------------//

        #region Components

        [Header("COMPONENTS")]
        [SerializeField] private UnityEngine.Rigidbody2D _body;
        [SerializeField] private CapsuleCollider2D _capsuleCollider;
        [SerializeField] private Transform _visualRoot;
        [SerializeField] private GameInput _gameInput;

        [Header("PLAYER COMPONENTS")]
        [SerializeField] private PlayerRigidbodyHandler _playerHandler;
        [SerializeField] private PlayerRigidbodyJump _playerJump;
        [SerializeField] private PlayerRigidbodyMovement _playerMovement;
        [SerializeField] private PlayerRigidbodyPhysics _playerPhysics;

        public UnityEngine.Rigidbody2D Body => _body;
        public CapsuleCollider2D MainCollider => _capsuleCollider;
        public Transform VisualRoot => _visualRoot;
        public GameInput Input => _gameInput;
        public LocomotionState State => _playerPhysics != null ? _playerPhysics.State : null;
        public PlayerRigidbodySurfaceProbe SurfaceProbe => _playerPhysics != null ? _playerPhysics.SurfaceProbe : null;
        public PlayerRigidbodyExternalForceReceiver ExternalForces => _playerPhysics != null ? _playerPhysics.ExternalForces : null;
        public PlayerRigidbodyPlatformTracker MovingPlatforms => _playerPhysics != null ? _playerPhysics.MovingPlatforms : null;
        public PlayerRigidbodyHandler PlayerRigidbodyHandler => _playerHandler;
        public PlayerRigidbodyJump PlayerRigidbodyJump => _playerJump;
        public PlayerRigidbodyMovement PlayerRigidbodyMovement => _playerMovement;
        public PlayerRigidbodyJump Jump => _playerJump;
        public PlayerRigidbodyMovement Movement => _playerMovement;
        public PlayerRigidbodyPhysics PlayerRigidbodyPhysics => _playerPhysics;

        public float MovementInput => _movementInput;
        public float FacingDirection => _facingDirection;
        public Vector2 Velocity => _body != null ? _body.linearVelocity : Vector2.zero;
        public float Speed => Velocity.magnitude;
        public float VerticalVelocity => Velocity.y;
        public float HorizontalDistancePerFixedFrame => _playerPhysics != null
            ? _playerPhysics.HorizontalDistancePerFixedFrame
            : 0f;
        public float SteepSlopeBlockDirection => _playerPhysics != null
            ? _playerPhysics.SteepSlopeBlockDirection
            : 0f;
        public bool IsMovementFrozen => _playerHandler != null && _playerHandler.IsMovementFrozen;
        public bool IsScriptedControlActive => _playerHandler != null && _playerHandler.IsScriptedControlActive;

        public event Action<float> DirectionChanged;
        public event Action JumpTriggered;

        #endregion

        #region Flags

        private bool _initialized;
        private float _movementInput;
        private float _facingDirection = 1f;
        private Vector2 _scriptedMoveInput;

        #endregion

        //------------------------------- Unity Methods -------------------------------//

        #region Unity

        protected void Start()
        {
            Initialize();
        }

        protected void OnEnable()
        {
            if (_initialized)
            {
                InitializeEvents();
            }
        }

        protected void Update()
        {
            if (!_initialized)
            {
                return;
            }

            RefreshGameInput();
            UpdateParameters();
            UpdateModules();
        }

        protected void FixedUpdate()
        {
            if (!_initialized || !CanRunFixedStep())
            {
                return;
            }

            _playerPhysics.ApplyPlatformDelta();
            _playerPhysics.UpdatePhysics();
            _playerMovement.UpdateMovement();
            _playerJump.UpdateJump();
            _playerPhysics.FinalizePlatformSync();
        }

        protected void OnDisable()
        {
            UnsubscribeEvents();
            SetMovementInput(0f);
        }

        protected void OnDestroy()
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
            _initialized = true;
        }

        private void InitializeModules()
        {
            if (_gameInput == null)
            {
                _gameInput = GameInput.Instance;
            }

            if (_playerHandler == null)
            {
                _playerHandler = GetComponent<PlayerRigidbodyHandler>();
            }
            if (_playerHandler == null)
            {
                _playerHandler = gameObject.AddComponent<PlayerRigidbodyHandler>();
            }

            if (_playerJump == null)
            {
                _playerJump = GetComponent<PlayerRigidbodyJump>();
            }
            if (_playerJump == null)
            {
                _playerJump = gameObject.AddComponent<PlayerRigidbodyJump>();
            }

            if (_playerMovement == null)
            {
                _playerMovement = GetComponent<PlayerRigidbodyMovement>();
            }
            if (_playerMovement == null)
            {
                _playerMovement = gameObject.AddComponent<PlayerRigidbodyMovement>();
            }

            if (_playerPhysics == null)
            {
                _playerPhysics = GetComponent<PlayerRigidbodyPhysics>();
            }
            if (_playerPhysics == null)
            {
                _playerPhysics = gameObject.AddComponent<PlayerRigidbodyPhysics>();
            }

            _playerPhysics.Initialize(this);
            _playerMovement.Initialize(this);
            _playerJump.Initialize(this);
            _playerHandler.Initialize(this, _playerPhysics.State, _playerPhysics);
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

            GameManager.GameStateChanged -= OnGameStateChanged;
            GameManager.GameStateChanged += OnGameStateChanged;

            if (_gameInput == null)
            {
                return;
            }

            _gameInput.MovementValueChanged -= HandleMovementValueChanged;
            _gameInput.MovementValueChanged += HandleMovementValueChanged;
        }

        private void UnsubscribeEvents()
        {
            GameManager.GameStateChanged -= OnGameStateChanged;
            if (_gameInput != null)
            {
                _gameInput.MovementValueChanged -= HandleMovementValueChanged;
            }
        }

        #endregion

        //------------------------------- Update Methods -------------------------------//

        #region Update

        private void RefreshGameInput()
        {
            if (_gameInput != null || GameInput.Instance == null)
            {
                return;
            }

            _gameInput = GameInput.Instance;
            InitializeEvents();
            _playerJump?.Initialize(this);
        }

        private void UpdateParameters()
        {
            UpdateMoveInput();
        }

        private void UpdateModules()
        {
            UpdateFacingDirection();
        }

        private void UpdateMoveInput()
        {
            if (_playerHandler == null || _playerHandler.IsMovementFrozen ||
                PlayerRigidbodyStatus.IsBusy || PlayerRigidbodyStatus.IsDead || PlayerRigidbodyStatus.IsResetting ||
                PlayerRigidbodyStatus.IsDisabled || PlayerRigidbodyStatus.IsCutscene)
            {
                SetMovementInput(0f);
                return;
            }

            if (_playerHandler.IsScriptedControlActive)
            {
                if (!_playerPhysics.State.ControlEnabled)
                {
                    SetMovementInput(0f);
                    return;
                }

                SetMovementInput(_playerHandler.ScriptedMoveInput.x);
                return;
            }

            if (!_playerPhysics.State.ControlEnabled)
            {
                SetMovementInput(0f);
                return;
            }

            // PlayerRigidbodyController is the only locomotion class that reads movement from GameInput.
            SetMovementInput(_gameInput != null ? _gameInput.MovementValue.x : 0f);
        }

        private bool CanRunFixedStep()
        {
            // Disabled, busy, dead, and cutscene states still need grounding/gravity updates.
            // Movement and jump modules reject input independently; a pause freeze stops the step.
            return _playerHandler != null &&
                !_playerHandler.IsMovementFrozen &&
                _playerPhysics != null &&
                _playerPhysics.State != null &&
                _playerMovement != null &&
                _playerJump != null;
        }

        private void HandleMovementValueChanged(Vector2 movementValue)
        {
            // Keep a zero value immediately visible to the fixed-step controller on action cancellation.
            if (movementValue == Vector2.zero && !_playerHandler.IsScriptedControlActive)
            {
                SetMovementInput(0f);
            }
        }

        private void UpdateFacingDirection()
        {
            float direction = _playerMovement.ResolveFacingDirection(
                _movementInput,
                _body,
                _playerPhysics.ExternalForces,
                _facingDirection);

            if (!Mathf.Approximately(direction, _facingDirection))
            {
                SetFacingDirection(direction);
            }
        }

        private void SetMovementInput(float movementInput)
        {
            _movementInput = Mathf.Clamp(movementInput, -1f, 1f);
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
            if (_playerMovement == null || !_playerMovement.FlipVisualWithFacing || _visualRoot == null)
            {
                return;
            }

            Vector3 eulerAngles = _visualRoot.localEulerAngles;
            eulerAngles.y = _facingDirection > 0f ? 0f : 180f;
            _visualRoot.localEulerAngles = eulerAngles;
        }

        #endregion

        //------------------------------- Scripted Control Methods -------------------------------//

        #region Scripted Control

        /// <summary>
        /// Enters scripted-control mode. The script must supply movement via SetScriptedMoveInput.
        /// </summary>
        public void BeginScriptedControl()
        {
            _scriptedMoveInput = Vector2.zero;
            _playerHandler.BeginScriptedControl();
        }

        /// <summary>
        /// Supplies input while scripted control is active.
        /// </summary>
        public void SetScriptedMoveInput(Vector2 input)
        {
            _scriptedMoveInput = Vector2.ClampMagnitude(input, 1f);
            _playerHandler.SetScriptedMoveInput(_scriptedMoveInput);
        }

        public void StopScriptedHorizontalMotion()
        {
            if (_body == null)
            {
                return;
            }

            Vector2 velocity = _body.linearVelocity;
            velocity.x = 0f;
            _body.linearVelocity = velocity;
        }

        public void EndScriptedControl()
        {
            _scriptedMoveInput = Vector2.zero;
            _playerHandler.EndScriptedControl();
            SetMovementInput(0f);
        }

        #endregion

        //------------------------------- Control and Reset Methods -------------------------------//

        #region Control

        public void RequestJump()
        {
            _playerJump.RequestJump();
        }

        public void ReleaseJump()
        {
            _playerJump.ReleaseJump();
        }

        public void SetControlEnabled(bool controlEnabled)
        {
            if (controlEnabled && (PlayerRigidbodyStatus.IsDead || PlayerRigidbodyStatus.IsResetting))
            {
                return;
            }

            _playerPhysics.State.SetControlEnabled(controlEnabled);
            _playerHandler.SetControlEnabledStatus(controlEnabled ? Status.Normal : Status.Disabled);
            _playerHandler.NotifyControlStateChanged();

            if (!controlEnabled)
            {
                SetMovementInput(0f);
                _playerJump.ResetRuntimeState();
            }
        }

        public void FreezeMovement()
        {
            _playerPhysics?.FreezeMovement();
            _playerHandler?.SetAnimationFrozen(true);
        }

        public void UnFreezeMovement()
        {
            _playerPhysics?.UnFreezeMovement();
            _playerHandler?.SetAnimationFrozen(false);
        }

        public void ZeroVelocity()
        {
            _playerPhysics?.ZeroVelocity();
        }

        public void ResetLocomotionRuntimeState()
        {
            _playerHandler.BeginReset();
            SetMovementInput(0f);
            _scriptedMoveInput = Vector2.zero;
            SetFacingDirection(1f);
            _playerMovement.ResetRuntimeState();
            _playerJump.ResetRuntimeState();
            _playerPhysics.ResetRuntimeState();
            ZeroVelocity();
            _playerHandler.CompleteReset();
        }

        public void AddExternalImpulse(Vector2 impulse, bool protectHorizontalMomentum = true, bool clearOnGrounded = false)
        {
            _playerPhysics.AddImpulse(impulse, protectHorizontalMomentum, clearOnGrounded);
        }

        public void AddExternalVelocity(Vector2 velocity, bool protectHorizontalMomentum = true, bool clearOnGrounded = false)
        {
            _playerPhysics.AddExternalVelocity(velocity, protectHorizontalMomentum, clearOnGrounded);
        }

        public void SetExternalVelocity(Vector2 velocity, bool protectHorizontalMomentum = true, bool clearOnGrounded = false)
        {
            _playerPhysics.SetExternalVelocity(velocity, protectHorizontalMomentum, clearOnGrounded);
        }

        internal void NotifyJumpTriggered()
        {
            _playerHandler?.NotifyJumpTriggered();
            JumpTriggered?.Invoke();
        }

        #endregion

#if UNITY_EDITOR
        //------------------------------- Editor -------------------------------//

        private void OnValidate()
        {
            if (!Application.isPlaying)
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

                if (_playerHandler == null) _playerHandler = GetComponent<PlayerRigidbodyHandler>();
                if (_playerJump == null) _playerJump = GetComponent<PlayerRigidbodyJump>();
                if (_playerMovement == null) _playerMovement = GetComponent<PlayerRigidbodyMovement>();
                if (_playerPhysics == null) _playerPhysics = GetComponent<PlayerRigidbodyPhysics>();
            }
        }
#endif
    }
}
