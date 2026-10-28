using System;
using System.Collections;
using Portfolio.Character;
using Portfolio.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;

namespace Portfolio.Input
{
    public class GameInput : MonoBehaviour
    {
        //------------------------------- Game Pause -------------------------------//

        private void OnGameStateChanged(GameState newGameState)
        {
            EnableInputForState(newGameState);
        }

        //------------------------------- Debugging Methods -------------------------------//

        private void Debugging()
        {
            _gameControlsEnabled = _gameplayMap != null && _gameplayMap.enabled;
            _systemControlsEnabled = _systemMap != null && _systemMap.enabled;
        }

        //------------------------------- Parameters -------------------------------//

        #region Components

        public const string MOUSE = "Mouse";
        public const string KEYBOARD = "Keyboard";
        public const string GAMEPAD = "Gamepad";
        public const string DUALSENSE = "DualSense";
        public const string DUALSHOCK = "DualShock";

        [Header("COMPONENTS")]
        [SerializeField] private InputActionAsset _inputActions;

        [Header("INPUT HANDLER")]
        [SerializeField] private bool _usePlayerActionGate = true;
        [SerializeField] private bool _updateCursorForCurrentDevice;
        [SerializeField, Range(0f, 1f)] private float _lookUpInputThreshold = 0.5f;

        [Header("MOVEMENT INPUT")]
        [Tooltip("Minimum left-stick horizontal input required to move. Vertical stick input is ignored for horizontal movement.")]
        [SerializeField, Range(0f, 1f)] private float _horizontalStickMovementThreshold = 0.35f;

        [Header("ATTACK FEEDBACK")]
        [SerializeField] private bool _rumbleOnAttack = true;
        [SerializeField, Range(0f, 1f)] private float _attackLowFrequencyRumble = 0.25f;
        [SerializeField, Range(0f, 1f)] private float _attackHighFrequencyRumble = 0.7f;
        [SerializeField, Min(0.01f)] private float _attackRumbleDuration = 0.18f;

        private static GameInput _instance;
        private InputActionMap _gameplayMap;
        private InputActionMap _systemMap;
        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _jumpAction;
        private InputAction _switchPhysicsAction;
        private InputAction _attackAction;
        private InputAction _pauseAction;
        private Vector2 _movementValue;
        private string _device = KEYBOARD;
        private CharacterAnimationController2D _activeCharacterAnimation;
        private Coroutine _attackRumbleCoroutine;
        private Gamepad _attackRumbleGamepad;

        public static GameInput Instance => _instance;
        public InputActionAsset InputActions => _inputActions;
        public Vector2 MovementValue => ShouldProcessPlayerAction() ? _movementValue : Vector2.zero;
        public bool GameControlsEnabled => _gameControlsEnabled;
        public bool SystemControlsEnabled => _systemControlsEnabled;

        public Func<bool> playerActionGate;

        public delegate void MoveDelegate(InputAction.CallbackContext context);
        public MoveDelegate moveEvent;

        public delegate void JumpDelegate(InputAction.CallbackContext context);
        public JumpDelegate jumpEvent;

        public delegate void JumpCanceledDelegate(InputAction.CallbackContext context);
        public JumpCanceledDelegate jumpCanceledEvent;

        public delegate void SwitchPhysicsDelegate(InputAction.CallbackContext context);
        public SwitchPhysicsDelegate switchPhysicsEvent;

        public delegate void AttackDelegate(InputAction.CallbackContext context);
        public AttackDelegate attackEvent;

        public delegate void PauseDelegate(InputAction.CallbackContext context);
        public PauseDelegate pauseEvent;

        public event Action<Vector2> MovementValueChanged;
        public event Action JumpPressed;
        public event Action JumpReleased;
        public event Action PhysicsSwitchRequested;
        public event Action AttackRequested;
        public event Action PauseRequested;

        #endregion

        #region Flags

        private bool _initialized;
        private bool _eventsInitialized;
        private bool _gameControlsBound;
        private bool _systemControlsBound;
        private bool _gameControlsEnabled;
        private bool _systemControlsEnabled;

        #endregion

        //------------------------------- Unity Methods -------------------------------//

        #region Unity

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

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
            EnableInputForState(GameManager.Instance != null
                ? GameManager.Instance.CurrentState
                : GameState.Normal);
        }

        private void Update()
        {
            UpdateLoop();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
            DisableControls();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
            DisableControls();

            if (_instance == this)
            {
                _instance = null;
            }
        }

        #endregion

        //------------------------------- Initialization Methods -------------------------------//

        #region Initialization

        private void Initialize()
        {
            if (_initialized || _instance != this)
            {
                return;
            }

            InitializeModules();

            if (!ValidateInputActions())
            {
                enabled = false;
                return;
            }

            InitializeEvents();
            EnableInputForState(GameManager.Instance != null
                ? GameManager.Instance.CurrentState
                : GameState.Normal);
            _initialized = true;
        }

        private void InitializeModules()
        {
            if (_inputActions == null)
            {
                return;
            }

            _gameplayMap = _inputActions.FindActionMap("Gameplay", false);
            _systemMap = _inputActions.FindActionMap("System", false);
            _moveAction = _gameplayMap?.FindAction("Move", false);
            _lookAction = _gameplayMap?.FindAction("Look", false);
            _jumpAction = _gameplayMap?.FindAction("Jump", false);
            _switchPhysicsAction = _gameplayMap?.FindAction("SwitchPhysics", false);
            _attackAction = _gameplayMap?.FindAction("Attack", false);
            _pauseAction = _systemMap?.FindAction("Pause", false);
        }

        private void InitializeEvents()
        {
            UnsubscribeEvents();
            BindDefaultGameControls(true);
            BindDefaultSystemControls(true);
            GameManager.GameStateChanged += OnGameStateChanged;
            _eventsInitialized = true;
        }

        private void UnsubscribeEvents()
        {
            if (_eventsInitialized)
            {
                BindDefaultGameControls(false);
                BindDefaultSystemControls(false);
            }

            GameManager.GameStateChanged -= OnGameStateChanged;
            _eventsInitialized = false;
        }

        #endregion

        //------------------------------- Core / Routines Methods -------------------------------//

        public void BindPlayerMoveAction(bool value)
        {
            if (_moveAction == null)
            {
                return;
            }

            _moveAction.performed -= MovePerformedHandler;
            _moveAction.canceled -= MoveCanceledHandler;

            if (value)
            {
                _moveAction.performed += MovePerformedHandler;
                _moveAction.canceled += MoveCanceledHandler;
            }
        }

        public void BindPlayerJumpAction(bool value)
        {
            if (_jumpAction == null)
            {
                return;
            }

            _jumpAction.started -= JumpStartedHandler;
            _jumpAction.canceled -= JumpCanceledHandler;

            if (value)
            {
                _jumpAction.started += JumpStartedHandler;
                _jumpAction.canceled += JumpCanceledHandler;
            }
        }

        public void BindPhysicsSwitchAction(bool value)
        {
            if (_switchPhysicsAction == null)
            {
                return;
            }

            _switchPhysicsAction.started -= SwitchPhysicsStartedHandler;

            if (value)
            {
                _switchPhysicsAction.started += SwitchPhysicsStartedHandler;
            }
        }

        public void BindPlayerAttackAction(bool value)
        {
            if (_attackAction == null)
            {
                return;
            }

            _attackAction.started -= AttackStartedHandler;

            if (value)
            {
                _attackAction.started += AttackStartedHandler;
            }
        }

        public void BindGamePauseAction(bool value)
        {
            if (_pauseAction == null)
            {
                return;
            }

            _pauseAction.started -= PauseStartedHandler;

            if (value)
            {
                _pauseAction.started += PauseStartedHandler;
            }
        }

        public void BindDefaultGameControls(bool value)
        {
            BindPlayerMoveAction(value);
            BindPlayerJumpAction(value);
            BindPhysicsSwitchAction(value);
            BindPlayerAttackAction(value);
            _gameControlsBound = value;
        }

        public void BindDefaultSystemControls(bool value)
        {
            BindGamePauseAction(value);
            _systemControlsBound = value;
        }

        public void DisableControls()
        {
            PublishMovement(Vector2.zero, true);
            ResetLookUpInput();
            StopAttackRumble();
            _gameplayMap?.Disable();
            _systemMap?.Disable();
            Debugging();
        }

        public void EnableGameControls()
        {
            _systemMap?.Enable();
            _gameplayMap?.Enable();
            Debugging();
        }

        public void EnableSystemControls()
        {
            PublishMovement(Vector2.zero, true);
            ResetLookUpInput();
            StopAttackRumble();
            _gameplayMap?.Disable();
            _systemMap?.Enable();
            Debugging();
        }

        public void UnbindEvents()
        {
            moveEvent = null;
            jumpEvent = null;
            jumpCanceledEvent = null;
            switchPhysicsEvent = null;
            pauseEvent = null;
            MovementValueChanged = null;
            JumpPressed = null;
            JumpReleased = null;
            PhysicsSwitchRequested = null;
            AttackRequested = null;
            PauseRequested = null;
            attackEvent = null;
        }

        public string GetCurrentDevice()
        {
            return _device;
        }

        private void UpdateLoop()
        {
            Debugging();
            UpdateHeldLookUp();

            if (_updateCursorForCurrentDevice)
            {
                UpdateCursor();
            }
        }

        private void EnableInputForState(GameState state)
        {
            if (!ValidateInputActions())
            {
                return;
            }

            if (!_gameControlsBound)
            {
                BindDefaultGameControls(true);
            }

            if (!_systemControlsBound)
            {
                BindDefaultSystemControls(true);
            }

            if (state == GameState.Paused)
            {
                EnableSystemControls();
                return;
            }

            EnableGameControls();
        }

        private bool ValidateInputActions()
        {
            if (_inputActions != null &&
                _gameplayMap != null &&
                _systemMap != null &&
                _moveAction != null &&
                _lookAction != null &&
                _jumpAction != null &&
                _switchPhysicsAction != null &&
                _attackAction != null &&
                _pauseAction != null)
            {
                return true;
            }

            Debug.LogError(
                "GameInput requires Gameplay/Move, Gameplay/Look, Gameplay/Jump, " +
                "Gameplay/SwitchPhysics, Gameplay/Attack and System/Pause actions.",
                this);
            return false;
        }

        private bool ShouldProcessPlayerAction()
        {
            if (!_usePlayerActionGate)
            {
                return true;
            }

            return playerActionGate?.Invoke() ?? true;
        }

        private void PublishMovement(Vector2 movementValue, bool forcePublish = false)
        {
            if (!forcePublish && _movementValue == movementValue)
            {
                return;
            }

            _movementValue = movementValue;
            MovementValueChanged?.Invoke(_movementValue);
        }

        private void SetCurrentDevice(InputAction.CallbackContext context)
        {
            InputDevice device = context.control?.device;

            if (device is DualSenseGamepadHID || DeviceMatchesName(device, DUALSENSE))
            {
                _device = DUALSENSE;
            }
            else if (device is DualShockGamepad ||
                     device is DualShock4GamepadHID ||
                     DeviceMatchesName(device, DUALSHOCK))
            {
                _device = DUALSHOCK;
            }
            else if (device is Gamepad)
            {
                _device = GAMEPAD;
            }
            else if (device is Mouse)
            {
                _device = MOUSE;
            }
            else if (device is Keyboard)
            {
                _device = KEYBOARD;
            }
            else if (device != null)
            {
                _device = device.displayName;
            }
        }

        private bool DeviceMatchesName(InputDevice device, string deviceName)
        {
            if (device == null)
            {
                return false;
            }

            string product = device.description.product ?? string.Empty;
            string displayName = device.displayName ?? string.Empty;
            return product.IndexOf(deviceName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                displayName.IndexOf(deviceName, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void UpdateCursor()
        {
            bool pointerDevice = _device == MOUSE || _device == KEYBOARD;
            Cursor.visible = pointerDevice;
            Cursor.lockState = pointerDevice ? CursorLockMode.None : CursorLockMode.Locked;
        }

        private void UpdateHeldLookUp()
        {
            bool upHeld = ShouldProcessPlayerAction() &&
                _lookAction != null &&
                _lookAction.ReadValue<Vector2>().y > _lookUpInputThreshold;

            if (_activeCharacterAnimation != null &&
                (!_activeCharacterAnimation.isActiveAndEnabled ||
                 !_activeCharacterAnimation.gameObject.activeInHierarchy))
            {
                _activeCharacterAnimation.SetLookUpInputHeld(false);
                _activeCharacterAnimation = null;
            }

            if (_activeCharacterAnimation == null && !upHeld)
            {
                return;
            }

            if (_activeCharacterAnimation == null && upHeld)
            {
                _activeCharacterAnimation = FindFirstObjectByType<CharacterAnimationController2D>();
            }

            _activeCharacterAnimation?.SetLookUpInputHeld(upHeld);
        }

        private void ResetLookUpInput()
        {
            _activeCharacterAnimation?.SetLookUpInputHeld(false);
            _activeCharacterAnimation = null;
        }

        private CharacterAnimationController2D GetActiveCharacterAnimation()
        {
            if (_activeCharacterAnimation != null &&
                _activeCharacterAnimation.isActiveAndEnabled &&
                _activeCharacterAnimation.gameObject.activeInHierarchy)
            {
                return _activeCharacterAnimation;
            }

            CharacterAnimationController2D nextAnimation =
                FindFirstObjectByType<CharacterAnimationController2D>();

            if (_activeCharacterAnimation != nextAnimation)
            {
                _activeCharacterAnimation?.SetLookUpInputHeld(false);
                _activeCharacterAnimation = nextAnimation;
            }

            return _activeCharacterAnimation;
        }

        private void PlayAttackRumble(Gamepad sourceGamepad)
        {
            if (!_rumbleOnAttack)
            {
                return;
            }

            Gamepad gamepad = sourceGamepad != null && sourceGamepad.added
                ? sourceGamepad
                : Gamepad.current;

            if (gamepad == null || !gamepad.added)
            {
                foreach (Gamepad candidate in Gamepad.all)
                {
                    if (candidate != null && candidate.added && candidate.enabled)
                    {
                        gamepad = candidate;
                        break;
                    }
                }
            }

            if (gamepad == null || !gamepad.added || !gamepad.enabled)
            {
                return;
            }

            StopAttackRumble();
            _attackRumbleGamepad = gamepad;
            _attackRumbleGamepad.SetMotorSpeeds(
                _attackLowFrequencyRumble,
                _attackHighFrequencyRumble);
            _attackRumbleCoroutine = StartCoroutine(StopAttackRumbleAfterDelay(gamepad));
        }

        private IEnumerator StopAttackRumbleAfterDelay(Gamepad gamepad)
        {
            yield return new WaitForSecondsRealtime(_attackRumbleDuration);

            if (gamepad != null && gamepad.added)
            {
                gamepad.SetMotorSpeeds(0f, 0f);
            }

            if (_attackRumbleGamepad == gamepad)
            {
                _attackRumbleGamepad = null;
                _attackRumbleCoroutine = null;
            }
        }

        private void StopAttackRumble()
        {
            if (_attackRumbleCoroutine != null)
            {
                StopCoroutine(_attackRumbleCoroutine);
                _attackRumbleCoroutine = null;
            }

            if (_attackRumbleGamepad != null && _attackRumbleGamepad.added)
            {
                _attackRumbleGamepad.SetMotorSpeeds(0f, 0f);
            }

            _attackRumbleGamepad = null;
        }

        private void MovePerformedHandler(InputAction.CallbackContext context)
        {
            SetCurrentDevice(context);

            if (!ShouldProcessPlayerAction())
            {
                PublishMovement(Vector2.zero, true);
                return;
            }

            PublishMovement(ReadMovementValue(context));
            moveEvent?.Invoke(context);
        }

        private Vector2 ReadMovementValue(InputAction.CallbackContext context)
        {
            Vector2 movementValue = context.ReadValue<Vector2>();

            if (!IsLeftStickInput(context.control))
            {
                return movementValue;
            }

            float horizontalInput = Mathf.Abs(movementValue.x) >= _horizontalStickMovementThreshold
                ? movementValue.x
                : 0f;

            // The left stick controls horizontal locomotion only. Its vertical axis is ignored.
            return new Vector2(horizontalInput, 0f);
        }

        private bool IsLeftStickInput(InputControl control)
        {
            return control != null &&
                control.device is Gamepad &&
                control.path.IndexOf("/leftStick", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void MoveCanceledHandler(InputAction.CallbackContext context)
        {
            SetCurrentDevice(context);
            PublishMovement(Vector2.zero);
        }

        private void JumpStartedHandler(InputAction.CallbackContext context)
        {
            SetCurrentDevice(context);

            if (!ShouldProcessPlayerAction())
            {
                return;
            }

            jumpEvent?.Invoke(context);
            JumpPressed?.Invoke();
        }

        private void JumpCanceledHandler(InputAction.CallbackContext context)
        {
            SetCurrentDevice(context);

            if (!ShouldProcessPlayerAction())
            {
                return;
            }

            jumpCanceledEvent?.Invoke(context);
            JumpReleased?.Invoke();
        }

        private void SwitchPhysicsStartedHandler(InputAction.CallbackContext context)
        {
            SetCurrentDevice(context);

            if (!ShouldProcessPlayerAction())
            {
                return;
            }

            switchPhysicsEvent?.Invoke(context);
            PhysicsSwitchRequested?.Invoke();
        }

        private void AttackStartedHandler(InputAction.CallbackContext context)
        {
            SetCurrentDevice(context);

            if (!ShouldProcessPlayerAction())
            {
                return;
            }

            attackEvent?.Invoke(context);
            AttackRequested?.Invoke();
            GetActiveCharacterAnimation()?.Attack();
            PlayAttackRumble(context.control?.device as Gamepad);
        }

        private void PauseStartedHandler(InputAction.CallbackContext context)
        {
            SetCurrentDevice(context);
            pauseEvent?.Invoke(context);
            PauseRequested?.Invoke();
            GameManager.Instance?.TogglePause();
        }

#if UNITY_EDITOR
        //------------------------------- Editor -------------------------------//

        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                InitializeModules();
            }
        }
#endif
    }
}
