using Portfolio.Input;
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

        private const float ControlsPanelHeight = 154f;
        private const float PhysicsSummaryCollapsedHeight = 104f;
        private const float PhysicsSummaryExpandedHeight = 236f;
        private const float ScenarioPromptHeight = 46f;
        private const float PanelPadding = 12f;
        private const float HeaderHeight = 28f;
        private const float ControlRowHeight = 22f;

        [Header("COMPONENTS")]
        [SerializeField] private PlayerController _rigidbodyController;
        [SerializeField] private SimulatedSplineLocomotionController _simulatedController;
        [SerializeField] private BasicCameraFollow2D _cameraFollow;
        [SerializeField] private GameInput _gameInput;

        [Header("DISPLAY PARAMETERS")]
        [SerializeField] private bool _startWithRigidbody = true;
        [SerializeField] private Color _rigidbodyColor = new Color(0.15f, 1f, 0f, 1f);
        [SerializeField] private Color _simulatedColor = new Color(1f, 0.55f, 0.2f, 1f);

        [Header("PRESENTATION PARAMETERS")]
        [SerializeField] private bool _showControlsPanel = true;
        [SerializeField] private bool _showPhysicsSummary = true;
        [SerializeField] private bool _showCoursePrompt = true;
        [SerializeField] private bool _showPlayerPhysicsLabel = true;
        [SerializeField] private bool _physicsSummaryStartsExpanded;
        [SerializeField] private float _screenMargin = 16f;
        [SerializeField] private float _controlsPanelWidth = 320f;
        [SerializeField] private float _physicsPanelWidth = 370f;
        [SerializeField] private float _playerLabelWidth = 220f;
        [SerializeField] private float _playerLabelHeight = 30f;
        [SerializeField] private Vector2 _playerLabelWorldOffset =
            new Vector2(0f, 0.3f);

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
        private Camera _presentationCamera;
        private GUIStyle _panelStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _captionStyle;
        private GUIStyle _controlBindingStyle;
        private GUIStyle _foldoutStyle;
        private GUIStyle _scenarioStyle;
        private GUIStyle _playerLabelStyle;

        public Transform ActivePlayer => _activePlayer;
        public bool RigidbodyActive => _rigidbodyActive;

        #endregion

        #region Flags

        private bool _initialized;
        private bool _rigidbodyActive;
        private bool _physicsSummaryExpanded;
        private bool _guiStylesInitialized;

        #endregion

        //------------------------------- Unity Methods -------------------------------//

        #region Unity

        private void Start()
        {
            Initialize();
        }

        private void OnGUI()
        {
            if (!_initialized || !Application.isPlaying)
            {
                return;
            }

            InitializeGuiStyles();
            DrawControlsPanel();
            DrawPhysicsSummary();
            DrawCoursePrompt();
            DrawPlayerPhysicsLabel();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
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
            _physicsSummaryExpanded = _physicsSummaryStartsExpanded;
            SetActivePlayer(_startWithRigidbody, false);
            _initialized = true;
        }

        private void InitializeModules()
        {
        }

        private void InitializeComponents()
        {
            Camera mainCamera = Camera.main;

            if (_cameraFollow == null && mainCamera != null)
            {
                _cameraFollow = mainCamera.GetComponent<BasicCameraFollow2D>();
            }

            if (_gameInput == null)
            {
                _gameInput = GameInput.Instance;
            }

            _presentationCamera = _cameraFollow != null
                ? _cameraFollow.GetComponent<Camera>()
                : mainCamera;
        }

        private void InitializeEvents()
        {
            UnsubscribeEvents();

            if (_gameInput != null)
            {
                _gameInput.PhysicsSwitchRequested += TogglePlayer;
            }
        }

        private void UnsubscribeEvents()
        {
            if (_gameInput != null)
            {
                _gameInput.PhysicsSwitchRequested -= TogglePlayer;
            }
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

        private void InitializeGuiStyles()
        {
            if (_guiStylesInitialized)
            {
                return;
            }

            _panelStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(
                    (int)PanelPadding,
                    (int)PanelPadding,
                    (int)PanelPadding,
                    (int)PanelPadding)
            };
            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
            _headerStyle.normal.textColor = Color.white;
            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 12,
                wordWrap = true
            };
            _bodyStyle.normal.textColor = new Color(0.92f, 0.95f, 1f, 1f);
            _captionStyle = new GUIStyle(_bodyStyle)
            {
                alignment = TextAnchor.MiddleRight
            };
            _captionStyle.normal.textColor = new Color(0.72f, 0.76f, 0.82f, 1f);
            _controlBindingStyle = new GUIStyle(_bodyStyle)
            {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold
            };
            _controlBindingStyle.normal.textColor =
                new Color(0.65f, 0.85f, 1f, 1f);
            _foldoutStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(10, 10, 4, 4)
            };
            _foldoutStyle.normal.textColor = Color.black;
            _foldoutStyle.hover.textColor = Color.black;
            _foldoutStyle.active.textColor = Color.black;
            _scenarioStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            _scenarioStyle.normal.textColor = new Color(0.04f, 0.05f, 0.07f, 1f);
            _playerLabelStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
            _playerLabelStyle.normal.textColor = Color.black;
            _guiStylesInitialized = true;
        }

        private void DrawControlsPanel()
        {
            if (!_showControlsPanel)
            {
                return;
            }

            Rect panelRect = GetControlsPanelRect();
            DrawTintedBox(panelRect, new Color(0.08f, 0.11f, 0.17f, 0.95f));
            GUI.Label(
                new Rect(
                    panelRect.x + PanelPadding,
                    panelRect.y + PanelPadding,
                    panelRect.width - PanelPadding * 2f,
                    HeaderHeight),
                "CONTROLS",
                _headerStyle);
            DrawControlRow(
                panelRect,
                0,
                "A / D  ·  Left Stick",
                "Move");
            DrawControlRow(
                panelRect,
                1,
                "Space  ·  South",
                "Jump");
            DrawControlRow(
                panelRect,
                2,
                "Tab  ·  Select",
                "Switch Physics");
            DrawControlRow(
                panelRect,
                3,
                "Esc  ·  Start",
                "Pause");
        }

        private void DrawControlRow(
            Rect panelRect,
            int rowIndex,
            string bindings,
            string action)
        {
            float rowY =
                panelRect.y +
                PanelPadding +
                HeaderHeight +
                rowIndex * ControlRowHeight;
            float contentWidth = panelRect.width - PanelPadding * 2f;
            float bindingWidth = contentWidth * 0.63f;
            Rect bindingRect = new Rect(
                panelRect.x + PanelPadding,
                rowY,
                bindingWidth,
                ControlRowHeight);
            Rect actionRect = new Rect(
                bindingRect.xMax,
                rowY,
                contentWidth - bindingWidth,
                ControlRowHeight);
            GUI.Label(bindingRect, bindings, _controlBindingStyle);
            GUI.Label(actionRect, action, _captionStyle);
        }

        private void DrawPhysicsSummary()
        {
            if (!_showPhysicsSummary)
            {
                return;
            }

            LocomotionState state = GetActiveState();

            if (state == null)
            {
                return;
            }

            Rect panelRect = GetPhysicsSummaryRect();
            DrawTintedBox(panelRect, new Color(0.08f, 0.11f, 0.17f, 0.95f));
            Rect headerRect = new Rect(
                panelRect.x + PanelPadding,
                panelRect.y + PanelPadding,
                panelRect.width - PanelPadding * 2f,
                HeaderHeight);
            Color previousBackgroundColor = GUI.backgroundColor;
            GUI.backgroundColor = GetActivePhysicsColor();

            if (GUI.Button(
                headerRect,
                _physicsSummaryExpanded
                    ? "▼  PHYSICS SUMMARY"
                    : "▶  PHYSICS SUMMARY",
                _foldoutStyle))
            {
                _physicsSummaryExpanded = !_physicsSummaryExpanded;
            }

            GUI.backgroundColor = previousBackgroundColor;
            Rect detailsRect = new Rect(
                panelRect.x + PanelPadding,
                headerRect.yMax + 8f,
                panelRect.width - PanelPadding * 2f,
                panelRect.height - HeaderHeight - PanelPadding * 2f - 8f);
            GUI.Label(
                detailsRect,
                _physicsSummaryExpanded
                    ? GetExpandedPhysicsText(state)
                    : GetCollapsedPhysicsText(state),
                _bodyStyle);
        }

        private void DrawCoursePrompt()
        {
            if (!_showCoursePrompt || _activePlayer == null)
            {
                return;
            }

            Rect promptRect = GetCoursePromptRect();
            Color previousBackgroundColor = GUI.backgroundColor;
            GUI.backgroundColor = GetCurrentScenarioColor(_activePlayer.position.x);
            GUI.Box(
                promptRect,
                GetCurrentScenarioPrompt(_activePlayer.position.x),
                _scenarioStyle);
            GUI.backgroundColor = previousBackgroundColor;
        }

        private void DrawPlayerPhysicsLabel()
        {
            if (!_showPlayerPhysicsLabel || _activePlayer == null)
            {
                return;
            }

            if (_presentationCamera == null)
            {
                _presentationCamera = Camera.main;
            }

            if (_presentationCamera == null)
            {
                return;
            }

            CapsuleCollider2D playerCollider =
                GetPlayerCollider(_rigidbodyActive);
            Vector3 labelPosition = _activePlayer.position;

            if (playerCollider != null)
            {
                Bounds bounds = playerCollider.bounds;
                labelPosition = new Vector3(
                    bounds.center.x,
                    bounds.max.y,
                    _activePlayer.position.z);
            }

            labelPosition += (Vector3)_playerLabelWorldOffset;
            Vector3 screenPosition =
                _presentationCamera.WorldToScreenPoint(labelPosition);

            if (screenPosition.z <= 0f)
            {
                return;
            }

            float labelWidth = Mathf.Min(
                _playerLabelWidth,
                Screen.width - _screenMargin * 2f);
            float labelX = Mathf.Clamp(
                screenPosition.x - labelWidth * 0.5f,
                _screenMargin,
                Screen.width - _screenMargin - labelWidth);
            float labelY = Mathf.Clamp(
                Screen.height - screenPosition.y - _playerLabelHeight,
                _screenMargin,
                Screen.height - _screenMargin - _playerLabelHeight);
            Rect labelRect = new Rect(
                labelX,
                labelY,
                labelWidth,
                _playerLabelHeight);
            Color previousBackgroundColor = GUI.backgroundColor;
            GUI.backgroundColor = GetActivePhysicsColor();
            GUI.Box(labelRect, GetActivePhysicsLabel(), _playerLabelStyle);
            GUI.backgroundColor = previousBackgroundColor;
        }

        private void DrawTintedBox(Rect rect, Color color)
        {
            Color previousBackgroundColor = GUI.backgroundColor;
            GUI.backgroundColor = color;
            GUI.Box(rect, GUIContent.none, _panelStyle);
            GUI.backgroundColor = previousBackgroundColor;
        }

        private Rect GetControlsPanelRect()
        {
            float width = Mathf.Min(
                _controlsPanelWidth,
                Mathf.Max(220f, Screen.width - _screenMargin * 2f));
            return new Rect(
                _screenMargin,
                _screenMargin,
                width,
                ControlsPanelHeight);
        }

        private Rect GetPhysicsSummaryRect()
        {
            float width = Mathf.Min(
                _physicsPanelWidth,
                Mathf.Max(260f, Screen.width - _screenMargin * 2f));
            bool stackPanels =
                Screen.width <
                _controlsPanelWidth +
                _physicsPanelWidth +
                _screenMargin * 3f;
            float y = stackPanels
                ? GetControlsPanelRect().yMax + _screenMargin
                : _screenMargin;
            float height = _physicsSummaryExpanded
                ? PhysicsSummaryExpandedHeight
                : PhysicsSummaryCollapsedHeight;
            return new Rect(
                Screen.width - _screenMargin - width,
                y,
                width,
                height);
        }

        private Rect GetCoursePromptRect()
        {
            Rect controlsRect = GetControlsPanelRect();
            Rect physicsRect = GetPhysicsSummaryRect();
            float left = controlsRect.xMax + _screenMargin;
            float right = physicsRect.x - _screenMargin;
            float availableWidth = right - left;

            if (physicsRect.y == _screenMargin && availableWidth >= 360f)
            {
                return new Rect(
                    left,
                    _screenMargin,
                    availableWidth,
                    ScenarioPromptHeight);
            }

            return new Rect(
                _screenMargin,
                Mathf.Max(controlsRect.yMax, physicsRect.yMax) + _screenMargin,
                Screen.width - _screenMargin * 2f,
                ScenarioPromptHeight);
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

        private string GetCollapsedPhysicsText(LocomotionState state)
        {
            Vector2 velocity = GetActiveVelocity();
            return
                $"{GetActivePhysicsLabel()}\n" +
                $"{GetSurfaceSummary(state)}  ·  Jump {GetActiveJumpState()}\n" +
                $"Velocity  X {velocity.x:0.00}  Y {velocity.y:0.00}";
        }

        private string GetExpandedPhysicsText(LocomotionState state)
        {
            Vector2 velocity = GetActiveVelocity();
            float movementInput = _gameInput != null
                ? _gameInput.MovementValue.x
                : 0f;
            return
                $"{GetActivePhysicsLabel()}\n" +
                $"Motion  {GetActiveMotionDescription()}\n" +
                $"Collision  {GetActiveCollisionDescription()}\n" +
                $"Surface  {GetSurfaceSummary(state)}\n" +
                $"Normal  X {state.SurfaceNormal.x:0.00}  " +
                $"Y {state.SurfaceNormal.y:0.00}\n" +
                $"Velocity  X {velocity.x:0.00}  Y {velocity.y:0.00}\n" +
                $"Input  X {movementInput:0.00}  Jump {GetActiveJumpState()}\n" +
                $"State  {state.CurrentState}";
        }

        private string GetActivePhysicsLabel()
        {
            return _rigidbodyActive
                ? "RIGIDBODY PHYSICS"
                : "SIMULATED PHYSICS";
        }

        private string GetActiveMotionDescription()
        {
            return _rigidbodyActive
                ? "Rigidbody2D velocity + Unity gravity"
                : "Transform.Translate + calculated gravity";
        }

        private string GetActiveCollisionDescription()
        {
            return _rigidbodyActive
                ? "Dynamic Rigidbody2D + capsule"
                : "Kinematic contacts + capsule sweeps";
        }

        private string GetSurfaceSummary(LocomotionState state)
        {
            if (state.OnSteepSlope)
            {
                return $"Steep slope  {state.SlopeAngle:0.0}°";
            }

            if (state.OnWalkableSlope)
            {
                return $"Walkable slope  {state.SlopeAngle:0.0}°";
            }

            if (state.Grounded)
            {
                return "Grounded";
            }

            if (state.Rising)
            {
                return "Airborne · Rising";
            }

            return state.Falling ? "Airborne · Falling" : "Airborne";
        }

        private Color GetActivePhysicsColor()
        {
            return _rigidbodyActive
                ? _rigidbodyColor
                : _simulatedColor;
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
            _screenMargin = Mathf.Max(8f, _screenMargin);
            _controlsPanelWidth = Mathf.Max(220f, _controlsPanelWidth);
            _physicsPanelWidth = Mathf.Max(260f, _physicsPanelWidth);
            _playerLabelWidth = Mathf.Max(140f, _playerLabelWidth);
            _playerLabelHeight = Mathf.Max(24f, _playerLabelHeight);
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
