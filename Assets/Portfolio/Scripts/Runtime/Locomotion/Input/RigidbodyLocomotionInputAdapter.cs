using Portfolio.Game;
using Portfolio.Input;
using Portfolio.Locomotion.Rigidbody;
using UnityEngine;

namespace Portfolio.Locomotion.Input
{
    public class RigidbodyLocomotionInputAdapter : MonoBehaviour
    {
        //------------------------------- Game Pause -------------------------------//

        //------------------------------- Debugging Methods -------------------------------//

        //------------------------------- Parameters -------------------------------//

        #region Components
        [SerializeField] private GameInput _gameInput;
        [SerializeField] private RigidbodyLocomotionController _controller;

        private bool _initialized;

        #endregion

        #region Flags

        #endregion

        //------------------------------- Unity Methods -------------------------------//

        #region Unity
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
            ApplyGameState(GameManager.Instance != null ? GameManager.Instance.CurrentState : GameState.Normal);
        }

        private void OnDisable()
        {
            CleanupEvents();
            DisableLocomotionControl();
        }

        private void OnDestroy()
        {
            CleanupEvents();
        }

        #endregion

        //------------------------------- Initialization Methods -------------------------------//

        #region Initialization

        private void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            InitializeComponents();
            InitializeModules();
            _initialized = true;
            InitializeEvents();
            ApplyGameState(GameManager.Instance != null ? GameManager.Instance.CurrentState : GameState.Normal);
        }

        private void InitializeComponents()
        {
            if (_controller == null)
            {
                _controller = GetComponent<RigidbodyLocomotionController>();
            }
        }

        private void InitializeModules()
        {
            if (_gameInput == null)
            {
                _gameInput = GameInput.Instance;
            }
        }

        private void InitializeEvents()
        {
            if (_gameInput != null)
            {
                _gameInput.MovementValueChanged -= HandleMovementValueChanged;
                _gameInput.JumpPressed -= HandleJumpPressed;
                _gameInput.JumpReleased -= HandleJumpReleased;
                _gameInput.MovementValueChanged += HandleMovementValueChanged;
                _gameInput.JumpPressed += HandleJumpPressed;
                _gameInput.JumpReleased += HandleJumpReleased;
            }

            GameManager.GameStateChanged -= HandleGameStateChanged;
            GameManager.GameStateChanged += HandleGameStateChanged;
        }

        #endregion

        //------------------------------- Core / Routines Methods -------------------------------//

        private void HandleMovementValueChanged(Vector2 movementValue)
        {
            _controller?.SetMovementInput(movementValue.x);
        }

        private void HandleJumpPressed()
        {
            _controller?.RequestJump();
        }

        private void HandleJumpReleased()
        {
            _controller?.ReleaseJump();
        }

        private void HandleGameStateChanged(GameState state)
        {
            ApplyGameState(state);
        }

        private void ApplyGameState(GameState state)
        {
            if (state == GameState.Paused)
            {
                DisableLocomotionControl();
                return;
            }

            _controller?.SetControlEnabled(true);
        }

        private void DisableLocomotionControl()
        {
            if (_controller == null)
            {
                return;
            }

            _controller.SetMovementInput(0f);
            _controller.SetControlEnabled(false);
        }

        private void CleanupEvents()
        {
            if (_gameInput != null)
            {
                _gameInput.MovementValueChanged -= HandleMovementValueChanged;
                _gameInput.JumpPressed -= HandleJumpPressed;
                _gameInput.JumpReleased -= HandleJumpReleased;
            }

            GameManager.GameStateChanged -= HandleGameStateChanged;
        }
#if UNITY_EDITOR
        //------------------------------- Editor -------------------------------//

        private void OnValidate()
        {
            if (_controller == null)
            {
                _controller = GetComponent<RigidbodyLocomotionController>();
            }
        }
#endif
    }
}
