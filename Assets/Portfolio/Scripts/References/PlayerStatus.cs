using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlayerSystem
{
    /// <summary>
    /// Represents the different player statuses.
    /// These are mutually exclusive states.
    /// </summary>
    public enum Status
    {
        Normal,
        Dead,
        Respawning,
        Disabled,
        Resetting
    }

    /// <summary>
    /// Represents the different player modes.
    /// These modes can be combined using bitwise operations.
    /// </summary>
    [Flags]
    public enum Mode
    {
        None = 0,
        Normal = 1 << 0,
        NoClip = 1 << 1,
        NoGravity = 1 << 2,
        BulletTime = 1 << 3,
        Refracted = 1 << 4,
        Held = 1 << 5,
        Melted = 1 << 6,
        Cutscene = 1 << 7,
        FloatingArea = 1 << 8,
        Dialogue = 1 << 9
    }

    /// <summary>
    /// Represents the different physics-related states the player can be in.
    /// These states can be combined using bitwise operations.
    /// </summary>
    [Flags]
    public enum PhysicsState
    {
        None = 0,
        Idle = 1 << 0,                  // Player is idle, not moving
        Moving = 1 << 1,                // Player is moving
        Walking = 1 << 2,               // Player is moving on the ground
        Jumping = 1 << 3,               // Player is in the air after jumping
        FloatingUp = 1 << 4,            // Player is in the air after jumping
        Falling = 1 << 5,               // Player is falling
        Sliding = 1 << 6,               // Player is sliding on a slope
        Pushed = 1 << 7,                // Player is being pushed by an external force
        OnSlope = 1 << 8,               // Player is on a normal slope
        OnSteepSlope = 1 << 9,          // Player is on a steep slope
        Turning = 1 << 10,              // Player is turning
        RefractedMoving = 1 << 11     // Player is moving while refracted
    }


    /// <summary>
    /// Represents the different player equipped items.
    /// These are mutually exclusive states.
    /// </summary>
    public enum EquippedItem
    {
        None,
        MirrorItem,
        Suncatcher
    }


    public static class PlayerStatus
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticStateOnSubsystemRegistration()
        {
            ResetStaticState();
        }

        //------------------------------- Events -------------------------------//

        /// <summary>
        /// Invoked when the player's busy state changes.
        /// </summary>
        public static event Action<bool> OnBusyStateChanged;

        /// <summary>
        /// Invoked when the player's main status changes.
        /// </summary>
        public static event Action<Status> OnStatusChanged;

        /// <summary>
        /// Invoked when the player's mode changes.
        /// </summary>
        public static event Action<Mode> OnModeChanged;

        /// <summary>
        /// Invoked after one or more player modes are removed.
        /// First argument contains the removed mode flags; second argument contains the remaining current mode.
        /// </summary>
        public static event Action<Mode, Mode> OnModeRemoved;

        /// <summary>
        /// Invoked when the player's equipment changes.
        /// </summary>
        public static event Action<EquippedItem> OnEquipmentChanged;

        /// <summary>
        /// Invoked when the player's state changes.
        /// </summary>
        public static event Action<PhysicsState> OnStateChanged;

        //------------------------------- Internal Variables -------------------------------//

        private static Status _status;
        private static Mode _mode;
        private static EquippedItem _item;
        private static PhysicsState _state;
        private static bool _isBusy;
        private static int _statusTransitionCount;
        private static int _stateTransitionCount;
        private static int _modeTransitionCount;
        private static int _blockedMutationCount;
        private static readonly List<string> _recentDebugEvents = new();
        private const int MaxRecentDebugEvents = 12;

        //------------------------------- Public Properties -------------------------------//

        /// <summary>
        /// Gets the player's current status.
        /// </summary>
        public static Status CurrentStatus => _status;

        /// <summary>
        /// Gets the player's current mode.
        /// </summary>
        public static Mode CurrentMode => _mode;

        /// <summary>
        /// Gets the player's current equipped item.
        /// </summary>
        public static EquippedItem CurrentEquippedItem => _item;

        /// <summary>
        /// Gets the player's current state.
        /// </summary>
        public static PhysicsState CurrentState => _state;

        /// <summary>
        /// Gets or sets whether the player is busy.
        /// </summary>
        public static int StatusTransitionCount => _statusTransitionCount;
        public static int StateTransitionCount => _stateTransitionCount;
        public static int ModeTransitionCount => _modeTransitionCount;
        public static int BlockedMutationCount => _blockedMutationCount;
        public static string RecentDebugEvents => string.Join("\n", _recentDebugEvents);

        public static bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (ShouldBlockStateMutation("IsBusy"))
                {
                    return;
                }

                if (_isBusy == value) return;
                bool previousBusy = _isBusy;
                _isBusy = value;
                RecordDebugEvent($"Busy {previousBusy} -> {_isBusy}");
                OnBusyStateChanged?.Invoke(_isBusy);
            }
        }

        // Status Helper Properties

        //------------------------------- Status Flags -------------------------------//

        public static bool IsNormal => _status == Status.Normal;
        public static bool IsResetting => _status == Status.Resetting;
        public static bool IsDead => _status == Status.Dead;
        public static bool IsRespawning => _status == Status.Respawning;
        public static bool IsDisabled => _status == Status.Disabled;

        //------------------------------- Physics State Flags -------------------------------//

        public static bool IsIdle => (_state & PhysicsState.Idle) == PhysicsState.Idle;
        public static bool IsMoving => (_state & PhysicsState.Moving) == PhysicsState.Moving;
        public static bool IsWalking => (_state & PhysicsState.Walking) == PhysicsState.Walking;
        public static bool IsJumping => (_state & PhysicsState.Jumping) == PhysicsState.Jumping;
        public static bool IsFloatingUp => (_state & PhysicsState.FloatingUp) == PhysicsState.FloatingUp;
        public static bool IsFalling => (_state & PhysicsState.Falling) == PhysicsState.Falling;
        public static bool IsSliding => (_state & PhysicsState.Sliding) == PhysicsState.Sliding;
        public static bool IsPushed => (_state & PhysicsState.Pushed) == PhysicsState.Pushed;
        public static bool IsOnSlope => (_state & PhysicsState.OnSlope) == PhysicsState.OnSlope;
        public static bool IsOnSteepSlope => (_state & PhysicsState.OnSteepSlope) == PhysicsState.OnSteepSlope;
        public static bool IsRefractedMoving => (_state & PhysicsState.RefractedMoving) == PhysicsState.RefractedMoving;

        public static bool IsTurning => (_state & PhysicsState.Turning) == PhysicsState.Turning;

        //------------------------------- Mode Flags -------------------------------//

        public static bool IsHeld => (_mode & Mode.Held) == Mode.Held;
        public static bool IsMelted => (_mode & Mode.Melted) == Mode.Melted;
        public static bool IsRefracted => (_mode & Mode.Refracted) == Mode.Refracted;
        public static bool IsCutscene => (_mode & Mode.Cutscene) == Mode.Cutscene;
        public static bool IsDialogue => (_mode & Mode.Dialogue) == Mode.Dialogue;

        //------------------------------- Equipped Items Flags -------------------------------//

        public static bool IsNoItem => _item == EquippedItem.None;
        public static bool IsMirrorItem => _item == EquippedItem.MirrorItem;
        public static bool IsSuncatcher => _item == EquippedItem.Suncatcher;


        //------------------------------- Public Methods -------------------------------//

        /// <summary>
        /// Sets the player's current status and invokes the status change event.
        /// </summary>
        public static void SetStatus(Status nextStatus)
        {
            if (ShouldBlockStatusTransition(nextStatus))
            {
                return;
            }

            if (_status == nextStatus) return;
            Status previousStatus = _status;
            _status = nextStatus;
            _statusTransitionCount++;
            RecordDebugEvent($"Status {previousStatus} -> {nextStatus}");
            OnStatusChanged?.Invoke(_status);
        }

        /// <summary>
        /// Enables a specific player mode (allows multiple active modes).
        /// </summary>
        public static void AddMode(Mode mode)
        {
            if ((_mode & mode) == mode) return; // Already set

            if (ShouldBlockRuntimeMutation($"AddMode({mode})", allowWhileCutscene: mode == Mode.Dialogue))
            {
                return;
            }

            Mode previousMode = _mode;
            _mode |= mode;
            _modeTransitionCount++;
            RecordDebugEvent($"AddMode {mode}: {previousMode} -> {_mode}");
            OnModeChanged?.Invoke(_mode);
        }

        /// <summary>
        /// Disables a specific player mode.
        /// </summary>
        public static void RemoveMode(Mode mode)
        {
            if ((_mode & mode) == 0) return; // Already removed

            if (ShouldBlockRuntimeMutation($"RemoveMode({mode})", allowWhileCutscene: true))
            {
                return;
            }

            // TODO: this is returns the remaining modes, not the removed mode. it should be `= previousMode & mode` instead.
            Mode previousMode = _mode;
            _mode &= ~mode;
            Mode removedMode = previousMode & mode;
            
            _modeTransitionCount++;
            RecordDebugEvent($"RemoveMode {mode}: {previousMode} -> {_mode}");
            OnModeRemoved?.Invoke(removedMode, _mode);
            OnModeChanged?.Invoke(_mode);
        }

        /// <summary>
        /// Checks if a specific mode is active.
        /// </summary>
        public static bool HasMode(Mode mode) => (_mode & mode) == mode;

        /// <summary>
        /// Clears all player modes.
        /// </summary>
        public static void ClearModes()
        {
            if (_mode == Mode.None) return;

            if (ShouldBlockRuntimeMutation("ClearModes", allowWhileCutscene: true))
            {
                return;
            }

            Mode previousMode = _mode;
            _mode = Mode.None;
            _mode &= Mode.None;
            _modeTransitionCount++;
            RecordDebugEvent($"ClearModes: {previousMode} -> {_mode}");
            OnModeRemoved?.Invoke(previousMode, _mode);
            OnModeChanged?.Invoke(_mode);
        }

        /// <summary>
        /// Enables a specific player state (allows multiple active states).
        /// </summary>
        public static void AddState(PhysicsState state)
        {
            if (ShouldBlockStateMutation($"AddState({state})"))
            {
                return;
            }

            if ((_state & state) == state) return; // Already set
            PhysicsState previousState = _state;
            _state |= state;
            _stateTransitionCount++;
            RecordDebugEvent($"AddState {state}: {previousState} -> {_state}");
            OnStateChanged?.Invoke(_state);
            Debug.Log($"[PlayerStatus] State change 'AddState({state})': {previousState} -> {_state}");
        }

        /// <summary>
        /// Disables a specific player state.
        /// </summary>
        public static void RemoveState(PhysicsState state)
        {
            if (ShouldBlockStateMutation($"RemoveState({state})"))
            {
                return;
            }

            if ((_state & state) == 0) return; // Already removed
            PhysicsState previousState = _state;
            _state &= ~state;
            _stateTransitionCount++;
            RecordDebugEvent($"RemoveState {state}: {previousState} -> {_state}");
            OnStateChanged?.Invoke(_state);
        }

        /// <summary>
        /// Checks if a specific state is active.
        /// </summary>
        public static bool HasState(PhysicsState state) => (_state & state) == state;

        /// <summary>
        /// Checks if a specific state is active and optionally outputs associated data.
        /// </summary>
        public static bool HasState(PhysicsState state, out MirrorObjectBase mirrorObject)
        {
            bool isActive = (_state & state) == state;
            mirrorObject = null;

            if (isActive && state == PhysicsState.RefractedMoving)
            {
                if (PlayerHandler.instance != null)
                    mirrorObject = PlayerHandler.instance.currentRefractingMirrorObject;
            }

            return isActive;
        }

        /// <summary>
        /// Clears all player states.
        /// </summary>
        public static void ClearStates()
        {
            if (ShouldBlockStateMutation("ClearStates"))
            {
                return;
            }

            PhysicsState previousState = _state;
            _state = PhysicsState.None;
            _stateTransitionCount++;
            RecordDebugEvent($"ClearStates: {previousState} -> {_state}");
            OnStateChanged?.Invoke(_state);
        }

        /// <summary>
        /// Clears all player states then set state to Idle.
        /// </summary>
        public static void ResetToIdleState()
        {
            if (ShouldBlockStateMutation("ResetToIdleState"))
            {
                return;
            }

            PhysicsState previousState = _state;
            _state = PhysicsState.None;
            _state = PhysicsState.Idle;
            _stateTransitionCount++;
            RecordDebugEvent($"ResetToIdleState: {previousState} -> {_state}");
            OnStateChanged?.Invoke(_state);
        }

        /// <summary>
        /// Sets whether the player is busy and invokes the event if changed.
        /// </summary>
        public static void SetBusy(bool value)
        {
            if (ShouldBlockStateMutation($"SetBusy({value})"))
            {
                return;
            }

            if (_isBusy == value) return;
            bool previousBusy = _isBusy;
            _isBusy = value;
            RecordDebugEvent($"Busy {previousBusy} -> {_isBusy}");
            OnBusyStateChanged?.Invoke(_isBusy);
        }

        public static void ForceSetBusy(bool value)
        {
            if (_isBusy == value) return;
            bool previousBusy = _isBusy;
            _isBusy = value;
            RecordDebugEvent($"Busy {previousBusy} -> {_isBusy}");
            OnBusyStateChanged?.Invoke(_isBusy);
        }

        public static void ForceSetStatus(Status nextStatus)
        {
            if (_status == nextStatus) return;
            Status previousStatus = _status;
            _status = nextStatus;
            _statusTransitionCount++;
            RecordDebugEvent($"Status {previousStatus} -> {nextStatus}");
            OnStatusChanged?.Invoke(_status);
        }

        //------------------------------- Events -------------------------------//

        public static void HandlePhysicsState(PhysicsState state, bool isActive)
        {
            if (isActive) { AddState(state); }
            else { RemoveState(state); }
        }

        private static bool ShouldBlockStateMutation(string operation)
        {
            if (IsDisabled)
            {
                RecordBlockedMutation(operation, "player status is Disabled");
                return true;
            }

            if (IsResetting)
            {
                RecordBlockedMutation(operation, "player status is Resetting");
                return true;
            }

            if (HasMode(Mode.Cutscene))
            {
                RecordBlockedMutation(operation, "player is in Cutscene mode");
                return true;
            }

            return false;
        }

        private static bool ShouldBlockStatusTransition(Status nextStatus)
        {
            if (nextStatus == Status.Dead)
            {
                return false;
            }


            if (IsDisabled)
            {
                // Disabled has highest precedence.
                // The only non-force transition allowed while disabled is explicitly leaving Disabled.
                if (nextStatus != Status.Disabled)
                {
                    return false;
                }

                RecordBlockedMutation($"SetStatus({nextStatus})", "player status is Disabled");
                return true;
            }

            return ShouldBlockStateMutation($"SetStatus({nextStatus})");
        }

        private static bool ShouldBlockRuntimeMutation(string operation, bool allowWhileCutscene = false)
        {
            if (IsDisabled)
            {
                RecordBlockedMutation(operation, "player status is Disabled");
                return true;
            }

            if (IsResetting)
            {
                RecordBlockedMutation(operation, "player status is Resetting");
                return true;
            }

            if (!allowWhileCutscene && HasMode(Mode.Cutscene))
            {
                RecordBlockedMutation(operation, "player is in Cutscene mode");
                return true;
            }

            return false;
        }


        private static void RecordBlockedMutation(string operation, string reason)
        {
            _blockedMutationCount++;
            string message = $"[PlayerStatus] State change '{operation}' ignored because {reason}.";
            RecordDebugEvent($"BLOCKED {operation}: {reason}");
            Debug.LogWarning(message);
        }

        private static void RecordDebugEvent(string message)
        {
            _recentDebugEvents.Add($"{Time.realtimeSinceStartup:F2}s {message}");
            if (_recentDebugEvents.Count > MaxRecentDebugEvents)
            {
                _recentDebugEvents.RemoveAt(0);
            }
        }

        public static void ClearAllEvents()
        {
            OnBusyStateChanged = null;
            OnStatusChanged = null;
            OnModeChanged = null;
            OnModeRemoved = null;
            OnEquipmentChanged = null;
            OnStateChanged = null;
        }

        public static void ResetRuntimeState()
        {
            _mode = Mode.None;
            _item = EquippedItem.None;
            _state = PhysicsState.Idle;
            _isBusy = false;
            _modeTransitionCount++;
            _stateTransitionCount++;
            RecordDebugEvent("ResetRuntimeState: mode=None item=None state=Idle busy=False");

            OnModeChanged?.Invoke(_mode);
            OnEquipmentChanged?.Invoke(_item);
            OnStateChanged?.Invoke(_state);
            OnBusyStateChanged?.Invoke(_isBusy);
        }

        public static void ResetStaticState()
        {
            ClearAllEvents();
            _status = Status.Normal;
            _mode = Mode.None;
            _item = EquippedItem.None;
            _state = PhysicsState.Idle;
            _isBusy = false;
            _statusTransitionCount = 0;
            _stateTransitionCount = 0;
            _modeTransitionCount = 0;
            _blockedMutationCount = 0;
            _recentDebugEvents.Clear();
        }
    }

}
