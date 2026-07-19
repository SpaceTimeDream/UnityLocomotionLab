using System;
using Portfolio.Game;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Portfolio.Input
{
    public class GameInput : MonoBehaviour
    {
        // Fields
        [SerializeField] private InputActionAsset _inputActions;

        private static GameInput _instance;
        private InputActionMap _gameplayMap;
        private InputActionMap _systemMap;
        private InputAction _moveAction;
        private InputAction _jumpAction;
        private InputAction _pauseAction;
        private Vector2 _movementValue;
        private bool _initialized;
        private bool _callbacksBound;

        // Properties
        public static GameInput Instance => _instance;
        public Vector2 MovementValue => _movementValue;

        public event Action<Vector2> MovementValueChanged;
        public event Action JumpPressed;
        public event Action JumpReleased;
        public event Action PauseRequested;

        // Unity Methods
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

            BindCallbacks();
            GameManager.GameStateChanged -= HandleGameStateChanged;
            GameManager.GameStateChanged += HandleGameStateChanged;
            EnableInputForState(GameManager.Instance != null ? GameManager.Instance.CurrentState : GameState.Normal);
        }

        private void OnDisable()
        {
            CleanupEvents();
            DisableMaps();
        }

        private void OnDestroy()
        {
            CleanupEvents();

            if (_instance == this)
            {
                _instance = null;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
        }
#endif

        // Private Methods
        private void Initialize()
        {
            if (_initialized || _instance != this)
            {
                return;
            }

            InitializeComponents();
            InitializeModules();

            if (!ValidateInputActions())
            {
                enabled = false;
                return;
            }

            _initialized = true;
            InitializeEvents();
            EnableInputForState(GameManager.Instance != null ? GameManager.Instance.CurrentState : GameState.Normal);
        }

        private void InitializeComponents()
        {
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
            _jumpAction = _gameplayMap?.FindAction("Jump", false);
            _pauseAction = _systemMap?.FindAction("Pause", false);
        }

        private void InitializeEvents()
        {
            BindCallbacks();
            GameManager.GameStateChanged -= HandleGameStateChanged;
            GameManager.GameStateChanged += HandleGameStateChanged;
        }

        private bool ValidateInputActions()
        {
            if (_inputActions != null && _gameplayMap != null && _systemMap != null && _moveAction != null && _jumpAction != null && _pauseAction != null)
            {
                return true;
            }

            Debug.LogError("GameInput requires an InputActionAsset with Gameplay/Move, Gameplay/Jump and System/Pause actions.", this);
            return false;
        }

        private void BindCallbacks()
        {
            if (_callbacksBound || !ValidateInputActions())
            {
                return;
            }

            _moveAction.performed += HandleMovePerformed;
            _moveAction.canceled += HandleMoveCanceled;
            _jumpAction.started += HandleJumpStarted;
            _jumpAction.canceled += HandleJumpCanceled;
            _pauseAction.performed += HandlePausePerformed;
            _callbacksBound = true;
        }

        private void EnableInputForState(GameState state)
        {
            _systemMap.Enable();

            if (state == GameState.Paused)
            {
                PublishMovement(Vector2.zero, true);
                _gameplayMap.Disable();
                return;
            }

            _gameplayMap.Enable();
        }

        private void DisableMaps()
        {
            PublishMovement(Vector2.zero, true);
            _gameplayMap?.Disable();
            _systemMap?.Disable();
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

        private void HandleMovePerformed(InputAction.CallbackContext context)
        {
            PublishMovement(context.ReadValue<Vector2>());
        }

        private void HandleMoveCanceled(InputAction.CallbackContext context)
        {
            PublishMovement(Vector2.zero);
        }

        private void HandleJumpStarted(InputAction.CallbackContext context)
        {
            JumpPressed?.Invoke();
        }

        private void HandleJumpCanceled(InputAction.CallbackContext context)
        {
            JumpReleased?.Invoke();
        }

        private void HandlePausePerformed(InputAction.CallbackContext context)
        {
            PauseRequested?.Invoke();
            GameManager.Instance?.TogglePause();
        }

        private void HandleGameStateChanged(GameState state)
        {
            EnableInputForState(state);
        }

        private void CleanupEvents()
        {
            if (_callbacksBound)
            {
                _moveAction.performed -= HandleMovePerformed;
                _moveAction.canceled -= HandleMoveCanceled;
                _jumpAction.started -= HandleJumpStarted;
                _jumpAction.canceled -= HandleJumpCanceled;
                _pauseAction.performed -= HandlePausePerformed;
                _callbacksBound = false;
            }

            GameManager.GameStateChanged -= HandleGameStateChanged;
        }
    }
}
