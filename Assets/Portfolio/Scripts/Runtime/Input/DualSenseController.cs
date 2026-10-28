using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace Portfolio.Input
{
    public enum DualSenseTriggerEffectMode
    {
        Off,
        ContinuousResistance,
        SectionResistance,
        Vibration,
        Calibration
    }

    public enum DualSenseMicLedState
    {
        Off,
        On,
        Pulsating
    }

    public enum DualSensePlayerLedBrightness
    {
        High,
        Medium,
        Low
    }

    [Serializable]
    public struct DualSenseTriggerEffect
    {
        [SerializeField] private DualSenseTriggerEffectMode _mode;
        [SerializeField, Range(0, 255)] private int _startPosition;
        [SerializeField, Range(0, 255)] private int _endPosition;
        [SerializeField, Range(0, 8)] private int _force;
        [SerializeField, Range(0, 8)] private int _beginForce;
        [SerializeField, Range(0, 8)] private int _middleForce;
        [SerializeField, Range(0, 8)] private int _endForce;
        [SerializeField, Range(0, 255)] private int _frequency;
        [SerializeField] private bool _keepEffect;

        public DualSenseTriggerEffectMode Mode => _mode;

        public static DualSenseTriggerEffect Off()
        {
            return new DualSenseTriggerEffect { _mode = DualSenseTriggerEffectMode.Off };
        }

        public static DualSenseTriggerEffect ContinuousResistance(int startPosition, int force)
        {
            return new DualSenseTriggerEffect
            {
                _mode = DualSenseTriggerEffectMode.ContinuousResistance,
                _startPosition = Mathf.Clamp(startPosition, 0, 9),
                _force = Mathf.Clamp(force, 0, 8)
            };
        }

        public static DualSenseTriggerEffect SectionResistance(int startPosition, int endPosition, int force)
        {
            return new DualSenseTriggerEffect
            {
                _mode = DualSenseTriggerEffectMode.SectionResistance,
                _startPosition = Mathf.Clamp(startPosition, 0, 9),
                _endPosition = Mathf.Clamp(endPosition, 0, 9),
                _force = Mathf.Clamp(force, 0, 8)
            };
        }

        public static DualSenseTriggerEffect Vibration(
            int startPosition,
            int beginForce,
            int middleForce,
            int endForce,
            int frequency,
            bool keepEffect = false)
        {
            return new DualSenseTriggerEffect
            {
                _mode = DualSenseTriggerEffectMode.Vibration,
                _startPosition = Mathf.Clamp(startPosition, 0, 255),
                _beginForce = Mathf.Clamp(beginForce, 0, 8),
                _middleForce = Mathf.Clamp(middleForce, 0, 8),
                _endForce = Mathf.Clamp(endForce, 0, 8),
                _frequency = Mathf.Clamp(frequency, 0, 255),
                _keepEffect = keepEffect
            };
        }

        public static DualSenseTriggerEffect Calibrate()
        {
            return new DualSenseTriggerEffect { _mode = DualSenseTriggerEffectMode.Calibration };
        }

        internal byte GetModeCode()
        {
            switch (_mode)
            {
                case DualSenseTriggerEffectMode.ContinuousResistance:
                    return 0x01;
                case DualSenseTriggerEffectMode.SectionResistance:
                    return 0x02;
                case DualSenseTriggerEffectMode.Vibration:
                    return 0x26;
                case DualSenseTriggerEffectMode.Calibration:
                    return 0xFC;
                default:
                    return 0x00;
            }
        }

        internal byte GetParameter(int index)
        {
            switch (_mode)
            {
                case DualSenseTriggerEffectMode.ContinuousResistance:
                    if (index == 0) return (byte)Mathf.Clamp(_startPosition, 0, 9);
                    if (index == 1) return (byte)Mathf.Clamp(_force, 0, 8);
                    break;

                case DualSenseTriggerEffectMode.SectionResistance:
                    if (index == 0) return (byte)Mathf.Clamp(_startPosition, 0, 9);
                    if (index == 1) return (byte)Mathf.Clamp(_endPosition, 0, 9);
                    if (index == 2) return (byte)Mathf.Clamp(_force, 0, 8);
                    break;

                case DualSenseTriggerEffectMode.Vibration:
                    if (index == 0) return (byte)(255 - Mathf.Clamp(_startPosition, 0, 255));
                    if (index == 1) return _keepEffect ? (byte)0x02 : (byte)0x00;
                    if (index == 3) return (byte)Mathf.Clamp(_beginForce, 0, 8);
                    if (index == 4) return (byte)Mathf.Clamp(_middleForce, 0, 8);
                    if (index == 5) return (byte)Mathf.Clamp(_endForce, 0, 8);
                    if (index == 8) return (byte)Mathf.Clamp(_frequency, 0, 255);
                    break;
            }

            return 0;
        }
    }

    public class DualSenseController : MonoBehaviour
    {
        //------------------------------- Parameters -------------------------------//

        #region Components

        [Header("CONTROLLER")]
        [SerializeField, Min(0)] private int _controllerIndex;
        [SerializeField] private bool _detectControllerAutomatically = true;
        private Gamepad _controllerOverride;

        [Header("DEBUG FALLBACK")]
        [SerializeField] private bool _debugAllowGenericGamepadFallback;

        [Header("CONNECTION FEEDBACK")]
        [SerializeField] private bool _applyLightBarColorOnConnect;
        [SerializeField] private Color _lightBarColor = new Color(0.12f, 0.38f, 1f, 1f);
        [SerializeField] private bool _applyPlayerLedsOnConnect;
        [SerializeField, Range(0, 31)] private int _playerLedMask = 1;
        [SerializeField]
        private DualSensePlayerLedBrightness _playerLedBrightness =
            DualSensePlayerLedBrightness.High;

        [Header("ADAPTIVE TRIGGERS")]
        [SerializeField] private bool _applyTriggerEffectsOnConnect;
        [SerializeField] private DualSenseTriggerEffect _leftTriggerEffect =
            DualSenseTriggerEffect.ContinuousResistance(0, 4);
        [SerializeField] private DualSenseTriggerEffect _rightTriggerEffect =
            DualSenseTriggerEffect.ContinuousResistance(0, 4);

        private Gamepad _controller;
        private Coroutine _debugRumbleCoroutine;
        private bool _initialized;
        private bool _warnedAboutAdvancedOutput;
        private readonly List<Gamepad> _deviceCandidates = new List<Gamepad>();

#if UNITY_EDITOR
        private bool _hasValidatedOutputSettings;
        private Color _validatedLightBarColor;
        private int _validatedPlayerLedMask;
        private DualSensePlayerLedBrightness _validatedPlayerLedBrightness;
        private DualSenseTriggerEffect _validatedLeftTriggerEffect;
        private DualSenseTriggerEffect _validatedRightTriggerEffect;
        private bool _validatedApplyLightBarColorOnConnect;
        private bool _validatedApplyPlayerLedsOnConnect;
        private bool _validatedApplyTriggerEffectsOnConnect;
#endif

        public event Action<bool> ConnectionChanged;
        public event Action<Gamepad> ControllerChanged;

        #endregion

        #region Properties

        public Gamepad CurrentGamepad => _controller;
        public DualSenseGamepadHID HidGamepad => _controller as DualSenseGamepadHID;
        public bool IsConnected => _controller != null && _controller.added && _controller.enabled;
        public string DeviceName => IsConnected ? _controller.displayName : "Disconnected";
        public Vector2 LeftStick => IsConnected ? _controller.leftStick.ReadValue() : Vector2.zero;
        public Vector2 RightStick => IsConnected ? _controller.rightStick.ReadValue() : Vector2.zero;
        public Vector2 Dpad => IsConnected ? _controller.dpad.ReadValue() : Vector2.zero;
        public float LeftTrigger => IsConnected ? _controller.leftTrigger.ReadValue() : 0f;
        public float RightTrigger => IsConnected ? _controller.rightTrigger.ReadValue() : 0f;
        public bool CrossPressed => IsConnected && _controller.buttonSouth.isPressed;
        public bool CirclePressed => IsConnected && _controller.buttonEast.isPressed;
        public bool SquarePressed => IsConnected && _controller.buttonWest.isPressed;
        public bool TrianglePressed => IsConnected && _controller.buttonNorth.isPressed;
        public bool OptionsPressed => IsConnected && _controller.startButton.isPressed;
        public bool CreatePressed => IsConnected && _controller.selectButton.isPressed;
        public bool PlayStationButtonPressed => ReadButtonControl("playStationButton", "systemButton");
        public bool TouchpadPressed => ReadButtonControl("touchpadButton");
        public bool MicMuteButtonPressed => ReadButtonControl("micMuteButton");
        public bool IsBatteryCharging => ReadButtonControl("batteryCharging", "battery/charging");
        public bool IsBatteryFullyCharged => ReadButtonControl("batteryFullyCharged", "battery/fullyCharged");
        public bool HasTouchpadPosition =>
            FindControl("touch0/position", "touch1/position", "touchpad/position", "touchpadPosition") != null;
        public bool HasGyroscope => FindControl("gyro", "angularVelocity") != null;
        public bool HasAccelerometer => FindControl("accel", "accelerometer") != null;
        public bool HasBatteryLevel => FindControl("batteryLevel", "battery/level") != null;
        public bool IsDualSenseDevice => IsConnected && IsRecognizedDualSense(_controller);
        public bool IsUsingGenericGamepadFallback =>
            IsConnected && _debugAllowGenericGamepadFallback && !IsRecognizedDualSense(_controller);
        public bool SupportsAdaptiveTriggers => IsDualSenseDevice;

        #endregion

        //------------------------------- Unity Methods -------------------------------//

        #region Unity

        private void Start()
        {
            Initialize();
        }

        private void OnEnable()
        {
            if (_initialized)
            {
                InitializeEvents();
                RefreshController();
            }
        }

        private void OnDisable()
        {
            UnsubscribeEvents();

            if (_debugRumbleCoroutine != null)
            {
                StopCoroutine(_debugRumbleCoroutine);
                _debugRumbleCoroutine = null;
            }

            SetController(null);
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                StopFeedback();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _controllerIndex = Mathf.Max(0, _controllerIndex);
            _playerLedMask = Mathf.Clamp(_playerLedMask, 0, 31);

            if (Application.isPlaying && _initialized)
            {
                RefreshController();
                ApplyInspectorChanges();
            }
        }

        private void ApplyInspectorChanges()
        {
            if (!_hasValidatedOutputSettings || !IsConnected || !IsDualSenseDevice)
            {
                CacheValidatedOutputSettings();
                return;
            }

            bool lightBarChanged = _lightBarColor != _validatedLightBarColor ||
                (!_validatedApplyLightBarColorOnConnect && _applyLightBarColorOnConnect);
            bool playerLedsChanged = _playerLedMask != _validatedPlayerLedMask ||
                _playerLedBrightness != _validatedPlayerLedBrightness ||
                (!_validatedApplyPlayerLedsOnConnect && _applyPlayerLedsOnConnect);
            bool triggerToggleEnabled =
                !_validatedApplyTriggerEffectsOnConnect && _applyTriggerEffectsOnConnect;
            bool leftTriggerChanged =
                !_validatedLeftTriggerEffect.Equals(_leftTriggerEffect) || triggerToggleEnabled;
            bool rightTriggerChanged =
                !_validatedRightTriggerEffect.Equals(_rightTriggerEffect) || triggerToggleEnabled;

            DualSenseHIDOutputReport command = DualSenseHIDOutputReport.Create();
            bool hasOutput = false;

            if (lightBarChanged)
            {
                command.SetLightBarColor(_lightBarColor);
                hasOutput = true;
            }

            if (playerLedsChanged)
            {
                command.SetPlayerLedState((byte)_playerLedMask);
                command.SetPlayerLedBrightness(_playerLedBrightness);
                hasOutput = true;
            }

            if (leftTriggerChanged)
            {
                command.SetTriggerState(true, _leftTriggerEffect);
                hasOutput = true;
            }

            if (rightTriggerChanged)
            {
                command.SetTriggerState(false, _rightTriggerEffect);
                hasOutput = true;
            }

            if (hasOutput)
            {
                ExecuteDualSenseOutput(ref command);
            }

            CacheValidatedOutputSettings();
        }

        private void CacheValidatedOutputSettings()
        {
            _validatedLightBarColor = _lightBarColor;
            _validatedPlayerLedMask = _playerLedMask;
            _validatedPlayerLedBrightness = _playerLedBrightness;
            _validatedLeftTriggerEffect = _leftTriggerEffect;
            _validatedRightTriggerEffect = _rightTriggerEffect;
            _validatedApplyLightBarColorOnConnect = _applyLightBarColorOnConnect;
            _validatedApplyPlayerLedsOnConnect = _applyPlayerLedsOnConnect;
            _validatedApplyTriggerEffectsOnConnect = _applyTriggerEffectsOnConnect;
            _hasValidatedOutputSettings = true;
        }
#endif

        #endregion

        //------------------------------- Controller Selection Methods -------------------------------//

        #region Controller Selection

        public void SetControllerOverride(Gamepad controller)
        {
            _controllerOverride = controller;
            RefreshController();
        }

        #endregion

        //------------------------------- Input Methods -------------------------------//

        #region Input

        public bool TryGetTouchpadPosition(out Vector2 position)
        {
            return TryGetTouchpadPosition(0, out position);
        }

        public bool TryGetTouchpadPosition(int touchIndex, out Vector2 position)
        {
            position = Vector2.zero;

            if (touchIndex < 0 || touchIndex > 1)
            {
                return false;
            }

            string touchPath = "touch" + touchIndex + "/position";

            if (TryReadVector2(out position, touchPath))
            {
                return true;
            }

            if (touchIndex == 0)
            {
                return TryReadVector2(out position, "touchpad/position", "touchpadPosition");
            }

            return TryReadVector2(out position, "touchpad/touch1/position", "touch1Position");
        }

        public bool TryGetGyroscope(out Vector3 angularVelocity)
        {
            return TryReadVector3(out angularVelocity, "gyro", "angularVelocity");
        }

        public bool TryGetAccelerometer(out Vector3 acceleration)
        {
            return TryReadVector3(out acceleration, "accel", "accelerometer");
        }

        public bool TryGetBatteryLevel(out float normalizedLevel)
        {
            normalizedLevel = 0f;
            InputControl control = FindControl("batteryLevel", "battery/level");

            if (control == null)
            {
                return false;
            }

            object value = control.ReadValueAsObject();

            if (value is float floatValue)
            {
                normalizedLevel = floatValue > 1f ? Mathf.Clamp01(floatValue / 100f) : Mathf.Clamp01(floatValue);
                return true;
            }

            if (value is int intValue)
            {
                normalizedLevel = Mathf.Clamp01(intValue > 1 ? intValue / 100f : intValue);
                return true;
            }

            return false;
        }

        private bool TryReadVector2(out Vector2 value, params string[] paths)
        {
            value = Vector2.zero;
            InputControl control = FindControl(paths);

            if (control == null)
            {
                return false;
            }

            object rawValue = control.ReadValueAsObject();

            if (!(rawValue is Vector2 vector))
            {
                return false;
            }

            value = vector;
            return true;
        }

        private bool TryReadVector3(out Vector3 value, params string[] paths)
        {
            value = Vector3.zero;
            InputControl control = FindControl(paths);

            if (control == null)
            {
                return false;
            }

            object rawValue = control.ReadValueAsObject();

            if (!(rawValue is Vector3 vector))
            {
                return false;
            }

            value = vector;
            return true;
        }

        private bool ReadButtonControl(params string[] paths)
        {
            InputControl control = FindControl(paths);

            if (control == null)
            {
                return false;
            }

            if (control is ButtonControl button)
            {
                return button.isPressed;
            }

            object value = control.ReadValueAsObject();
            return value is float floatValue && floatValue > 0.5f;
        }

        private InputControl FindControl(params string[] paths)
        {
            if (!IsConnected || paths == null)
            {
                return null;
            }

            foreach (InputControl control in _controller.allControls)
            {
                for (int i = 0; i < paths.Length; i++)
                {
                    string controlPath = control.path;
                    string requestedPath = paths[i];
                    bool pathMatches = string.Equals(
                        controlPath,
                        requestedPath,
                        StringComparison.OrdinalIgnoreCase) ||
                        controlPath.EndsWith("/" + requestedPath, StringComparison.OrdinalIgnoreCase);
                    bool nameMatches = string.Equals(
                        control.name,
                        requestedPath,
                        StringComparison.OrdinalIgnoreCase);

                    if (pathMatches || nameMatches)
                    {
                        return control;
                    }
                }
            }

            return null;
        }

        #endregion

        //------------------------------- Output Methods -------------------------------//

        #region Output

        public void DebugTestRumble()
        {
            if (_debugRumbleCoroutine != null)
            {
                StopCoroutine(_debugRumbleCoroutine);
                _debugRumbleCoroutine = null;
                StopRumble();
            }

            if (SetMotorSpeeds(0.35f, 0.8f))
            {
                _debugRumbleCoroutine = StartCoroutine(StopDebugRumbleAfterDelay());
            }
        }

        

        public bool SetLightBarColor(Color color)
        {
            if (!CanSendDualSenseOutput())
            {
                return false;
            }

            DualSenseHIDOutputReport command = DualSenseHIDOutputReport.Create();
            command.SetLightBarColor(color);

            if (!ExecuteDualSenseOutput(ref command))
            {
                return false;
            }

            _lightBarColor = color;
            return true;
        }

        public bool SetMotorSpeeds(float lowFrequency, float highFrequency)
        {
            return SetFeedback(lowFrequency, highFrequency);
        }

        public bool SetRumbleAndLightBar(float lowFrequency, float highFrequency, Color color)
        {
            return SetFeedback(lowFrequency, highFrequency, color);
        }

        public bool SetFeedback(
            float lowFrequency,
            float highFrequency,
            Color? lightBarColor = null,
            DualSenseTriggerEffect? leftTriggerEffect = null,
            DualSenseTriggerEffect? rightTriggerEffect = null,
            DualSenseMicLedState? microphoneLedState = null,
            byte? playerLedMask = null,
            DualSensePlayerLedBrightness? playerLedBrightness = null)
        {
            if (!IsConnected)
            {
                return false;
            }

            bool requestsDualSenseFeatures =
                lightBarColor.HasValue ||
                leftTriggerEffect.HasValue ||
                rightTriggerEffect.HasValue ||
                microphoneLedState.HasValue ||
                playerLedMask.HasValue ||
                playerLedBrightness.HasValue;

            if (!IsDualSenseDevice)
            {
                if (_debugAllowGenericGamepadFallback && !requestsDualSenseFeatures)
                {
                    _controller.SetMotorSpeeds(Mathf.Clamp01(lowFrequency), Mathf.Clamp01(highFrequency));
                    return true;
                }

                CanSendDualSenseOutput();
                return false;
            }

            DualSenseHIDOutputReport command = DualSenseHIDOutputReport.Create();
            command.SetMotorSpeeds(Mathf.Clamp01(lowFrequency), Mathf.Clamp01(highFrequency));

            if (lightBarColor.HasValue)
            {
                command.SetLightBarColor(lightBarColor.Value);
            }

            if (leftTriggerEffect.HasValue)
            {
                command.SetTriggerState(true, leftTriggerEffect.Value);
            }

            if (rightTriggerEffect.HasValue)
            {
                command.SetTriggerState(false, rightTriggerEffect.Value);
            }

            if (microphoneLedState.HasValue)
            {
                command.SetMicLedState(microphoneLedState.Value);
            }

            if (playerLedMask.HasValue)
            {
                command.SetPlayerLedState(playerLedMask.Value);
            }

            if (playerLedBrightness.HasValue)
            {
                command.SetPlayerLedBrightness(playerLedBrightness.Value);
            }

            if (!ExecuteDualSenseOutput(ref command))
            {
                return false;
            }

            if (lightBarColor.HasValue)
            {
                _lightBarColor = lightBarColor.Value;
            }

            return true;
        }

        public void StopRumble()
        {
            if (!IsConnected)
            {
                return;
            }

            if (IsDualSenseDevice)
            {
                SetMotorSpeeds(0f, 0f);
            }
            else
            {
                _controller.SetMotorSpeeds(0f, 0f);
            }
        }

        public bool SetLeftTriggerEffect(DualSenseTriggerEffect effect)
        {
            if (!CanSendDualSenseOutput())
            {
                return false;
            }

            DualSenseHIDOutputReport command = DualSenseHIDOutputReport.Create();
            command.SetTriggerState(true, effect);
            return ExecuteDualSenseOutput(ref command);
        }

        public bool SetRightTriggerEffect(DualSenseTriggerEffect effect)
        {
            if (!CanSendDualSenseOutput())
            {
                return false;
            }

            DualSenseHIDOutputReport command = DualSenseHIDOutputReport.Create();
            command.SetTriggerState(false, effect);
            return ExecuteDualSenseOutput(ref command);
        }

        public bool SetAdaptiveTriggerEffects(DualSenseTriggerEffect leftEffect, DualSenseTriggerEffect rightEffect)
        {
            if (!CanSendDualSenseOutput())
            {
                return false;
            }

            DualSenseHIDOutputReport command = DualSenseHIDOutputReport.Create();
            command.SetTriggerState(true, leftEffect);
            command.SetTriggerState(false, rightEffect);
            return ExecuteDualSenseOutput(ref command);
        }

        public bool ResetAdaptiveTriggers()
        {
            return SetAdaptiveTriggerEffects(
                DualSenseTriggerEffect.Off(),
                DualSenseTriggerEffect.Off());
        }

        public bool SetMicrophoneMuteLed(DualSenseMicLedState state)
        {
            if (!CanSendDualSenseOutput())
            {
                return false;
            }

            DualSenseHIDOutputReport command = DualSenseHIDOutputReport.Create();
            command.SetMicLedState(state);
            return ExecuteDualSenseOutput(ref command);
        }

        public bool SetPlayerLedState(byte ledMask)
        {
            if (!CanSendDualSenseOutput())
            {
                return false;
            }

            DualSenseHIDOutputReport command = DualSenseHIDOutputReport.Create();
            command.SetPlayerLedState(ledMask);
            return ExecuteDualSenseOutput(ref command);
        }

        public bool SetPlayerLedBrightness(DualSensePlayerLedBrightness brightness)
        {
            if (!CanSendDualSenseOutput())
            {
                return false;
            }

            DualSenseHIDOutputReport command = DualSenseHIDOutputReport.Create();
            command.SetPlayerLedBrightness(brightness);
            return ExecuteDualSenseOutput(ref command);
        }

        public bool SetPlayerLeds(byte ledMask, DualSensePlayerLedBrightness brightness)
        {
            if (!CanSendDualSenseOutput())
            {
                return false;
            }

            DualSenseHIDOutputReport command = DualSenseHIDOutputReport.Create();
            command.SetPlayerLedState(ledMask);
            command.SetPlayerLedBrightness(brightness);
            return ExecuteDualSenseOutput(ref command);
        }

        public void StopFeedback()
        {
            if (IsDualSenseDevice)
            {
                DualSenseHIDOutputReport command = DualSenseHIDOutputReport.Create();
                command.SetMotorSpeeds(0f, 0f);
                command.SetTriggerState(true, DualSenseTriggerEffect.Off());
                command.SetTriggerState(false, DualSenseTriggerEffect.Off());
                ExecuteDualSenseOutput(ref command);
                return;
            }

            StopRumble();
        }

        #endregion

        //------------------------------- Initialization Methods -------------------------------//

        #region Initialization

private IEnumerator StopDebugRumbleAfterDelay()
        {
            yield return new WaitForSecondsRealtime(0.35f);
            _debugRumbleCoroutine = null;
            StopRumble();
        }

        private void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            InitializeEvents();
            RefreshController();
            _initialized = true;
#if UNITY_EDITOR
            CacheValidatedOutputSettings();
#endif
        }

        private void InitializeEvents()
        {
            UnsubscribeEvents();
            InputSystem.onDeviceChange += OnDeviceChange;
        }

        private void UnsubscribeEvents()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
        }

        private void RefreshController()
        {
            Gamepad nextController = null;

            if (_controllerOverride != null && _controllerOverride.added && _controllerOverride.enabled)
            {
                nextController = _controllerOverride;
            }
            else if (_detectControllerAutomatically)
            {
                nextController = FindController();
            }

            SetController(nextController);
        }

        private Gamepad FindController()
        {
            _deviceCandidates.Clear();

            foreach (Gamepad gamepad in Gamepad.all)
            {
                if (!gamepad.added || !gamepad.enabled)
                {
                    continue;
                }

                if (gamepad is DualSenseGamepadHID || IsDualSenseByDescription(gamepad))
                {
                    _deviceCandidates.Add(gamepad);
                }
            }

            if (_deviceCandidates.Count > 0 || !_debugAllowGenericGamepadFallback)
            {
                return _controllerIndex < _deviceCandidates.Count
                    ? _deviceCandidates[_controllerIndex]
                    : null;
            }

            _deviceCandidates.Clear();

            foreach (Gamepad gamepad in Gamepad.all)
            {
                if (gamepad.added && gamepad.enabled)
                {
                    _deviceCandidates.Add(gamepad);
                }
            }

            return _controllerIndex < _deviceCandidates.Count
                ? _deviceCandidates[_controllerIndex]
                : null;
        }

        private void SetController(Gamepad controller)
        {
            if (_controller == controller)
            {
                return;
            }

            bool wasConnected = IsConnected;

            if (_controller != null && _controller.added && _controller.enabled)
            {
                if (IsDualSenseDevice)
                {
                    StopFeedback();
                }
                else
                {
                    _controller.SetMotorSpeeds(0f, 0f);
                }
            }

            _controller = controller;
            _warnedAboutAdvancedOutput = false;

            if (IsUsingGenericGamepadFallback)
            {
                Debug.LogWarning(
                    "DualSense debug fallback selected a generic Gamepad. Standard controls and generic rumble can be tested, but DualSense-specific output requires a recognized DualSense HID layout.",
                    this);
            }

            if (IsConnected)
            {
                ApplyConnectionSettings();
            }

            if (wasConnected != IsConnected)
            {
                ConnectionChanged?.Invoke(IsConnected);
            }

            ControllerChanged?.Invoke(_controller);
        }

        private void ApplyConnectionSettings()
        {
            if (IsRecognizedDualSense(_controller))
            {
                DualSenseHIDOutputReport command = DualSenseHIDOutputReport.Create();
                bool hasOutput = false;

                if (_applyLightBarColorOnConnect)
                {
                    command.SetLightBarColor(_lightBarColor);
                    hasOutput = true;
                }

                if (_applyPlayerLedsOnConnect)
                {
                    command.SetPlayerLedState((byte)_playerLedMask);
                    command.SetPlayerLedBrightness(_playerLedBrightness);
                    hasOutput = true;
                }

                if (_applyTriggerEffectsOnConnect)
                {
                    command.SetTriggerState(true, _leftTriggerEffect);
                    command.SetTriggerState(false, _rightTriggerEffect);
                    hasOutput = true;
                }

                if (hasOutput)
                {
                    ExecuteDualSenseOutput(ref command);
                }

                return;
            }

            if (_applyLightBarColorOnConnect && _controller is DualShockGamepad fallbackController)
            {
                fallbackController.SetLightBarColor(_lightBarColor);
            }
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (change != InputDeviceChange.Added &&
                change != InputDeviceChange.Removed &&
                change != InputDeviceChange.Disconnected &&
                change != InputDeviceChange.Reconnected &&
                change != InputDeviceChange.Enabled &&
                change != InputDeviceChange.Disabled)
            {
                return;
            }

            if (device == _controller || device == _controllerOverride ||
                change == InputDeviceChange.Added || change == InputDeviceChange.Reconnected)
            {
                RefreshController();
            }
        }

        private bool CanSendDualSenseOutput()
        {
            if (IsDualSenseDevice)
            {
                return true;
            }

            if (!_warnedAboutAdvancedOutput)
            {
                Debug.LogWarning(
                    "DualSense HID output requires a connected DualSense controller. " +
                    "Standard gamepad input remains available on compatible fallback devices.",
                    this);
                _warnedAboutAdvancedOutput = true;
            }

            return false;
        }

        private bool ExecuteDualSenseOutput(ref DualSenseHIDOutputReport command)
        {
            long result = _controller.ExecuteCommand(ref command);

            if (result >= 0)
            {
                return true;
            }

            if (!_warnedAboutAdvancedOutput)
            {
                Debug.LogWarning(
                    "The connected controller did not accept the DualSense HID output command. " +
                    "USB HID is the intended transport; Bluetooth output may require a different report format.",
                    this);
                _warnedAboutAdvancedOutput = true;
            }

            return false;
        }

        #endregion

        //------------------------------- Utility Methods -------------------------------//

        #region Utilities

        private static bool IsRecognizedDualSense(Gamepad gamepad)
        {
            return gamepad is DualSenseGamepadHID || IsDualSenseByDescription(gamepad);
        }

        private static bool IsDualSenseByDescription(Gamepad gamepad)
        {
            if (gamepad == null)
            {
                return false;
            }

            string product = gamepad.description.product ?? string.Empty;
            string displayName = gamepad.displayName ?? string.Empty;

            if (product.IndexOf("DualSense", StringComparison.OrdinalIgnoreCase) >= 0 ||
                displayName.IndexOf("DualSense", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            string capabilitiesJson = gamepad.description.capabilities;

            if (string.IsNullOrWhiteSpace(capabilitiesJson))
            {
                return false;
            }

            try
            {
                HidDeviceCapabilities capabilities =
                    JsonUtility.FromJson<HidDeviceCapabilities>(capabilitiesJson);

                return capabilities != null &&
                       capabilities.vendorId == 0x054C &&
                       (capabilities.productId == 0x0CE6 || capabilities.productId == 0x0DF2);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        [Serializable]
        private sealed class HidDeviceCapabilities
        {
            public int vendorId;
            public int productId;
        }

        #endregion
    }

    // Adapted from the DualSense HID output-report layout in UniSense (MIT License).
    // Copyright (c) 2020 nullkal. See THIRD-PARTY-NOTICES.md.
    [StructLayout(LayoutKind.Explicit, Size = SIZE)]
    internal struct DualSenseHIDOutputReport : IInputDeviceCommandInfo
    {
        private const int SIZE = InputDeviceCommand.BaseCommandSize + 48;
        private const int REPORT_ID = 0x02;

        [Flags]
        private enum OutputFlags : byte
        {
            LowFrequencyMotor = 0x01,
            HighFrequencyMotor = 0x02,
            RightTrigger = 0x04,
            LeftTrigger = 0x08,
        }

        [Flags]
        private enum LedFlags : byte
        {
            PlayerLedBrightness = 0x01,
            LightBarFade = 0x02,
        }

        [FieldOffset(0)] private InputDeviceCommand _baseCommand;
        [FieldOffset(InputDeviceCommand.BaseCommandSize)] private byte _reportId;
        [FieldOffset(InputDeviceCommand.BaseCommandSize + 1)] private byte _flags1;
        [FieldOffset(InputDeviceCommand.BaseCommandSize + 2)] private byte _flags2;
        [FieldOffset(InputDeviceCommand.BaseCommandSize + 3)] private byte _lowFrequencyMotorSpeed;
        [FieldOffset(InputDeviceCommand.BaseCommandSize + 4)] private byte _highFrequencyMotorSpeed;
        [FieldOffset(InputDeviceCommand.BaseCommandSize + 9)] private byte _micLedState;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 11)] private byte _rightTriggerMode;
        [FieldOffset(InputDeviceCommand.BaseCommandSize + 12)] private byte _rightTriggerParam0;
        [FieldOffset(InputDeviceCommand.BaseCommandSize + 13)] private byte _rightTriggerParam1;
        [FieldOffset(InputDeviceCommand.BaseCommandSize + 14)] private byte _rightTriggerParam2;
        [FieldOffset(InputDeviceCommand.BaseCommandSize + 15)] private byte _rightTriggerParam3;
        [FieldOffset(InputDeviceCommand.BaseCommandSize + 16)] private byte _rightTriggerParam4;
        [FieldOffset(InputDeviceCommand.BaseCommandSize + 17)] private byte _rightTriggerParam5;
        [FieldOffset(InputDeviceCommand.BaseCommandSize + 18)] private byte _rightTriggerParam6;
        [FieldOffset(InputDeviceCommand.BaseCommandSize + 19)] private byte _rightTriggerParam7;
        [FieldOffset(InputDeviceCommand.BaseCommandSize + 20)] private byte _rightTriggerParam8;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 22)] private byte _leftTriggerMode;
        [FieldOffset(InputDeviceCommand.BaseCommandSize + 23)] private byte _leftTriggerParam0;
        [FieldOffset(InputDeviceCommand.BaseCommandSize + 24)] private byte _leftTriggerParam1;
        [FieldOffset(InputDeviceCommand.BaseCommandSize + 25)] private byte _leftTriggerParam2;
        [FieldOffset(InputDeviceCommand.BaseCommandSize + 26)] private byte _leftTriggerParam3;
        [FieldOffset(InputDeviceCommand.BaseCommandSize + 27)] private byte _leftTriggerParam4;
        [FieldOffset(InputDeviceCommand.BaseCommandSize + 28)] private byte _leftTriggerParam5;
        [FieldOffset(InputDeviceCommand.BaseCommandSize + 29)] private byte _leftTriggerParam6;
        [FieldOffset(InputDeviceCommand.BaseCommandSize + 30)] private byte _leftTriggerParam7;
        [FieldOffset(InputDeviceCommand.BaseCommandSize + 31)] private byte _leftTriggerParam8;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + 39)] private byte _ledFlags;
        [FieldOffset(InputDeviceCommand.BaseCommandSize + 42)] private byte _ledPulseOption;
        [FieldOffset(InputDeviceCommand.BaseCommandSize + 43)] private byte _playerLedBrightness;
        [FieldOffset(InputDeviceCommand.BaseCommandSize + 44)] private byte _playerLedState;
        [FieldOffset(InputDeviceCommand.BaseCommandSize + 45)] private byte _lightBarRed;
        [FieldOffset(InputDeviceCommand.BaseCommandSize + 46)] private byte _lightBarGreen;
        [FieldOffset(InputDeviceCommand.BaseCommandSize + 47)] private byte _lightBarBlue;

        public FourCC typeStatic => new FourCC('H', 'I', 'D', 'O');

        public static DualSenseHIDOutputReport Create()
        {
            return new DualSenseHIDOutputReport
            {
                _baseCommand = new InputDeviceCommand(new FourCC('H', 'I', 'D', 'O'), SIZE),
                _reportId = REPORT_ID
            };
        }

        public void SetMotorSpeeds(float lowFrequency, float highFrequency)
        {
            _flags1 |= (byte)(OutputFlags.LowFrequencyMotor | OutputFlags.HighFrequencyMotor);
            _lowFrequencyMotorSpeed = ToByte(lowFrequency);
            _highFrequencyMotorSpeed = ToByte(highFrequency);
        }

        public void SetTriggerState(bool isLeft, DualSenseTriggerEffect effect)
        {
            _flags1 |= (byte)(isLeft ? OutputFlags.LeftTrigger : OutputFlags.RightTrigger);

            if (isLeft)
            {
                _leftTriggerMode = effect.GetModeCode();
                _leftTriggerParam0 = effect.GetParameter(0);
                _leftTriggerParam1 = effect.GetParameter(1);
                _leftTriggerParam2 = effect.GetParameter(2);
                _leftTriggerParam3 = effect.GetParameter(3);
                _leftTriggerParam4 = effect.GetParameter(4);
                _leftTriggerParam5 = effect.GetParameter(5);
                _leftTriggerParam6 = effect.GetParameter(6);
                _leftTriggerParam7 = effect.GetParameter(7);
                _leftTriggerParam8 = effect.GetParameter(8);
            }
            else
            {
                _rightTriggerMode = effect.GetModeCode();
                _rightTriggerParam0 = effect.GetParameter(0);
                _rightTriggerParam1 = effect.GetParameter(1);
                _rightTriggerParam2 = effect.GetParameter(2);
                _rightTriggerParam3 = effect.GetParameter(3);
                _rightTriggerParam4 = effect.GetParameter(4);
                _rightTriggerParam5 = effect.GetParameter(5);
                _rightTriggerParam6 = effect.GetParameter(6);
                _rightTriggerParam7 = effect.GetParameter(7);
                _rightTriggerParam8 = effect.GetParameter(8);
            }
        }

        public void SetMicLedState(DualSenseMicLedState state)
        {
            _flags2 |= 0x01;
            _micLedState = (byte)state;
        }

        public void SetPlayerLedState(byte state)
        {
            _flags2 |= 0x10;
            _ledPulseOption = 0x02;
            _playerLedState = (byte)(state & 0x1F);
        }

        public void SetPlayerLedBrightness(DualSensePlayerLedBrightness brightness)
        {
            _ledFlags |= (byte)LedFlags.PlayerLedBrightness;
            _ledPulseOption = 0x02;
            _playerLedBrightness = (byte)brightness;
        }

        public void SetLightBarColor(Color color)
        {
            _flags2 |= 0x04;
            _ledFlags |= (byte)LedFlags.LightBarFade;
            _ledPulseOption = 0x02;
            _lightBarRed = ToByte(color.r);
            _lightBarGreen = ToByte(color.g);
            _lightBarBlue = ToByte(color.b);
        }

        private static byte ToByte(float value)
        {
            return (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255f);
        }
    }
}
