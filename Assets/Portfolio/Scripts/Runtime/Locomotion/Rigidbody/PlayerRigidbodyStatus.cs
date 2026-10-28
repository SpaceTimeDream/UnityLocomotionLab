using System;
using System.Collections.Generic;
using UnityEngine;

namespace Portfolio.Locomotion.Rigidbody
{
    public enum Status
    {
        Normal,
        Dead,
        Respawning,
        Disabled,
        Resetting
    }

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

    [Flags]
    public enum PhysicsState
    {
        None = 0,
        Idle = 1 << 0,
        Moving = 1 << 1,
        Walking = 1 << 2,
        Jumping = 1 << 3,
        FloatingUp = 1 << 4,
        Falling = 1 << 5,
        Sliding = 1 << 6,
        Pushed = 1 << 7,
        OnSlope = 1 << 8,
        OnSteepSlope = 1 << 9,
        Turning = 1 << 10,
        RefractedMoving = 1 << 11
    }

    public enum EquippedItem
    {
        None,
        MirrorItem,
        Suncatcher
    }

    /// <summary>
    /// Global player status and mode flags. Physics measurements and detailed locomotion
    /// transitions remain owned by LocomotionState.
    /// </summary>
    public static class PlayerRigidbodyStatus
    {
        //------------------------------- Parameters -------------------------------//

        #region State

        public static event Action<bool> OnBusyStateChanged;
        public static event Action<Status> OnStatusChanged;
        public static event Action<Mode> OnModeChanged;
        public static event Action<Mode, Mode> OnModeRemoved;
        public static event Action<EquippedItem> OnEquipmentChanged;
        public static event Action<PhysicsState> OnStateChanged;

        private static Status _status = Status.Normal;
        private static Mode _mode = Mode.Normal;
        private static EquippedItem _item = EquippedItem.None;
        private static PhysicsState _state = PhysicsState.Idle;
        private static bool _isBusy;
        private static int _statusTransitionCount;
        private static int _stateTransitionCount;
        private static int _modeTransitionCount;
        private static int _blockedMutationCount;
        private static readonly List<string> _recentEvents = new List<string>();
        private const int MAXIMUM_RECENT_EVENTS = 12;

        public static Status CurrentStatus => _status;
        public static Mode CurrentMode => _mode;
        public static EquippedItem CurrentEquippedItem => _item;
        public static PhysicsState CurrentState => _state;
        public static int StatusTransitionCount => _statusTransitionCount;
        public static int StateTransitionCount => _stateTransitionCount;
        public static int ModeTransitionCount => _modeTransitionCount;
        public static int BlockedMutationCount => _blockedMutationCount;
        public static string RecentDebugEvents => string.Join("\n", _recentEvents);

        public static bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (IsTerminalStatus(_status))
                {
                    _blockedMutationCount++;
                    return;
                }
                if (_isBusy == value)
            {
                return;
            }
                bool previous = _isBusy;
                _isBusy = value;
                RecordEvent($"Busy {previous} -> {_isBusy}");
                OnBusyStateChanged?.Invoke(_isBusy);
            }
        }

        public static bool IsNormal => _status == Status.Normal;
        public static bool IsResetting => _status == Status.Resetting;
        public static bool IsDead => _status == Status.Dead;
        public static bool IsRespawning => _status == Status.Respawning;
        public static bool IsDisabled => _status == Status.Disabled;
        public static bool IsIdle => HasState(PhysicsState.Idle);
        public static bool IsMoving => HasState(PhysicsState.Moving);
        public static bool IsWalking => HasState(PhysicsState.Walking);
        public static bool IsJumping => HasState(PhysicsState.Jumping);
        public static bool IsFloatingUp => HasState(PhysicsState.FloatingUp);
        public static bool IsFalling => HasState(PhysicsState.Falling);
        public static bool IsSliding => HasState(PhysicsState.Sliding);
        public static bool IsPushed => HasState(PhysicsState.Pushed);
        public static bool IsOnSlope => HasState(PhysicsState.OnSlope);
        public static bool IsOnSteepSlope => HasState(PhysicsState.OnSteepSlope);
        public static bool IsTurning => HasState(PhysicsState.Turning);
        public static bool IsRefractedMoving => HasState(PhysicsState.RefractedMoving);
        public static bool IsHeld => HasMode(Mode.Held);
        public static bool IsMelted => HasMode(Mode.Melted);
        public static bool IsRefracted => HasMode(Mode.Refracted);
        public static bool IsCutscene => HasMode(Mode.Cutscene);
        public static bool IsDialogue => HasMode(Mode.Dialogue);

        #endregion

        //------------------------------- Initialization Methods -------------------------------//

        #region Initialization

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticStateOnSubsystemRegistration() => ResetStaticState();

        #endregion

        //------------------------------- Core / Routines Methods -------------------------------//

        #region State Methods

        public static bool HasState(PhysicsState flag) =>
            flag != PhysicsState.None && (_state & flag) == flag;

        public static bool HasMode(Mode flag) =>
            flag != Mode.None && (_mode & flag) == flag;

        public static bool SetStatus(Status next)
        {
            if (_status == next)
            {
                return true;
            }
            if (_status == Status.Dead && next != Status.Respawning && next != Status.Resetting)
            {
                _blockedMutationCount++;
                return false;
            }

            Status previous = _status;
            _status = next;
            _statusTransitionCount++;
            RecordEvent($"Status {previous} -> {next}");
            OnStatusChanged?.Invoke(next);
            return true;
        }

        public static bool SetMode(Mode mode)
        {
            if (IsTerminalStatus(_status))
            {
                _blockedMutationCount++;
                return false;
            }
            if (_mode == mode)
            {
                return true;
            }

            _mode = mode;
            _modeTransitionCount++;
            RecordEvent($"Mode -> {_mode}");
            OnModeChanged?.Invoke(_mode);
            return true;
        }

        public static bool AddMode(Mode mode) => SetMode(_mode | mode);

        public static bool RemoveMode(Mode mode)
        {
            Mode removed = _mode & mode;
            if (removed == Mode.None)
            {
                return true;
            }
            if (!SetMode(_mode & ~mode)) return false;
            OnModeRemoved?.Invoke(removed, _mode);
            return true;
        }

        /// <summary>
        /// Accepts physics-state changes forwarded by PlayerRigidbodyHandler from PlayerRigidbodyPhysics.
        /// Other components should not mutate the global physics-state flag directly.
        /// </summary>
        public static void HandlePhysicsState(PhysicsState state)
        {
            if (IsTerminalStatus(_status))
            {
                _blockedMutationCount++;
                return;
            }
            if (_state == state)
            {
                return;
            }
            _state = state;
            _stateTransitionCount++;
            RecordEvent($"PhysicsState -> {_state}");
            OnStateChanged?.Invoke(_state);
        }

        public static void SetEquippedItem(EquippedItem item)
        {
            if (IsTerminalStatus(_status))
            {
                _blockedMutationCount++;
                return;
            }
            if (_item == item)
            {
                return;
            }
            _item = item;
            OnEquipmentChanged?.Invoke(_item);
        }

        public static void ResetStaticState()
        {
            _status = Status.Normal;
            _mode = Mode.Normal;
            _item = EquippedItem.None;
            _state = PhysicsState.Idle;
            _isBusy = false;
            _statusTransitionCount = 0;
            _stateTransitionCount = 0;
            _modeTransitionCount = 0;
            _blockedMutationCount = 0;
            _recentEvents.Clear();
        }

        #endregion

        //------------------------------- Helpers -------------------------------//

        #region Helpers

        private static bool IsTerminalStatus(Status status) =>
            status == Status.Dead || status == Status.Resetting;

        private static void RecordEvent(string message)
        {
            _recentEvents.Add($"{Time.frameCount}: {message}");
            if (_recentEvents.Count > MAXIMUM_RECENT_EVENTS)
            {
                _recentEvents.RemoveAt(0);
            }
        }

        #endregion
    }
}