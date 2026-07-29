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

        [Header("PRESENTATION PARAMETERS")]
        [SerializeField] private bool _showRuntimePanel = true;
        [SerializeField] private bool _showCoursePrompt = true;
        [SerializeField] private Rect _runtimePanelRect =
            new Rect(16f, 68f, 420f, 112f);
        [SerializeField] private Rect _coursePromptRect =
            new Rect(452f, 16f, 650f, 44f);

        [Header("COURSE GIZMO PARAMETERS")]
        [SerializeField] private bool _drawCourseGizmos = true;
        [SerializeField] private float _courseGizmoY = 4.75f;
        [SerializeField] private float _courseGizmoHeight = 0.5f;
        [SerializeField] private Vector2 _runwayRange = new Vector2(-32f, -24f);
        [SerializeField] private Vector2 _rollingSlopeRange = new Vector2(-24f, -8f);
        [SerializeField] private Vector2 _uphillJumpRange = new Vector2(-8f, 8f);
        [SerializeField] private Vector2 _lowCeilingRange = new Vector2(10f, 16f);
        [SerializeField] private Vector2 _jumpableStepRange = new Vector2(18f, 25f);
        [SerializeField] private Vector2 _steepSlopeRange = new Vector2(30f, 40f);

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
            DrawRuntimePanel();
            DrawCoursePrompt();
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
            Vector3 transferPositionVector =
                GetTransferPosition(outgoingPlayer, outgoingCollider);

            SetPlayerControl(!useRigidbody, false);
            SetPlayerActive(!useRigidbody, false);
            SetPlayerActive(useRigidbody, true);

            Transform incomingPlayer = GetPlayerTransform(useRigidbody);
            CapsuleCollider2D incomingCollider = GetPlayerCollider(useRigidbody);

            if (transferPosition)
            {
                AlignPlayerToTransferPosition(
                    incomingPlayer,
                    incomingCollider,
                    transferPositionVector);
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

        private void DrawRuntimePanel()
        {
            if (!_initialized || !_showRuntimePanel || !Application.isPlaying)
            {
                return;
            }

            LocomotionState state = GetActiveState();

            if (state == null)
            {
                return;
            }

            GUI.Box(_runtimePanelRect, string.Empty);
            Rect labelRect = new Rect(
                _runtimePanelRect.x + 10f,
                _runtimePanelRect.y + 8f,
                _runtimePanelRect.width - 20f,
                _runtimePanelRect.height - 16f);
            GUI.Label(labelRect, GetRuntimeStatusText(state));
        }

        private void DrawCoursePrompt()
        {
            if (!_initialized ||
                !_showCoursePrompt ||
                !Application.isPlaying ||
                _activePlayer == null)
            {
                return;
            }

            Color previousBackgroundColor = GUI.backgroundColor;
            GUI.backgroundColor = GetCurrentScenarioColor(_activePlayer.position.x);
            GUI.Box(
                _coursePromptRect,
                GetCurrentScenarioPrompt(_activePlayer.position.x));
            GUI.backgroundColor = previousBackgroundColor;
        }

        private LocomotionState GetActiveState()
        {
            return _rigidbodyActive
                ? _rigidbodyController.State
                : _simulatedController.State;
        }

        private Vector2 GetActiveVelocity()
        {
            return _rigidbodyActive
                ? _rigidbodyController.Velocity
                : _simulatedController.Velocity;
        }

        private string GetRuntimeStatusText(LocomotionState state)
        {
            Vector2 velocity = GetActiveVelocity();
            string implementation = _rigidbodyActive
                ? "GREEN RIGIDBODY"
                : "ORANGE SIMULATED";
            string jumpState = GetActiveJumpState();
            return
                $"{implementation}\n" +
                $"Velocity  X {velocity.x:0.00}  Y {velocity.y:0.00}\n" +
                $"Grounded {state.Grounded}  Slope {state.SlopeAngle:0.0}°  " +
                $"Jump {jumpState}\n" +
                $"State {state.CurrentState}";
        }

        private string GetActiveJumpState()
        {
            if (_rigidbodyActive)
            {
                return GetJumpState(
                    _rigidbodyController.Jump.IsInJumpAscentState,
                    _rigidbodyController.Jump.IsInAnticipationFrame,
                    _rigidbodyController.Jump.IsSteepSlopeJumpRestricted,
                    _rigidbodyController.Jump.HasBufferedJump);
            }

            return GetJumpState(
                _simulatedController.Jump.IsInJumpAscentState,
                _simulatedController.Jump.IsInAnticipationFrame,
                _simulatedController.Jump.IsSteepSlopeJumpRestricted,
                _simulatedController.Jump.HasBufferedJump);
        }

        private string GetJumpState(
            bool jumping,
            bool anticipating,
            bool steepSlopeRestricted,
            bool buffered)
        {
            if (steepSlopeRestricted)
            {
                return "STEEP BLOCK";
            }

            if (anticipating)
            {
                return "ANTICIPATION";
            }

            if (jumping)
            {
                return "ASCENDING";
            }

            return buffered ? "BUFFERED" : "READY";
        }

        private string GetCurrentScenarioPrompt(float positionX)
        {
            if (positionX < _runwayRange.y)
            {
                return "RUNWAY — accelerate, release input, and compare stopping distance";
            }

            if (positionX < _rollingSlopeRange.y)
            {
                return "ROLLING SLOPES — stop on each incline without drifting backward";
            }

            if (positionX < _uphillJumpRange.y)
            {
                return "UPHILL JUMP — jump while climbing and preserve upward momentum";
            }

            if (positionX < _lowCeilingRange.y)
            {
                return "CYAN CEILING — verify head collision and early jump release";
            }

            if (positionX < _jumpableStepRange.y)
            {
                return "YELLOW STEP — jump onto or clear the raised obstacle";
            }

            if (positionX < _steepSlopeRange.y)
            {
                return "STEEP END SLOPE — stop before the face and remain free to jump away";
            }

            return "END PLATFORM — turn around and repeat the course from the right";
        }

        private Color GetCurrentScenarioColor(float positionX)
        {
            if (positionX < _runwayRange.y)
            {
                return new Color(0.55f, 0.7f, 1f, 1f);
            }

            if (positionX < _rollingSlopeRange.y)
            {
                return new Color(0.35f, 1f, 0.55f, 1f);
            }

            if (positionX < _uphillJumpRange.y)
            {
                return new Color(0.2f, 0.9f, 1f, 1f);
            }

            if (positionX < _lowCeilingRange.y)
            {
                return Color.cyan;
            }

            if (positionX < _jumpableStepRange.y)
            {
                return new Color(1f, 0.8f, 0.05f, 1f);
            }

            return positionX < _steepSlopeRange.y
                ? new Color(1f, 0.25f, 0.2f, 1f)
                : new Color(0.7f, 0.7f, 0.7f, 1f);
        }

#if UNITY_EDITOR
        //------------------------------- Editor -------------------------------//

        private void OnDrawGizmos()
        {
            if (!_drawCourseGizmos)
            {
                return;
            }

            DrawCourseGizmo(
                _runwayRange,
                new Color(0.55f, 0.7f, 1f, 1f),
                "Acceleration / braking");
            DrawCourseGizmo(
                _rollingSlopeRange,
                new Color(0.35f, 1f, 0.55f, 1f),
                "Rolling slope stop");
            DrawCourseGizmo(
                _uphillJumpRange,
                new Color(0.2f, 0.9f, 1f, 1f),
                "Uphill jump");
            DrawCourseGizmo(
                _lowCeilingRange,
                Color.cyan,
                "Low ceiling");
            DrawCourseGizmo(
                _jumpableStepRange,
                new Color(1f, 0.8f, 0.05f, 1f),
                "Jumpable step");
            DrawCourseGizmo(
                _steepSlopeRange,
                new Color(1f, 0.25f, 0.2f, 1f),
                "Steep slope");
        }

        private void DrawCourseGizmo(
            Vector2 range,
            Color color,
            string label)
        {
            float width = Mathf.Max(0f, range.y - range.x);
            Vector3 center = new Vector3(
                (range.x + range.y) * 0.5f,
                _courseGizmoY,
                0f);
            Vector3 size = new Vector3(width, _courseGizmoHeight, 0.1f);
            Color fillColor = color;
            fillColor.a = 0.2f;
            Gizmos.color = fillColor;
            Gizmos.DrawCube(center, size);
            Gizmos.color = color;
            Gizmos.DrawWireCube(center, size);
            UnityEditor.Handles.Label(
                center + Vector3.up * (_courseGizmoHeight + 0.15f),
                label);
        }

        private void OnValidate()
        {
            _buttonRect.width = Mathf.Max(220f, _buttonRect.width);
            _buttonRect.height = Mathf.Max(30f, _buttonRect.height);
            _runtimePanelRect.width = Mathf.Max(280f, _runtimePanelRect.width);
            _runtimePanelRect.height = Mathf.Max(90f, _runtimePanelRect.height);
            _coursePromptRect.width = Mathf.Max(360f, _coursePromptRect.width);
            _coursePromptRect.height = Mathf.Max(30f, _coursePromptRect.height);
            _courseGizmoHeight = Mathf.Max(0.05f, _courseGizmoHeight);
            NormalizeRange(ref _runwayRange);
            NormalizeRange(ref _rollingSlopeRange);
            NormalizeRange(ref _uphillJumpRange);
            NormalizeRange(ref _lowCeilingRange);
            NormalizeRange(ref _jumpableStepRange);
            NormalizeRange(ref _steepSlopeRange);

            if (!Application.isPlaying)
            {
                InitializeComponents();
            }
        }

        private void NormalizeRange(ref Vector2 range)
        {
            if (range.x <= range.y)
            {
                return;
            }

            float minimum = range.y;
            range.y = range.x;
            range.x = minimum;
        }
#endif
    }
}
