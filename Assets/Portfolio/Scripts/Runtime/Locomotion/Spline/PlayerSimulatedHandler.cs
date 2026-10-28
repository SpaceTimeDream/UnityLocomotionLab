using System;
using Portfolio.Character;
using Portfolio.Locomotion.Core;
using Portfolio.Locomotion.Rigidbody;
using UnityEngine;

namespace Portfolio.Locomotion.Spline
{
    /// <summary>
    /// Coordinates simulated-player status, movement control, and animation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSimulatedHandler : MonoBehaviour
    {
        //------------------------------- Parameters -------------------------------//

        #region Components

        [Header("COMPONENTS")]
        [SerializeField] private CharacterAnimationController2D _characterAnimationController;
        [SerializeField] private PlayerSimulatedController _playerController;

        #endregion

        #region Flags

        private LocomotionState _state;
        private LocomotionPhysicsState _lastState;
        private bool _isInitialized;
        private bool _movementFrozen;
        private bool _scriptedControlActive;
        private Vector2 _scriptedMoveInput;
        private Vector2 _cachedPausedVelocity;
        private bool _hasAnimationFacingDirection;
        private float _lastAnimationFacingDirection = 1f;
        private bool _lastDisabledState;
        private bool _isRestoringBaseline;
        private int _baselineRestoreCount;
        private int _resetInvocationCount;
        private string _resetPipelineStage = "Idle";
        private string _lastResetPipelineLog = "Never";

        #endregion

        #region Properties

        public static PlayerSimulatedHandler Instance { get; private set; }
        public static event Action<bool> OnPlayerDisabledStateChanged;
        public static bool IsPlayerDisabled =>
            Instance == null || Instance._state == null ||
            !Instance._state.ControlEnabled || Instance._movementFrozen ||
            PlayerRigidbodyStatus.IsDead || PlayerRigidbodyStatus.IsResetting ||
            PlayerRigidbodyStatus.IsDisabled;

        public CharacterAnimationController2D CharacterAnimationController => _characterAnimationController;
        public bool IsMovementFrozen => _movementFrozen;
        public bool IsScriptedControlActive => _scriptedControlActive;
        public Vector2 ScriptedMoveInput => _scriptedMoveInput;
        public bool IsRestoringBaseline => _isRestoringBaseline;
        public int BaselineRestoreCount => _baselineRestoreCount;
        public int ResetInvocationCount => _resetInvocationCount;
        public string ResetPipelineStage => _resetPipelineStage;
        public string LastResetPipelineLog => _lastResetPipelineLog;

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
                Debug.LogWarning("Multiple PlayerSimulatedHandler components found; only one should own global player status.", this);
                return;
            }

            Instance = this;
        }

        private void OnEnable()
        {
            if (Instance == null || !Instance.isActiveAndEnabled)
            {
                Instance = this;
            }

            if (_isInitialized)
            {
                SubscribeEvents();
            }
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
            if (Instance == this)
            {
                Instance = null;
            }
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

        public void Initialize(PlayerSimulatedController controller, LocomotionState state)
        {
            if (_playerController != controller && _playerController != null)
            {
                _playerController.DirectionChanged -= HandleDirectionChanged;
            }

            UnsubscribeEvents();
            _playerController = controller;
            _state = state;

            if (_characterAnimationController == null && controller != null)
            {
                _characterAnimationController =
                    controller.GetComponentInChildren<CharacterAnimationController2D>(true);
            }

            _lastState = _state != null ? _state.CurrentState : LocomotionPhysicsState.None;
            _lastAnimationFacingDirection = _playerController != null
                ? _playerController.FacingDirection
                : 1f;
            _hasAnimationFacingDirection = _playerController != null;
            _lastDisabledState = EvaluateDisabledState();
            _isInitialized = _playerController != null && _state != null;

            if (_characterAnimationController != null)
            {
                _characterAnimationController.SetLocomotionAnimationDriverEnabled(false);
                _characterAnimationController.SetFacingDirection(_lastAnimationFacingDirection);
                _characterAnimationController.SetCutsceneMode(PlayerRigidbodyStatus.HasMode(Mode.Cutscene));
            }

            if (_isInitialized)
            {
                SubscribeEvents();
                ApplyAnimationForPhysicsState(_state.CurrentState);
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
                _playerController.JumpTriggered -= HandleJumpTriggered;
                _playerController.JumpTriggered += HandleJumpTriggered;
            }

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
                _playerController.JumpTriggered -= HandleJumpTriggered;
            }

            PlayerRigidbodyStatus.OnBusyStateChanged -= HandleBusyStateChanged;
            PlayerRigidbodyStatus.OnStatusChanged -= HandleStatusChanged;
            PlayerRigidbodyStatus.OnModeChanged -= HandleModeChanged;
        }

        #endregion

        //------------------------------- Event Handlers -------------------------------//

        #region Events

        private void HandleJumpTriggered()
        {
            JumpSoundRequested?.Invoke();
        }

        private void HandleStateChanged(LocomotionPhysicsState nextState)
        {
            LocomotionPhysicsState previousState = _lastState;
            _lastState = nextState;
            StateTransitioned?.Invoke(nextState);
            PlayerRigidbodyStatus.HandlePhysicsState(ToSharedPhysicsState(nextState));
            ApplyAnimationForPhysicsState(nextState);
            NotifyFlagChanged(previousState, nextState, LocomotionPhysicsState.Walking, WalkingChanged);
            NotifyFlagChanged(previousState, nextState, LocomotionPhysicsState.Jumping, JumpingChanged);
            NotifyFlagChanged(previousState, nextState, LocomotionPhysicsState.Falling, FallingChanged);
            NotifyFlagChanged(previousState, nextState, LocomotionPhysicsState.Rising, RisingChanged);
            NotifyFlagChanged(previousState, nextState, LocomotionPhysicsState.Sliding, SlidingChanged);
            NotifyDisabledStateChanged();
        }

        private void HandleBusyStateChanged(bool isBusy)
        {
            if (isBusy)
            {
                ResetLocomotionAnimationFlags();
                _characterAnimationController?.Idle();
                return;
            }

            ApplyAnimationForPhysicsState(PlayerRigidbodyStatus.IsCutscene
                ? LocomotionPhysicsState.None
                : _state != null ? _state.CurrentState : LocomotionPhysicsState.None);
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
                    ResetLocomotionAnimationFlags();
                    _characterAnimationController?.Idle();
                    break;
                case Status.Normal:
                    ApplyAnimationForPhysicsState(_state != null
                        ? _state.CurrentState
                        : LocomotionPhysicsState.None);
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
                ApplyAnimationForPhysicsState(_state != null
                    ? _state.CurrentState
                    : LocomotionPhysicsState.None);
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

                if (_state != null && HasState(_state.CurrentState, LocomotionPhysicsState.Walking))
                {
                    _characterAnimationController.SetRunning(true);
                }
            }

            _lastAnimationFacingDirection = direction;
            _hasAnimationFacingDirection = true;
        }

        private void ApplyAnimationForPhysicsState(LocomotionPhysicsState locomotionState)
        {
            if (_characterAnimationController == null)
            {
                return;
            }

            if (PlayerRigidbodyStatus.IsBusy || PlayerRigidbodyStatus.IsDead ||
                PlayerRigidbodyStatus.IsResetting || PlayerRigidbodyStatus.IsDisabled ||
                PlayerRigidbodyStatus.HasMode(Mode.Cutscene))
            {
                ResetLocomotionAnimationFlags();
                return;
            }

            bool walking = HasState(locomotionState, LocomotionPhysicsState.Walking);
            bool jumping = HasState(locomotionState, LocomotionPhysicsState.Jumping);
            bool falling = HasState(locomotionState, LocomotionPhysicsState.Falling);
            bool rising = HasState(locomotionState, LocomotionPhysicsState.Rising);
            bool sliding = HasState(locomotionState, LocomotionPhysicsState.Sliding);
            bool idle = HasState(locomotionState, LocomotionPhysicsState.Idle) &&
                !walking && !jumping && !falling && !rising && !sliding;

            _characterAnimationController.SetRunning(walking);
            _characterAnimationController.SetJumping(jumping || falling || rising);
            _characterAnimationController.SetFalling(false);
            _characterAnimationController.SetFloatingUp(false);
            _characterAnimationController.SetSliding(sliding);

            if (idle)
            {
                _characterAnimationController.Idle();
            }
        }

        private void ResetLocomotionAnimationFlags()
        {
            _characterAnimationController?.SetRunning(false);
            _characterAnimationController?.SetJumping(false);
            _characterAnimationController?.SetFalling(false);
            _characterAnimationController?.SetFloatingUp(false);
            _characterAnimationController?.SetSliding(false);
        }

        private static PhysicsState ToSharedPhysicsState(LocomotionPhysicsState state)
        {
            PhysicsState mapped = PhysicsState.None;
            AddFlag(ref mapped, PhysicsState.Idle, HasState(state, LocomotionPhysicsState.Idle));
            AddFlag(ref mapped, PhysicsState.Moving, HasState(state, LocomotionPhysicsState.Moving));
            AddFlag(ref mapped, PhysicsState.Walking, HasState(state, LocomotionPhysicsState.Walking));
            AddFlag(ref mapped, PhysicsState.Jumping, HasState(state, LocomotionPhysicsState.Jumping));
            AddFlag(ref mapped, PhysicsState.Falling, HasState(state, LocomotionPhysicsState.Falling));
            AddFlag(ref mapped, PhysicsState.Sliding, HasState(state, LocomotionPhysicsState.Sliding));
            AddFlag(ref mapped, PhysicsState.Pushed, HasState(state, LocomotionPhysicsState.Pushed));
            AddFlag(ref mapped, PhysicsState.OnSlope, HasState(state, LocomotionPhysicsState.OnSlope));
            AddFlag(ref mapped, PhysicsState.OnSteepSlope, HasState(state, LocomotionPhysicsState.OnSteepSlope));
            return mapped;
        }

        private static void AddFlag(ref PhysicsState state, PhysicsState flag, bool active)
        {
            if (active)
            {
                state |= flag;
            }
        }

        private static bool HasState(LocomotionPhysicsState state, LocomotionPhysicsState flag)
        {
            return flag != LocomotionPhysicsState.None && (state & flag) == flag;
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

        #endregion

        //------------------------------- Core / Routines Methods -------------------------------//

        #region Core

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
            _playerController?.SetMovementInput(0f);
            NotifyDisabledStateChanged();
        }

        public void FreezeMovement()
        {
            if (_movementFrozen || _playerController == null)
            {
                return;
            }

            _movementFrozen = true;
            _cachedPausedVelocity = _playerController.Velocity;
            _playerController.SetSimulatedVelocity(Vector2.zero);
            _playerController.SetMovementInput(0f);
            SetAnimationFrozen(true);
            NotifyDisabledStateChanged();
        }

        public void UnfreezeMovement()
        {
            if (!_movementFrozen || _playerController == null)
            {
                return;
            }

            _movementFrozen = false;
            _playerController.SetSimulatedVelocity(_cachedPausedVelocity);
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
                PlayerRigidbodyStatus.IsDead || PlayerRigidbodyStatus.IsResetting ||
                PlayerRigidbodyStatus.IsDisabled;
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

        public bool SetStatus(Status status)
        {
            return PlayerRigidbodyStatus.SetStatus(status);
        }

        public bool SetControlEnabledStatus(Status status)
        {
            bool accepted = PlayerRigidbodyStatus.SetStatus(status);
            if (accepted)
            {
                _playerController?.SetControlEnabled(status != Status.Disabled);
                NotifyDisabledStateChanged();
            }

            return accepted;
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

        public void SetControlEnabled(bool controlEnabled)
        {
            if (controlEnabled && (PlayerRigidbodyStatus.IsDead || PlayerRigidbodyStatus.IsResetting))
            {
                return;
            }

            _playerController?.SetControlEnabled(controlEnabled);
            PlayerRigidbodyStatus.SetStatus(controlEnabled ? Status.Normal : Status.Disabled);
            NotifyDisabledStateChanged();
        }

        public void BeginReset()
        {
            if (_isRestoringBaseline)
            {
                return;
            }

            _isRestoringBaseline = true;
            _baselineRestoreCount++;
            _resetInvocationCount++;
            _resetPipelineStage = "Entering Resetting";
            _lastResetPipelineLog = $"#{_resetInvocationCount} {Time.realtimeSinceStartup:F2}s {_resetPipelineStage}";

            SetStatus(Status.Resetting);
            ResetRuntimeState();
            _resetPipelineStage = "Runtime cleanup complete";
        }

        public void CompleteReset()
        {
            _characterAnimationController?.ResetToIdle();
            SetStatus(Status.Normal);
            SetMode(Mode.Normal);
            SetBusy(false);
            _isRestoringBaseline = false;
            NotifyDisabledStateChanged();
            _resetPipelineStage = "Restore complete";
            _lastResetPipelineLog = $"#{_resetInvocationCount} {Time.realtimeSinceStartup:F2}s {_resetPipelineStage}";
        }

        public void ResetRuntimeState()
        {
            _scriptedMoveInput = Vector2.zero;
            _scriptedControlActive = false;
        }

        public void NotifyJumpTriggered()
        {
            JumpSoundRequested?.Invoke();
        }

        #endregion
    }
}
