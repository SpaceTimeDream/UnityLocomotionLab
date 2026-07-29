using Portfolio.Locomotion.Rigidbody;
using Portfolio.Locomotion.Spline;
using UnityEngine;

namespace Portfolio.Locomotion.Core
{
    public class LocomotionDemoSwitcher : MonoBehaviour
    {
        //------------------------------- Game Pause -------------------------------//

        //------------------------------- Debugging Methods -------------------------------//

        //------------------------------- Parameters -------------------------------//

        #region Components

        [Header("COMPONENTS")]
        [SerializeField] private RigidbodyLocomotionController _rigidbodyController;
        [SerializeField] private SimulatedSplineLocomotionController _simulatedController;
        [SerializeField] private BasicCameraFollow2D _cameraFollow;

        [Header("DISPLAY PARAMETERS")]
        [SerializeField] private bool _startWithRigidbody = true;
        [SerializeField] private bool _showDebugButton = true;
        [SerializeField] private Rect _buttonRect = new Rect(16f, 16f, 420f, 44f);
        [SerializeField] private Color _rigidbodyColor = new Color(0.15f, 1f, 0f, 1f);
        [SerializeField] private Color _simulatedColor = new Color(1f, 0.55f, 0.2f, 1f);

        private Transform _activePlayer;

        public Transform ActivePlayer => _activePlayer;
        public bool RigidbodyActive => _rigidbodyActive;

        #endregion

        #region Flags

        private bool _initialized;
        private bool _rigidbodyActive;

        #endregion

        //------------------------------- Unity Methods -------------------------------//

        #region Unity

        private void Start()
        {
            Initialize();
        }

        private void OnGUI()
        {
            DrawSwitchButton();
        }

        #endregion

        //------------------------------- Initialization Methods -------------------------------//

        #region Initialization

        private void Initialize()
        {
            InitializeModules();
            InitializeComponents();
            InitializeEvents();
            ApplyPlayerColors();
            SetActivePlayer(_startWithRigidbody, false);
            _initialized = true;
        }

        private void InitializeModules()
        {
        }

        private void InitializeComponents()
        {
            if (_cameraFollow == null && Camera.main != null)
            {
                _cameraFollow = Camera.main.GetComponent<BasicCameraFollow2D>();
            }
        }

        private void InitializeEvents()
        {
        }

        #endregion

        //------------------------------- Core / Routines Methods -------------------------------//

        public void TogglePlayer()
        {
            SetActivePlayer(!_rigidbodyActive, true);
        }

        public void ShowRigidbodyPlayer()
        {
            SetActivePlayer(true, true);
        }

        public void ShowSimulatedPlayer()
        {
            SetActivePlayer(false, true);
        }

        private void SetActivePlayer(bool useRigidbody, bool transferPosition)
        {
            if (_rigidbodyController == null || _simulatedController == null)
            {
                return;
            }

            if (_initialized && _rigidbodyActive == useRigidbody)
            {
                return;
            }

            Transform outgoingPlayer = GetPlayerTransform(!useRigidbody);
            CapsuleCollider2D outgoingCollider = GetPlayerCollider(!useRigidbody);
            Vector3 transferPosition = GetTransferPosition(outgoingPlayer, outgoingCollider);

            SetPlayerControl(!useRigidbody, false);
            SetPlayerActive(!useRigidbody, false);
            SetPlayerActive(useRigidbody, true);

            Transform incomingPlayer = GetPlayerTransform(useRigidbody);
            CapsuleCollider2D incomingCollider = GetPlayerCollider(useRigidbody);

            if (transferPosition)
            {
                AlignPlayerToTransferPosition(incomingPlayer, incomingCollider, transferPosition);
            }

            ResetPlayer(useRigidbody);
            SetPlayerControl(useRigidbody, true);
            _rigidbodyActive = useRigidbody;
            _activePlayer = incomingPlayer;
            _cameraFollow?.SetTarget(_activePlayer, true);
        }

        private Transform GetPlayerTransform(bool useRigidbody)
        {
            return useRigidbody
                ? _rigidbodyController.transform
                : _simulatedController.transform;
        }

        private CapsuleCollider2D GetPlayerCollider(bool useRigidbody)
        {
            return useRigidbody
                ? _rigidbodyController.MainCollider
                : _simulatedController.MainCollider;
        }

        private Vector3 GetTransferPosition(
            Transform playerTransform,
            CapsuleCollider2D playerCollider)
        {
            if (playerTransform == null)
            {
                return Vector3.zero;
            }

            if (playerCollider == null || !playerCollider.gameObject.activeInHierarchy)
            {
                return playerTransform.position;
            }

            Bounds bounds = playerCollider.bounds;
            return new Vector3(bounds.center.x, bounds.min.y, playerTransform.position.z);
        }

        private void AlignPlayerToTransferPosition(
            Transform playerTransform,
            CapsuleCollider2D playerCollider,
            Vector3 transferPosition)
        {
            if (playerTransform == null)
            {
                return;
            }

            Vector3 position = playerTransform.position;
            position.z = transferPosition.z;
            playerTransform.position = position;
            Physics2D.SyncTransforms();

            if (playerCollider == null)
            {
                playerTransform.position = transferPosition;
                return;
            }

            Bounds bounds = playerCollider.bounds;
            Vector3 adjustment = new Vector3(
                transferPosition.x - bounds.center.x,
                transferPosition.y - bounds.min.y,
                0f);
            playerTransform.position += adjustment;
            Physics2D.SyncTransforms();
        }

        private void SetPlayerActive(bool useRigidbody, bool active)
        {
            GetPlayerTransform(useRigidbody).gameObject.SetActive(active);
        }

        private void SetPlayerControl(bool useRigidbody, bool controlEnabled)
        {
            if (useRigidbody)
            {
                _rigidbodyController.SetControlEnabled(controlEnabled);
                return;
            }

            _simulatedController.SetControlEnabled(controlEnabled);
        }

        private void ResetPlayer(bool useRigidbody)
        {
            if (useRigidbody)
            {
                _rigidbodyController.ResetLocomotionRuntimeState();
                return;
            }

            _simulatedController.ResetLocomotionRuntimeState();
        }

        private void ApplyPlayerColors()
        {
            ApplyPlayerColor(_rigidbodyController, _rigidbodyColor);
            ApplyPlayerColor(_simulatedController, _simulatedColor);
        }

        private void ApplyPlayerColor(Component controller, Color color)
        {
            if (controller == null)
            {
                return;
            }

            SpriteRenderer spriteRenderer =
                controller.GetComponentInChildren<SpriteRenderer>(true);

            if (spriteRenderer != null)
            {
                spriteRenderer.color = color;
            }
        }

        private void DrawSwitchButton()
        {
            if (!_initialized || !_showDebugButton || !Application.isPlaying)
            {
                return;
            }

            Color previousBackgroundColor = GUI.backgroundColor;
            GUI.backgroundColor = _rigidbodyActive
                ? _simulatedColor
                : _rigidbodyColor;

            if (GUI.Button(_buttonRect, GetSwitchButtonLabel()))
            {
                TogglePlayer();
            }

            GUI.backgroundColor = previousBackgroundColor;
        }

        private string GetSwitchButtonLabel()
        {
            return _rigidbodyActive
                ? "Active: Green Rigidbody | Switch to Orange Simulated"
                : "Active: Orange Simulated | Switch to Green Rigidbody";
        }

#if UNITY_EDITOR
        //------------------------------- Editor -------------------------------//

        private void OnValidate()
        {
            _buttonRect.width = Mathf.Max(220f, _buttonRect.width);
            _buttonRect.height = Mathf.Max(30f, _buttonRect.height);

            if (!Application.isPlaying)
            {
                InitializeComponents();
            }
        }
#endif
    }
}
