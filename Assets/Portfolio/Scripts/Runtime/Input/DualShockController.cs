using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.DualShock;

namespace Portfolio.Input
{
    public class DualShockController : MonoBehaviour
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
        [SerializeField] private bool _applyRumbleOnConnect;
        [SerializeField, Range(0f, 1f)] private float _lowFrequencyMotorSpeed;
        [SerializeField, Range(0f, 1f)] private float _highFrequencyMotorSpeed;

        private Gamepad _controller;
        private Coroutine _debugRumbleCoroutine;
        private bool _initialized;
        private readonly List<Gamepad> _deviceCandidates = new List<Gamepad>();

#if UNITY_EDITOR
        private bool _hasValidatedOutputSettings;
        private Color _validatedLightBarColor;
        private float _validatedLowFrequencyMotorSpeed;
        private float _validatedHighFrequencyMotorSpeed;
        private bool _validatedApplyLightBarColorOnConnect;
        private bool _validatedApplyRumbleOnConnect;
#endif

        public event Action<bool> ConnectionChanged;
        public event Action<Gamepad> ControllerChanged;

        #endregion

        #region Properties

        public Gamepad CurrentGamepad => _controller;
        public DualShockGamepad PlayStationGamepad => _controller as DualShockGamepad;
        public bool IsConnected => _controller != null && _controller.added && _controller.enabled;
        public bool IsUsingGenericGamepadFallback =>
            IsConnected && _debugAllowGenericGamepadFallback && !(_controller is DualShockGamepad);
        public bool SupportsRumble =>
            IsConnected &&
            (_controller is DualShockGamepad ||
             (_debugAllowGenericGamepadFallback && !(_controller is DualSenseGamepadHID)));
        public bool SupportsLightBar => IsConnected && _controller is DualShock4GamepadHID;
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
        public bool SharePressed => IsConnected && _controller.selectButton.isPressed;
        public bool TouchpadPressed => ReadButtonControl("touchpadButton");

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
            CancelDebugRumbleRoutine();
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
                StopRumble();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _controllerIndex = Mathf.Max(0, _controllerIndex);
            _lowFrequencyMotorSpeed = Mathf.Clamp01(_lowFrequencyMotorSpeed);
            _highFrequencyMotorSpeed = Mathf.Clamp01(_highFrequencyMotorSpeed);

            if (Application.isPlaying && _initialized)
            {
                RefreshController();
                ApplyInspectorChanges();
            }
        }

        private void ApplyInspectorChanges()
        {
            if (!_hasValidatedOutputSettings)
            {
                CacheValidatedOutputSettings();
                return;
            }

            bool lightBarChanged = _lightBarColor != _validatedLightBarColor ||
                (!_validatedApplyLightBarColorOnConnect && _applyLightBarColorOnConnect);
            bool rumbleChanged =
                !Mathf.Approximately(_lowFrequencyMotorSpeed, _validatedLowFrequencyMotorSpeed) ||
                !Mathf.Approximately(_highFrequencyMotorSpeed, _validatedHighFrequencyMotorSpeed) ||
                (!_validatedApplyRumbleOnConnect && _applyRumbleOnConnect);

            if (IsConnected)
            {
                if (lightBarChanged)
                {
                    SetLightBarColor(_lightBarColor);
                }

                if (rumbleChanged && SupportsRumble)
                {
                    SetMotorSpeeds(_lowFrequencyMotorSpeed, _highFrequencyMotorSpeed);
                }
            }

            CacheValidatedOutputSettings();
        }

        private void CacheValidatedOutputSettings()
        {
            _validatedLightBarColor = _lightBarColor;
            _validatedLowFrequencyMotorSpeed = _lowFrequencyMotorSpeed;
            _validatedHighFrequencyMotorSpeed = _highFrequencyMotorSpeed;
            _validatedApplyLightBarColorOnConnect = _applyLightBarColorOnConnect;
            _validatedApplyRumbleOnConnect = _applyRumbleOnConnect;
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
            position = Vector2.zero;
            InputControl control = FindControl("touch0/position", "touchpad/position", "touchpadPosition");

            if (control == null)
            {
                return false;
            }

            object value = control.ReadValueAsObject();

            if (!(value is Vector2 vector))
            {
                return false;
            }

            position = vector;
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
                    if (string.Equals(control.path, paths[i], StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(control.name, paths[i], StringComparison.OrdinalIgnoreCase))
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
            CancelDebugRumbleRoutine();

            if (SetMotorSpeeds(0.35f, 0.8f))
            {
                _debugRumbleCoroutine = StartCoroutine(StopDebugRumbleAfterDelay());
            }
        }

        public void DebugTestRumbleAndLightBar()
        {
            CancelDebugRumbleRoutine();

            if (SetRumbleAndLightBar(0.35f, 0.8f, new Color(0.1f, 0.45f, 1f, 1f)))
            {
                _debugRumbleCoroutine = StartCoroutine(StopDebugRumbleAfterDelay());
            }
        }

        

        

        public bool SetLightBarColor(Color color)
        {
            if (!SupportsLightBar || !(_controller is DualShock4GamepadHID dualShock4))
            {
                return false;
            }

            _lightBarColor = color;
            dualShock4.SetLightBarColor(color);
            return true;
        }

        public bool SetMotorSpeeds(float lowFrequency, float highFrequency)
        {
            if (!IsConnected)
            {
                return false;
            }

            _lowFrequencyMotorSpeed = Mathf.Clamp01(lowFrequency);
            _highFrequencyMotorSpeed = Mathf.Clamp01(highFrequency);

            if (_controller is DualShockGamepad dualShockController)
            {
                dualShockController.SetMotorSpeeds(_lowFrequencyMotorSpeed, _highFrequencyMotorSpeed);
                return true;
            }

            if (_debugAllowGenericGamepadFallback && !(_controller is DualSenseGamepadHID))
            {
                _controller.SetMotorSpeeds(_lowFrequencyMotorSpeed, _highFrequencyMotorSpeed);
                return true;
            }

            return false;
        }

        public bool SetRumbleAndLightBar(float lowFrequency, float highFrequency, Color color)
        {
            if (!IsConnected || !(_controller is DualShock4GamepadHID dualShock4))
            {
                return false;
            }

            _lowFrequencyMotorSpeed = Mathf.Clamp01(lowFrequency);
            _highFrequencyMotorSpeed = Mathf.Clamp01(highFrequency);
            _lightBarColor = color;

            return dualShock4.SetMotorSpeedsAndLightBarColor(
                _lowFrequencyMotorSpeed,
                _highFrequencyMotorSpeed,
                _lightBarColor);
        }

        public void StopRumble()
        {
            if (!IsConnected)
            {
                return;
            }

            if (_controller is DualShockGamepad dualShockController)
            {
                dualShockController.SetMotorSpeeds(0f, 0f);
            }
            else if (_debugAllowGenericGamepadFallback && !(_controller is DualSenseGamepadHID))
            {
                _controller.SetMotorSpeeds(0f, 0f);
            }
        }

        public void ResetHaptics()
        {
            if (IsConnected && _controller is DualShockGamepad dualShockController)
            {
                dualShockController.ResetHaptics();
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

private void CancelDebugRumbleRoutine()
        {
            if (_debugRumbleCoroutine != null)
            {
                StopCoroutine(_debugRumbleCoroutine);
                _debugRumbleCoroutine = null;
                StopRumble();
            }
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
                if (!gamepad.added || !gamepad.enabled ||
                    gamepad is DualSenseGamepadHID ||
                    IsDualSenseByDescription(gamepad))
                {
                    continue;
                }

                if (gamepad is DualShockGamepad || IsPlayStationByDescription(gamepad))
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
                if (gamepad.added && gamepad.enabled &&
                    !(gamepad is DualSenseGamepadHID) &&
                    !IsDualSenseByDescription(gamepad))
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
                _controller.SetMotorSpeeds(0f, 0f);
            }

            _controller = controller;

            if (IsUsingGenericGamepadFallback)
            {
                Debug.LogWarning(
                    "DualShock debug fallback selected a generic Gamepad. Standard controls and generic rumble can be tested, but DualShock-specific output requires a supported PlayStation layout.",
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
            if (_controller is DualShock4GamepadHID &&
                _applyLightBarColorOnConnect &&
                _applyRumbleOnConnect)
            {
                SetRumbleAndLightBar(
                    _lowFrequencyMotorSpeed,
                    _highFrequencyMotorSpeed,
                    _lightBarColor);
                return;
            }

            if (_applyLightBarColorOnConnect)
            {
                SetLightBarColor(_lightBarColor);
            }

            if (_applyRumbleOnConnect)
            {
                SetMotorSpeeds(_lowFrequencyMotorSpeed, _highFrequencyMotorSpeed);
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

        #endregion

        //------------------------------- Utility Methods -------------------------------//

        #region Utilities

        private static bool IsPlayStationByDescription(Gamepad gamepad)
        {
            if (gamepad == null)
            {
                return false;
            }

            string product = gamepad.description.product ?? string.Empty;
            string manufacturer = gamepad.description.manufacturer ?? string.Empty;
            string displayName = gamepad.displayName ?? string.Empty;

            bool sonyDevice = manufacturer.IndexOf("Sony", StringComparison.OrdinalIgnoreCase) >= 0;
            bool playStationProduct =
                product.IndexOf("DualShock", StringComparison.OrdinalIgnoreCase) >= 0 ||
                product.IndexOf("PlayStation", StringComparison.OrdinalIgnoreCase) >= 0 ||
                displayName.IndexOf("DualShock", StringComparison.OrdinalIgnoreCase) >= 0 ||
                displayName.IndexOf("PlayStation", StringComparison.OrdinalIgnoreCase) >= 0;

            return playStationProduct || (sonyDevice &&
                product.IndexOf("Wireless Controller", StringComparison.OrdinalIgnoreCase) >= 0);
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
}
