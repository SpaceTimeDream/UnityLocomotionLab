using System;
using Portfolio.Character;
using Portfolio.Locomotion.Core;
using UnityEngine;

namespace Portfolio.Locomotion.Rigidbody
{
    /// <summary>
    /// Coordinates global player status and modes, busy/disabled transitions, and animation.
    /// All animation state changes are routed through CharacterAnimationController2D.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerRigidbodyHandler : MonoBehaviour
    {
        //------------------------------- Game Pause -------------------------------//

        //------------------------------- Debugging Methods -------------------------------//

        //------------------------------- Parameters -------------------------------//

        #region Components

        [Header("COMPONENTS")]
        [SerializeField] private CharacterAnimationController2D _characterAnimationController;
        [SerializeField] private PlayerRigidbodyController _playerController;
        [SerializeField] private PlayerRigidbodyPhysics _playerPhysics;

        public static PlayerRigidbodyHandler Instance { get; private set; }
        public static event Action<bool> OnPlayerDisabledStateChanged;
        public static bool IsPlayerDisabled =>
            Instance == null || Instance._state == null ||
            !Instance._state.ControlEnabled || Instance._movementFrozen ||
            PlayerRigidbodyStatus.IsDead || PlayerRigidbodyStatus.IsResetting || PlayerRigidbodyStatus.IsDisabled;

        public CharacterAnimationController2D CharacterAnimationController => _characterAnimationController;
        public bool IsMovementFrozen => _movementFrozen;
        public bool IsScriptedControlActive => _scriptedControlActive;
        public Vector2 ScriptedMoveInput => _scriptedMoveInput;
        public bool IsRestoringBaseline => _isRestoringBaseline;
        public int BaselineRestoreCount => _baselineRestoreCount;
        public int ResetInvocationCount => _resetInvocationCount;
        public string ResetPipelineStage => _resetPipelineStage;
        public string LastResetPipelineLog => _lastResetPipelineLog;

        #endregion

        #region Flags

        private bool _movementFrozen;
        private bool _scriptedControlActive;
        private Vector2 _scriptedMoveInput;
        private Vector2 _cachedPausedVelocity;
        private float _cachedPausedGravityScale;
        private LocomotionState _state;
        private LocomotionPhysicsState _lastState;
        private bool _lastDisabledState;
        private bool _isInitialized;
        private bool _hasAnimationFacingDirection;
        private float _lastAnimationFacingDirection = 1f;
        private bool _isRestoringBaseline;
        private int _baselineRestoreCount;
        private int _resetInvocationCount;
        private float _lastBaselineRestoreRealtime = -1f;
        private string _resetPipelineStage = "Idle";
        private string _lastResetPipelineLog = "Never";

        public event Action<LocomotionPhysicsState> StateTransitioned;
        public event Action<bool> WalkingChanged;
        public event Action<bool> JumpingChanged;
        public event Action<bool> FallingChanged;
        public event Action<bool> RisingChanged;
        public event Action<bool> SlidingChanged;
        public event Action JumpSoundRequested;

        #endregion

        //------------------------------- Unity Methods -------------------------------//

        #region Unity

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("Multiple PlayerRigidbodyHandler components found; only one should own global player status.", this);
                return;
            }

            Instance = this;
        }

        private void OnEnable()
        {
            if (_isInitialized)
            {
                SubscribeEvents();
            }
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        #endregion

        //------------------------------- Initialization Methods -------------------------------//

        #region Initialization

        /// <summary>
        /// Binds this handler to the player modules and installs status/physics event handlers.
        /// </summary>
        public void Initialize(
            PlayerRigidbodyController controller,
            LocomotionState state,
            PlayerRigidbodyPhysics physics)
        {
            if (_playerController != controller && _playerController != null)
            {
                _playerController.DirectionChanged -= HandleDirectionChanged;
            }

            UnsubscribeEvents();
            _playerController = controller;
            _playerPhysics = physics;
            _state = state;

            if (_characterAnimationController == null && controller != null)
            {
                _characterAnimationController = controller.GetComponentInChildren<CharacterAnimationController2D>(true);
            }

            _lastState = _state != null ? _state.CurrentState : LocomotionPhysicsState.None;
            _lastAnimationFacingDirection = _playerController != null ? _playerController.FacingDirection : 1f;
            _hasAnimationFacingDirection = _playerController != null;
            _lastDisabledState = EvaluateDisabledState();
            _isInitialized = _playerController != null && _state != null && _playerPhysics != null;

            if (_characterAnimationController != null)
            {
                _characterAnimationController.SetLocomotionAnimationDriverEnabled(false);
                _characterAnimationController.SetFacingDirection(_lastAnimationFacingDirection);
                _characterAnimationController.SetCutsceneMode(PlayerRigidbodyStatus.HasMode(Mode.Cutscene));
            }

            if (_isInitialized)
            {
                SubscribeEvents();
            }
        }

        private void SubscribeEvents()
        {
            UnsubscribeEvents();

            if (_state != null)
            {
                _state.StateChanged += HandleStateChanged;
            }

            if (_playerController != null)
            {
                _playerController.DirectionChanged -= HandleDirectionChanged;
                _playerController.DirectionChanged += HandleDirectionChanged;
            }

            PlayerRigidbodyPhysics.OnPhysicsStateChanged -= HandlePhysicsStateChanged;
            PlayerRigidbodyPhysics.OnPhysicsStateChanged += HandlePhysicsStateChanged;
            PlayerRigidbodyStatus.OnBusyStateChanged -= HandleBusyStateChanged;
            PlayerRigidbodyStatus.OnBusyStateChanged += HandleBusyStateChanged;
            PlayerRigidbodyStatus.OnStatusChanged -= HandleStatusChanged;
            PlayerRigidbodyStatus.OnStatusChanged += HandleStatusChanged;
            PlayerRigidbodyStatus.OnModeChanged -= HandleModeChanged;
            PlayerRigidbodyStatus.OnModeChanged += HandleModeChanged;
        }

        private void UnsubscribeEvents()
        {
            if (_state != null)
            {
                _state.StateChanged -= HandleStateChanged;
            }

            if (_playerController != null)
            {
                _playerController.DirectionChanged -= HandleDirectionChanged;
            }

            PlayerRigidbodyPhysics.OnPhysicsStateChanged -= HandlePhysicsStateChanged;
            PlayerRigidbodyStatus.OnBusyStateChanged -= HandleBusyStateChanged;
            PlayerRigidbodyStatus.OnStatusChanged -= HandleStatusChanged;
            PlayerRigidbodyStatus.OnModeChanged -= HandleModeChanged;
        }

        #endregion

        //------------------------------- Event Handlers -------------------------------//

        #region Events

        private void HandleStateChanged(LocomotionPhysicsState nextState)
        {
            LocomotionPhysicsState previousState = _lastState;
            _lastState = nextState;
            StateTransitioned?.Invoke(nextState);
            NotifyDisabledStateChanged();

            NotifyFlagChanged(previousState, nextState, LocomotionPhysicsState.Walking, WalkingChanged);
            NotifyFlagChanged(previousState, nextState, LocomotionPhysicsState.Jumping, JumpingChanged);
            NotifyFlagChanged(previousState, nextState, LocomotionPhysicsState.Falling, FallingChanged);
            NotifyFlagChanged(previousState, nextState, LocomotionPhysicsState.Rising, RisingChanged);
            NotifyFlagChanged(previousState, nextState, LocomotionPhysicsState.Sliding, SlidingChanged);
        }

        private void HandlePhysicsStateChanged(PhysicsState physicsState)
        {
            // Physics state enters PlayerRigidbodyStatus only through this handler.
            PlayerRigidbodyStatus.HandlePhysicsState(physicsState);
            ApplyAnimationForPhysicsState(physicsState);
        }

        private void HandleBusyStateChanged(bool isBusy)
        {
            if (isBusy)
            {
                _characterAnimationController?.Idle();
                return;
            }

            ApplyAnimationForPhysicsState(PlayerRigidbodyStatus.CurrentState);
        }

        private void HandleStatusChanged(Status status)
        {
            switch (status)
            {
                case Status.Dead:
                    _characterAnimationController?.Die();
                    break;
                case Status.Resetting:
                case Status.Respawning:
                    _characterAnimationController?.ResetToIdle();
                    break;
                case Status.Disabled:
                    _characterAnimationController?.Idle();
                    break;
                case Status.Normal:
                    ApplyAnimationForPhysicsState(PlayerRigidbodyStatus.CurrentState);
                    break;
            }

            NotifyDisabledStateChanged();
        }

        private void HandleModeChanged(Mode mode)
        {
            bool cutsceneEnabled = (mode & Mode.Cutscene) == Mode.Cutscene;
            _characterAnimationController?.SetCutsceneMode(cutsceneEnabled);

            if (!cutsceneEnabled)
            {
                ApplyAnimationForPhysicsState(PlayerRigidbodyStatus.CurrentState);
            }

            NotifyDisabledStateChanged();
        }

        private void HandleDirectionChanged(float direction)
        {
            if (_characterAnimationController == null)
            {
                return;
            }

            _characterAnimationController.SetFacingDirection(direction);

            if (_hasAnimationFacingDirection &&
                !Mathf.Approximately(_lastAnimationFacingDirection, direction) &&
                !PlayerRigidbodyStatus.IsBusy && !PlayerRigidbodyStatus.IsDead &&
                !PlayerRigidbodyStatus.IsResetting && !PlayerRigidbodyStatus.IsDisabled &&
                !PlayerRigidbodyStatus.HasMode(Mode.Cutscene))
            {
                _characterAnimationController.Turn();

                if ((PlayerRigidbodyStatus.CurrentState & PhysicsState.Walking) == PhysicsState.Walking)
                {
                    _characterAnimationController.SetRunning(true);
                }
            }

            _lastAnimationFacingDirection = direction;
            _hasAnimationFacingDirection = true;
        }

        private void ApplyAnimationForPhysicsState(PhysicsState physicsState)
        {
            if (_characterAnimationController == null)
            {
                return;
            }

            if (PlayerRigidbodyStatus.IsBusy || PlayerRigidbodyStatus.IsDead || PlayerRigidbodyStatus.IsResetting ||
                PlayerRigidbodyStatus.IsDisabled || PlayerRigidbodyStatus.HasMode(Mode.Cutscene))
            {
                _characterAnimationController.SetRunning(false);
                _characterAnimationController.SetJumping(false);
                _characterAnimationController.SetFalling(false);
                _characterAnimationController.SetFloatingUp(false);
                _characterAnimationController.SetSliding(false);
                return;
            }

            bool walking = (physicsState & PhysicsState.Walking) == PhysicsState.Walking;
            bool jumping = (physicsState & PhysicsState.Jumping) == PhysicsState.Jumping;
            bool falling = (physicsState & PhysicsState.Falling) == PhysicsState.Falling;
            bool floatingUp = (physicsState & PhysicsState.FloatingUp) == PhysicsState.FloatingUp;
            bool sliding = (physicsState & PhysicsState.Sliding) == PhysicsState.Sliding;
            bool idle = (physicsState & PhysicsState.Idle) == PhysicsState.Idle &&
                !walking && !jumping && !falling && !floatingUp && !sliding;

            _characterAnimationController.SetRunning(walking);
            // Reuse the jump loop for the entire airborne phase, including falling.
            _characterAnimationController.SetJumping(jumping || falling);
            _characterAnimationController.SetFalling(false);
            _characterAnimationController.SetFloatingUp(floatingUp);
            _characterAnimationController.SetSliding(sliding);

            if (idle)
            {
                _characterAnimationController.Idle();
            }
        }

        private static void NotifyFlagChanged(
            LocomotionPhysicsState previousState,
            LocomotionPhysicsState nextState,
            LocomotionPhysicsState flag,
            Action<bool> callback)
        {
            bool wasActive = HasState(previousState, flag);
            bool isActive = HasState(nextState, flag);
            if (wasActive != isActive)
            {
                callback?.Invoke(isActive);
            }
        }

        private static bool HasState(LocomotionPhysicsState state, LocomotionPhysicsState flag)
        {
            return (state & flag) == flag;
        }

        #endregion

        //------------------------------- Control Methods -------------------------------//

        #region Scripted Control

        public void BeginScriptedControl()
        {
            _scriptedMoveInput = Vector2.zero;
            _scriptedControlActive = true;
            NotifyDisabledStateChanged();
        }

        public void SetScriptedMoveInput(Vector2 input)
        {
            if (_scriptedControlActive)
            {
                _scriptedMoveInput = Vector2.ClampMagnitude(input, 1f);
            }
        }

        public void EndScriptedControl()
        {
            _scriptedMoveInput = Vector2.zero;
            _scriptedControlActive = false;
            NotifyDisabledStateChanged();
        }

        #endregion

        #region Movement Freeze

        public void Freeze(UnityEngine.Rigidbody2D body)
        {
            if (_movementFrozen || body == null)
            {
                return;
            }

            _movementFrozen = true;
            _cachedPausedVelocity = body.linearVelocity;
            _cachedPausedGravityScale = body.gravityScale;
            body.linearVelocity = Vector2.zero;
            body.gravityScale = 0f;
            SetAnimationFrozen(true);
            NotifyDisabledStateChanged();
        }

        public void Unfreeze(UnityEngine.Rigidbody2D body)
        {
            if (!_movementFrozen || body == null)
            {
                return;
            }

            _movementFrozen = false;
            body.linearVelocity = _cachedPausedVelocity;
            body.gravityScale = _cachedPausedGravityScale;
            SetAnimationFrozen(false);
            NotifyDisabledStateChanged();
        }

        public void SetAnimationFrozen(bool frozen)
        {
            _characterAnimationController?.SetAnimationFrozen(frozen);
        }

        public void NotifyControlStateChanged()
        {
            NotifyDisabledStateChanged();
        }

        private bool EvaluateDisabledState()
        {
            return _state == null || !_state.ControlEnabled || _movementFrozen ||
                PlayerRigidbodyStatus.IsDead || PlayerRigidbodyStatus.IsResetting || PlayerRigidbodyStatus.IsDisabled;
        }

        private void NotifyDisabledStateChanged()
        {
            bool nextDisabledState = EvaluateDisabledState();
            if (_lastDisabledState == nextDisabledState)
            {
                return;
            }

            _lastDisabledState = nextDisabledState;
            OnPlayerDisabledStateChanged?.Invoke(nextDisabledState);
        }

        #endregion

        //------------------------------- Status and Mode Methods -------------------------------//

        #region Status

        public bool SetStatus(Status status)
        {
            return PlayerRigidbodyStatus.SetStatus(status);
        }

        public bool SetControlEnabledStatus(Status status)
        {
            return PlayerRigidbodyStatus.SetStatus(status);
        }

        public bool SetMode(Mode mode)
        {
            return PlayerRigidbodyStatus.SetMode(mode);
        }

        public bool AddMode(Mode mode)
        {
            return PlayerRigidbodyStatus.AddMode(mode);
        }

        public bool RemoveMode(Mode mode)
        {
            return PlayerRigidbodyStatus.RemoveMode(mode);
        }

        public void SetBusy(bool value)
        {
            PlayerRigidbodyStatus.IsBusy = value;
        }

        public void SetCutsceneMode(bool enabled)
        {
            if (enabled)
            {
                AddMode(Mode.Cutscene);
            }
            else
            {
                RemoveMode(Mode.Cutscene);
                if (PlayerRigidbodyStatus.CurrentStatus != Status.Resetting)
                {
                    SetStatus(Status.Normal);
                }
            }

            _characterAnimationController?.SetCutsceneMode(enabled);
        }

        #endregion

        //------------------------------- Reset Pipeline -------------------------------//

        #region Reset

        public void BeginReset()
        {
            if (_isRestoringBaseline)
            {
                return;
            }

            _isRestoringBaseline = true;
            _baselineRestoreCount++;
            _resetInvocationCount++;
            _lastBaselineRestoreRealtime = Time.realtimeSinceStartup;
            RecordResetPipelineStage("Entering Resetting");

            SetStatus(Status.Resetting);
            ResetRuntimeState();
            RecordResetPipelineStage("Runtime cleanup complete");
        }

        public void CompleteReset()
        {
            RecordResetPipelineStage("Restoring animation state");
            _characterAnimationController?.ResetToIdle();
            RecordResetPipelineStage("Restoring gameplay status");
            SetStatus(Status.Normal);
            SetMode(Mode.Normal);
            SetBusy(false);
            _isRestoringBaseline = false;
            NotifyDisabledStateChanged();
            RecordResetPipelineStage("Restore complete");
        }

        public void ResetRuntimeState()
        {
            _scriptedMoveInput = Vector2.zero;
            _scriptedControlActive = false;
        }

        private void RecordResetPipelineStage(string stage)
        {
            _resetPipelineStage = stage;
            _lastResetPipelineLog =
                $"#{_resetInvocationCount} {Time.realtimeSinceStartup:F2}s {stage} | " +
                $"status={PlayerRigidbodyStatus.CurrentStatus} mode={PlayerRigidbodyStatus.CurrentMode} " +
                $"state={PlayerRigidbodyStatus.CurrentState} busy={PlayerRigidbodyStatus.IsBusy} disabled={IsPlayerDisabled}";
        }

        #endregion

        //------------------------------- Utility Methods -------------------------------//

        /// <summary>
        /// Notifies audio or presentation extensions that a jump was successfully triggered.
        /// </summary>
        public void NotifyJumpTriggered()
        {
            JumpSoundRequested?.Invoke();
        }
    }
}
