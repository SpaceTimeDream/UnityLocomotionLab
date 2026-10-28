using System;
using Portfolio.Input;
using Portfolio.Locomotion.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Portfolio.Locomotion.Rigidbody
{
    /// <summary>
    /// Owns jump input, buffering, coyote time, anticipation, jump velocity, and ground-check delay.
    /// PlayerRigidbodyController invokes UpdateJump in the shared fixed-step sequence.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerRigidbodyJump : MonoBehaviour
    {
        //------------------------------- Game Pause -------------------------------//

        //------------------------------- Debugging Methods -------------------------------//

        //------------------------------- Parameters -------------------------------//

        #region Components

        [Header("COMPONENTS")]
        [SerializeField] private PlayerRigidbodyController _controller;
        [SerializeField] private GameInput _gameInput;

        [Header("JUMP PARAMETERS")]
        [FormerlySerializedAs("_jumpVelocity")]
        [InspectorName("jump")]
        [SerializeField] private float _jump = 13.3f;

        [FormerlySerializedAs("_maximumJumpVelocity")]
        [InspectorName("maxJumpForce")]
        [SerializeField] private float _maxJumpForce = 13.3f;

        [SerializeField] private float _jumpForceClampModifier;
        [FormerlySerializedAs("_jumpBufferTime")]
        [InspectorName("jumpBufferDuration")]
        [SerializeField] private float _jumpBufferDuration = 0.12f;

        [FormerlySerializedAs("_coyoteTime")]
        [InspectorName("coyoteTimeThreshold")]
        [SerializeField] private float _coyoteTimeThreshold = 0.1f;

        [SerializeField] private float _jumpAnticipationDuration = 0.04f;
        [FormerlySerializedAs("_groundCheckDelayFixedFrames")]
        [InspectorName("groundedCheckDelay")]
        [SerializeField] private int _groundedCheckDelay = 5;

        [Header("EARLY RELEASE")]
        [SerializeField] private bool _enableEarlyRelease = true;
        [SerializeField, Range(0f, 1f)] private float _earlyReleaseVelocityMultiplier = 0.5f;

        public float jump
        {
            get => _jump;
            set => _jump = Mathf.Max(0f, value);
        }

        public float maxJumpForce
        {
            get => _maxJumpForce;
            set => _maxJumpForce = Mathf.Max(0f, value);
        }

        public float jumpBufferDuration
        {
            get => _jumpBufferDuration;
            set => _jumpBufferDuration = Mathf.Max(0f, value);
        }

        public float coyoteTimeThreshold
        {
            get => _coyoteTimeThreshold;
            set => _coyoteTimeThreshold = Mathf.Max(0f, value);
        }

        public float jumpAnticipationDuration
        {
            get => _jumpAnticipationDuration;
            set => _jumpAnticipationDuration = Mathf.Max(0f, value);
        }

        public int groundedCheckDelay
        {
            get => _groundedCheckDelay;
            set => _groundedCheckDelay = Mathf.Max(0, value);
        }

        public float jumpForceClampModifier => _jumpForceClampModifier;
        public float JumpBufferRemainingTime => _jumpBufferRemainingTime;
        public float CoyoteRemainingTime => _coyoteRemainingTime;
        public bool HasBufferedJump => _jumpBufferRemainingTime > 0f;
        public bool CanUseCoyoteTime => _coyoteRemainingTime > 0f;
        public bool IsInAnticipationFrame => _anticipationRemainingTime > 0f;
        public bool IsInJumpAscentState => _inJumpAscentState;
        public bool IsJumpDisabled => _jumpDisabled;
        public bool IsSteepSlopeJumpRestricted => _steepSlopeJumpRestricted;
        public bool ShouldIgnoreGrounding => _groundCheckDelayFramesRemaining > 0;
        public bool HasJumped => _jumpConsumed;

        #endregion

        #region Flags

        private const float MINIMUM_INPUT = 0.01f;

        private bool _isInitialized;
        private float _jumpBufferRemainingTime;
        private float _coyoteRemainingTime;
        private float _anticipationRemainingTime;
        private int _groundCheckDelayFramesRemaining;
        private bool _jumpConsumed;
        private bool _jumpHeld;
        private bool _inJumpAscentState;
        private bool _jumpDisabled;
        private bool _steepSlopeJumpRestricted;
        private bool _eventsInitialized;

        public event Action OnJumpTriggered;

        #endregion

        //------------------------------- Unity Methods -------------------------------//

        #region Unity

        private void Start()
        {
            Initialize();
        }

        private void OnEnable()
        {
            if (_isInitialized)
            {
                InitializeEvents();
            }
        }

        private void OnDisable()
        {
            UninitializeEvents();
        }

        private void OnDestroy()
        {
            UninitializeEvents();
        }

        #endregion

        //------------------------------- Initialization Methods -------------------------------//

        #region Initialization

        /// <summary>
        /// Binds the module and its input source. Safe to call repeatedly after an input source appears.
        /// </summary>
        public void Initialize(PlayerRigidbodyController controller)
        {
            UninitializeEvents();
            _controller = controller;
            if (_gameInput == null)
            {
                _gameInput = GameInput.Instance;
            }

            Validate();
            _isInitialized = _controller != null;
            InitializeEvents();
        }

        private void Initialize()
        {
            if (_isInitialized)
            {
                InitializeEvents();
                return;
            }

            if (_controller == null)
            {
                _controller = GetComponent<PlayerRigidbodyController>();
            }

            if (_gameInput == null)
            {
                _gameInput = GameInput.Instance;
            }

            Validate();
            _isInitialized = _controller != null;
            InitializeEvents();
        }

        private void InitializeEvents()
        {
            if (_eventsInitialized || _gameInput == null)
            {
                return;
            }

            _gameInput.jumpEvent += HandleJumpPressed;
            _gameInput.jumpCanceledEvent += HandleJumpReleased;
            _eventsInitialized = true;
        }

        private void UninitializeEvents()
        {
            if (!_eventsInitialized)
            {
                return;
            }

            if (_gameInput != null)
            {
                _gameInput.jumpEvent -= HandleJumpPressed;
                _gameInput.jumpCanceledEvent -= HandleJumpReleased;
            }

            _eventsInitialized = false;
        }

        private void HandleJumpPressed(InputAction.CallbackContext context)
        {
            if (_controller == null || !_controller.State.ControlEnabled ||
                _controller.IsMovementFrozen || PlayerRigidbodyStatus.IsBusy || PlayerRigidbodyStatus.IsDead ||
                PlayerRigidbodyStatus.IsResetting || PlayerRigidbodyStatus.IsDisabled || PlayerRigidbodyStatus.IsCutscene)
            {
                return;
            }

            RequestJump();
        }

        private void HandleJumpReleased(InputAction.CallbackContext context)
        {
            ReleaseJump();
        }

        #endregion

        //------------------------------- Update Methods -------------------------------//

        #region Update

        /// <summary>
        /// Advances buffering, coyote time, anticipation, and jump execution once per fixed step.
        /// </summary>
        public void UpdateJump()
        {
            if (!_isInitialized || _controller == null || _controller.Body == null)
            {
                return;
            }

            if (_anticipationRemainingTime > 0f)
            {
                _anticipationRemainingTime -= Time.fixedDeltaTime;
                if (_anticipationRemainingTime <= 0f)
                {
                    if (ExecuteJump())
                    {
                        OnJumpTriggered?.Invoke();
                        _controller.NotifyJumpTriggered();
                    }
                }

                return;
            }

            if (HasBufferedJump && CanStartJump())
            {
                _jumpConsumed = true;
                _anticipationRemainingTime = _jumpAnticipationDuration;
                _jumpBufferRemainingTime = 0f;

                if (_anticipationRemainingTime <= 0f && ExecuteJump())
                {
                    OnJumpTriggered?.Invoke();
                    _controller.NotifyJumpTriggered();
                }
            }
        }

        /// <summary>
        /// Updates jump timers and grounded transitions before movement is applied.
        /// </summary>
        public void UpdateJumpState(UnityEngine.Rigidbody2D body, LocomotionState state)
        {
            if (body == null || state == null)
            {
                return;
            }

            if (_groundCheckDelayFramesRemaining > 0)
            {
                _groundCheckDelayFramesRemaining--;
            }

            if (_jumpBufferRemainingTime > 0f && _anticipationRemainingTime <= 0f)
            {
                _jumpBufferRemainingTime = Mathf.Max(0f, _jumpBufferRemainingTime - Time.fixedDeltaTime);
            }

            if (state.Grounded && _anticipationRemainingTime <= 0f && !_inJumpAscentState)
            {
                _coyoteRemainingTime = _coyoteTimeThreshold;
                _jumpConsumed = false;
            }
            else
            {
                _coyoteRemainingTime = Mathf.Max(0f, _coyoteRemainingTime - Time.fixedDeltaTime);
            }

            if (_inJumpAscentState && _anticipationRemainingTime <= 0f && body.linearVelocity.y <= 0f)
            {
                _inJumpAscentState = false;
            }
        }

        #endregion

        //------------------------------- Jump Methods -------------------------------//

        #region Jump

        /// <summary>
        /// Requests a buffered jump.
        /// </summary>
        public void RequestJump()
        {
            if (_jumpDisabled)
            {
                return;
            }

            _jumpHeld = true;
            _jumpBufferRemainingTime = _jumpBufferDuration;
        }

        /// <summary>
        /// Releases jump input and applies the optional early-release velocity reduction.
        /// </summary>
        public void ReleaseJump()
        {
            _jumpHeld = false;

            if (!_enableEarlyRelease || !_inJumpAscentState || _controller == null ||
                _controller.Body == null || _controller.Body.linearVelocity.y <= 0f)
            {
                return;
            }

            Vector2 velocity = _controller.Body.linearVelocity;
            velocity.y *= _earlyReleaseVelocityMultiplier;
            _controller.Body.linearVelocity = velocity;
        }

        /// <summary>
        /// Disables or enables jump starts.
        /// </summary>
        public void DisableJump(bool disabled)
        {
            _jumpDisabled = disabled;
            if (disabled)
            {
                ResetRuntimeState();
            }
        }

        /// <summary>
        /// Sets whether a steep surface currently restricts jumping.
        /// </summary>
        public void SetSteepSlopeJumpRestriction(bool restricted)
        {
            _steepSlopeJumpRestricted = restricted;
        }

        /// <summary>
        /// Sets the temporary jump-force clamp.
        /// </summary>
        public void SetJumpForceClamp(float value)
        {
            _jumpForceClampModifier = Mathf.Max(0f, value);
        }

        /// <summary>
        /// Increases the temporary jump-force clamp.
        /// </summary>
        public void AddJumpForceClamp(float value)
        {
            _jumpForceClampModifier = Mathf.Max(_jumpForceClampModifier, Mathf.Abs(value));
        }

        public void ResetJumpBuffer()
        {
            _jumpBufferRemainingTime = 0f;
        }

        public void ResetRuntimeState()
        {
            _jumpBufferRemainingTime = 0f;
            _coyoteRemainingTime = 0f;
            _anticipationRemainingTime = 0f;
            _groundCheckDelayFramesRemaining = 0;
            _jumpConsumed = false;
            _jumpHeld = false;
            _inJumpAscentState = false;
            _steepSlopeJumpRestricted = false;
        }

        private bool CanStartJump()
        {
            return _jumpBufferRemainingTime > 0f &&
                !_jumpConsumed &&
                _controller != null &&
                _controller.State.ControlEnabled &&
                !PlayerRigidbodyStatus.IsBusy && !PlayerRigidbodyStatus.IsDead && !PlayerRigidbodyStatus.IsResetting &&
                !PlayerRigidbodyStatus.IsDisabled && !PlayerRigidbodyStatus.IsCutscene &&
                !_jumpDisabled &&
                !_steepSlopeJumpRestricted &&
                (_controller.State.Grounded || _coyoteRemainingTime > 0f);
        }

        private bool ExecuteJump()
        {
            if (_controller == null || _controller.Body == null || !_controller.State.ControlEnabled ||
                _jumpDisabled || _steepSlopeJumpRestricted || PlayerRigidbodyStatus.IsBusy ||
                PlayerRigidbodyStatus.IsDead || PlayerRigidbodyStatus.IsResetting || PlayerRigidbodyStatus.IsDisabled || PlayerRigidbodyStatus.IsCutscene)
            {
                _anticipationRemainingTime = 0f;
                _jumpConsumed = false;
                return false;
            }

            UnityEngine.Rigidbody2D body = _controller.Body;
            Vector2 velocity = body.linearVelocity;
            float steepSlopeBlockDirection = _controller.SteepSlopeBlockDirection;

            if (velocity.x * steepSlopeBlockDirection > MINIMUM_INPUT)
            {
                velocity.x = 0f;
            }

            float baseVerticalVelocity = _controller.State.Grounded ? 0f : Mathf.Max(velocity.y, 0f);
            float clampedJumpForce = Mathf.Max(_jump, _jumpForceClampModifier);
            velocity.y = Mathf.Min(baseVerticalVelocity + clampedJumpForce, _maxJumpForce);

            if (_enableEarlyRelease && !_jumpHeld)
            {
                velocity.y *= _earlyReleaseVelocityMultiplier;
            }

            body.linearVelocity = velocity;
            _groundCheckDelayFramesRemaining = _groundedCheckDelay;
            _anticipationRemainingTime = 0f;
            _inJumpAscentState = true;
            _coyoteRemainingTime = 0f;
            return true;
        }

        #endregion

        //------------------------------- Editor -------------------------------//

        /// <summary>
        /// Clamps serialized jump tuning to safe runtime values.
        /// </summary>
        public void Validate()
        {
            _jump = Mathf.Max(0f, _jump);
            _maxJumpForce = Mathf.Max(_jump, _maxJumpForce);
            _jumpBufferDuration = Mathf.Max(0f, _jumpBufferDuration);
            _coyoteTimeThreshold = Mathf.Max(0f, _coyoteTimeThreshold);
            _jumpAnticipationDuration = Mathf.Max(0f, _jumpAnticipationDuration);
            _groundedCheckDelay = Mathf.Max(0, _groundedCheckDelay);
            _earlyReleaseVelocityMultiplier = Mathf.Clamp01(_earlyReleaseVelocityMultiplier);
            _jumpForceClampModifier = Mathf.Max(0f, _jumpForceClampModifier);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            Validate();
        }
#endif
    }
}
