using Portfolio.Input;
using Portfolio.Locomotion.Core;
using System;
using UnityEngine;

namespace Portfolio.Locomotion.Rigidbody
{
    /// <summary>
    /// Handles player-control input selection and pause/freeze mechanics. Public control
    /// methods on PlayerController provide the locomotion entry points.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerHandler : MonoBehaviour
    {
        public static PlayerHandler Instance { get; private set; }
        public static event Action<bool> OnPlayerDisabledStateChanged;
        public static bool IsPlayerDisabled => Instance == null || Instance._state == null || !Instance._state.ControlEnabled || Instance._movementFrozen;

        private bool _movementFrozen;
        private bool _scriptedControlActive;
        private Vector2 _scriptedMoveInput;
        private Vector2 _cachedPausedVelocity;
        private float _cachedPausedGravityScale;

        private LocomotionState _state;
        private LocomotionPhysicsState _lastState;
        private bool _lastDisabledState;

        public event Action<LocomotionPhysicsState> StateTransitioned;
        public event Action<bool> WalkingChanged;
        public event Action<bool> JumpingChanged;
        public event Action<bool> FallingChanged;
        public event Action<bool> RisingChanged;
        public event Action<bool> SlidingChanged;
        public event Action JumpSoundRequested;

        public bool IsMovementFrozen => _movementFrozen;
        public bool IsScriptedControlActive => _scriptedControlActive;
        public Vector2 ScriptedMoveInput => _scriptedMoveInput;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("Multiple PlayerHandler components found; keeping the existing singleton.", this);
                return;
            }
            Instance = this;
        }

        public void Initialize(LocomotionState state)
        {
            if (ReferenceEquals(_state, state))
                return;

            if (_state != null)
                _state.StateChanged -= HandleStateChanged;

            _state = state;
            _lastState = _state != null ? _state.CurrentState : LocomotionPhysicsState.None;
            _lastDisabledState = IsPlayerDisabled;

            if (_state != null)
                _state.StateChanged += HandleStateChanged;
        }

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

            if (!HasState(previousState, LocomotionPhysicsState.Jumping) &&
                HasState(nextState, LocomotionPhysicsState.Jumping))
                JumpSoundRequested?.Invoke();
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
                callback?.Invoke(isActive);
        }

        private static bool HasState(LocomotionPhysicsState state, LocomotionPhysicsState flag)
        {
            return (state & flag) == flag;
        }

        public void BeginScriptedControl()
        {
            _scriptedMoveInput = Vector2.zero;
            _scriptedControlActive = true;
        }

        public void SetScriptedMoveInput(Vector2 input)
        {
            if (_scriptedControlActive)
                _scriptedMoveInput = Vector2.ClampMagnitude(input, 1f);
        }

        public void EndScriptedControl()
        {
            _scriptedMoveInput = Vector2.zero;
            _scriptedControlActive = false;
        }

        public float ResolveMovementInput(
            bool controlEnabled,
            GameInput gameInput)
        {
            if (_movementFrozen)
                return 0f;
            if (_scriptedControlActive)
                return Mathf.Clamp(_scriptedMoveInput.x, -1f, 1f);
            if (!controlEnabled)
                return 0f;
            if (gameInput == null)
                return 0f;

            return Mathf.Clamp(gameInput.MovementValue.x, -1f, 1f);
        }

        public void Freeze(UnityEngine.Rigidbody2D body)
        {
            if (_movementFrozen || body == null)
                return;

            _movementFrozen = true;
            NotifyDisabledStateChanged();
            _cachedPausedVelocity = body.linearVelocity;
            _cachedPausedGravityScale = body.gravityScale;
            body.linearVelocity = Vector2.zero;
            body.gravityScale = 0f;
        }

        public void Unfreeze(UnityEngine.Rigidbody2D body)
        {
            if (!_movementFrozen || body == null)
                return;

            _movementFrozen = false;
            NotifyDisabledStateChanged();
            body.linearVelocity = _cachedPausedVelocity;
            body.gravityScale = _cachedPausedGravityScale;
        }

        public void NotifyJumpTriggered()
        {
            JumpSoundRequested?.Invoke();
        }

        public void NotifyControlStateChanged() => NotifyDisabledStateChanged();

        private void NotifyDisabledStateChanged()
        {
            bool isDisabled = IsPlayerDisabled;
            if (_lastDisabledState == isDisabled) return;
            _lastDisabledState = isDisabled;
            OnPlayerDisabledStateChanged?.Invoke(isDisabled);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_state != null)
                _state.StateChanged -= HandleStateChanged;
        }

        public void ResetRuntimeState()
        {
            _scriptedMoveInput = Vector2.zero;
            _scriptedControlActive = false;
        }
    }
}
