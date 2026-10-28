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


        [Header("COMPONENTS")]
        [SerializeField] private PlayerRigidbodyController _rigidbodyController;
        [SerializeField] private PlayerSimulatedController _simulatedController;
        [SerializeField] private BasicCameraFollow2D _cameraFollow;
        [SerializeField] private GameInput _gameInput;

        [Header("DISPLAY PARAMETERS")]
        [SerializeField] private bool _startWithRigidbody = true;
        [SerializeField] private Color _rigidbodyColor = new Color(0.15f, 1f, 0f, 1f);
        [SerializeField] private Color _simulatedColor = new Color(1f, 0.55f, 0.2f, 1f);
        [SerializeField] private bool _debugTintCharacterSprites = true;
        [SerializeField, Range(0f, 1f)] private float _debugSpriteTintStrength = 0.22f;

        [Header("PRESENTATION PARAMETERS")]
        [SerializeField] private bool _showControlsPanel = true;
        [SerializeField] private bool _showPhysicsSummary = true;
        [SerializeField] private bool _showCoursePrompt = true;
        [SerializeField] private bool _showPlayerPhysicsLabel = true;
        [SerializeField] private bool _physicsSummaryStartsExpanded;
        [SerializeField] private float _screenMargin = 16f;
        [SerializeField] private float _controlsPanelWidth = 360f;
        [SerializeField] private float _physicsPanelWidth = 440f;
        [SerializeField] private float _playerLabelWidth = 240f;
        [SerializeField] private float _playerLabelHeight = 32f;
        [SerializeField] private Vector2 _playerLabelWorldOffset =
            new Vector2(0f, 0.3f);

        [Header("HUD LAYOUT")]
        [SerializeField, Min(1f)] private float _controlsPanelHeight = 242f;
        [SerializeField, Min(1f)] private float _physicsSummaryCollapsedHeight = 148f;
        [SerializeField, Min(1f)] private float _physicsSummaryExpandedHeight = 390f;
        [SerializeField, Min(1f)] private float _scenarioPromptHeight = 78f;
        [SerializeField, Min(0f)] private float _panelPadding = 14f;
        [SerializeField, Min(1f)] private float _headerHeight = 34f;
        [SerializeField, Min(1f)] private float _controlRowHeight = 29f;

        [Header("HUD FONT SIZES")]
        [SerializeField, Min(1)] private int _headerFontSize = 17;
        [SerializeField, Min(1)] private int _bodyFontSize = 14;
        [SerializeField, Min(1)] private int _captionFontSize = 12;
        [SerializeField, Min(1)] private int _controlBindingFontSize = 13;
        [SerializeField, Min(1)] private int _foldoutFontSize = 14;
        [SerializeField, Min(1)] private int _scenarioFontSize = 14;
        [SerializeField, Min(1)] private int _sectionFontSize = 12;
        [SerializeField, Min(1)] private int _promptTitleFontSize = 14;
        [SerializeField, Min(1)] private int _promptBodyFontSize = 13;
        [SerializeField, Min(1)] private int _playerLabelFontSize = 14;
        [SerializeField, Min(1)] private int _courseGizmoFontSize = 14;

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
        private GUIStyle _sectionStyle;
        private GUIStyle _promptTitleStyle;
        private GUIStyle _promptBodyStyle;
        private Texture2D _panelTexture;
        private Texture2D _accentTexture;
        private Texture2D _scenarioTexture;
        private SpriteRenderer[] _rigidbodySpriteRenderers;
        private SpriteRenderer[] _simulatedSpriteRenderers;
        private Color[] _rigidbodyOriginalSpriteColors;
        private Color[] _simulatedOriginalSpriteColors;
        private bool _spriteColorsCached;

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

        private void LateUpdate()
        {
            ApplyPlayerColors();
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
            DestroyGuiTexture(_panelTexture);
            DestroyGuiTexture(_accentTexture);
            DestroyGuiTexture(_scenarioTexture);
        }

        private void DestroyGuiTexture(Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(texture);
            }
            else
            {
                DestroyImmediate(texture);
            }
        }

        #endregion

        //------------------------------- Initialization Methods -------------------------------//

        #region Initialization

        private void Initialize()
        {
            InitializeComponents();
            InitializeEvents();
            _physicsSummaryExpanded = _physicsSummaryStartsExpanded;
            SetActivePlayer(_startWithRigidbody, false);
            ApplyPlayerColors();
            _initialized = true;
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
            float facingDirection = !_initialized
                ? (useRigidbody
                    ? _rigidbodyController.FacingDirection
                    : _simulatedController.FacingDirection)
                : (_rigidbodyActive
                    ? _rigidbodyController.FacingDirection
                    : _simulatedController.FacingDirection);

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
            if (useRigidbody)
            {
                _rigidbodyController.SetFacingDirection(facingDirection);
            }
            else
            {
                _simulatedController.SetFacingDirection(facingDirection);
            }

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
            if (!_spriteColorsCached ||
                (_rigidbodySpriteRenderers.Length == 0 && _simulatedSpriteRenderers.Length == 0))
            {
                Transform rigidbodyVisualRoot = _rigidbodyController != null
                    ? _rigidbodyController.VisualRoot
                    : null;
                Transform simulatedVisualRoot = _simulatedController != null
                    ? _simulatedController.VisualRoot
                    : null;
                _rigidbodySpriteRenderers = rigidbodyVisualRoot != null
                    ? rigidbodyVisualRoot.GetComponentsInChildren<SpriteRenderer>(true)
                    : System.Array.Empty<SpriteRenderer>();
                _simulatedSpriteRenderers = simulatedVisualRoot != null
                    ? simulatedVisualRoot.GetComponentsInChildren<SpriteRenderer>(true)
                    : System.Array.Empty<SpriteRenderer>();
                _rigidbodyOriginalSpriteColors = CacheSpriteColors(_rigidbodySpriteRenderers);
                _simulatedOriginalSpriteColors = CacheSpriteColors(_simulatedSpriteRenderers);
                _spriteColorsCached = true;
            }

            ApplyPlayerColor(_rigidbodySpriteRenderers, _rigidbodyOriginalSpriteColors, _rigidbodyColor);
            ApplyPlayerColor(_simulatedSpriteRenderers, _simulatedOriginalSpriteColors, _simulatedColor);
        }

        private Color[] CacheSpriteColors(SpriteRenderer[] spriteRenderers)
        {
            Color[] colors = new Color[spriteRenderers.Length];

            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                colors[i] = spriteRenderers[i] != null ? spriteRenderers[i].color : Color.white;
            }

            return colors;
        }

        private void ApplyPlayerColor(
            SpriteRenderer[] spriteRenderers,
            Color[] originalColors,
            Color tintColor)
        {
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                SpriteRenderer spriteRenderer = spriteRenderers[i];

                if (spriteRenderer == null)
                {
                    continue;
                }

                Color originalColor = originalColors[i];
                spriteRenderer.color = _debugTintCharacterSprites
                    ? Color.Lerp(originalColor, new Color(tintColor.r, tintColor.g, tintColor.b, originalColor.a), _debugSpriteTintStrength)
                    : originalColor;
            }
        }

        private void InitializeGuiStyles()
        {
            if (_guiStylesInitialized)
            {
                return;
            }

            _panelTexture = CreateTexture(new Color(0.035f, 0.05f, 0.075f, 0.96f));
            _accentTexture = CreateTexture(new Color(0.14f, 0.2f, 0.29f, 1f));
            _scenarioTexture = CreateTexture(Color.white);
            _panelStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(
                    (int)_panelPadding,
                    (int)_panelPadding,
                    (int)_panelPadding,
                    (int)_panelPadding),
                border = new RectOffset(8, 8, 8, 8),
                normal = { background = _panelTexture }
            };
            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = _headerFontSize,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Clip
            };
            _headerStyle.normal.textColor = Color.white;
            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = _bodyFontSize,
                wordWrap = true
            };
            _bodyStyle.normal.textColor = new Color(0.88f, 0.91f, 0.96f, 1f);
            _bodyStyle.padding = new RectOffset(2, 2, 2, 2);
            _captionStyle = new GUIStyle(_bodyStyle)
            {
                alignment = TextAnchor.MiddleRight
            };
            _captionStyle.normal.textColor = new Color(0.62f, 0.69f, 0.79f, 1f);
            _captionStyle.fontSize = _captionFontSize;
            _controlBindingStyle = new GUIStyle(_bodyStyle)
            {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold
            };
            _controlBindingStyle.normal.textColor =
                new Color(0.76f, 0.87f, 1f, 1f);
            _controlBindingStyle.fontSize = _controlBindingFontSize;
            _foldoutStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = _foldoutFontSize,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(10, 10, 4, 4)
            };
            _foldoutStyle.normal.background = _accentTexture;
            _foldoutStyle.hover.background = _accentTexture;
            _foldoutStyle.active.background = _accentTexture;
            _foldoutStyle.normal.textColor = Color.white;
            _foldoutStyle.hover.textColor = Color.white;
            _foldoutStyle.active.textColor = Color.white;
            _scenarioStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = _scenarioFontSize,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                padding = new RectOffset(14, 14, 8, 8),
                normal = { background = _panelTexture }
            };
            _scenarioStyle.normal.textColor = Color.white;
            _sectionStyle = new GUIStyle(_captionStyle)
            {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold,
                fontSize = _sectionFontSize
            };
            _promptTitleStyle = new GUIStyle(_headerStyle)
            {
                fontSize = _promptTitleFontSize,
                alignment = TextAnchor.MiddleLeft
            };
            _promptBodyStyle = new GUIStyle(_bodyStyle)
            {
                fontSize = _promptBodyFontSize,
                wordWrap = true
            };
            _playerLabelStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = _playerLabelFontSize,
                fontStyle = FontStyle.Bold
            };
            _playerLabelStyle.normal.background = _panelTexture;
            _playerLabelStyle.normal.textColor = Color.white;
            _guiStylesInitialized = true;
        }

        private void ApplyGuiFontSizes()
        {
            if (_headerStyle != null) _headerStyle.fontSize = _headerFontSize;
            if (_bodyStyle != null) _bodyStyle.fontSize = _bodyFontSize;
            if (_captionStyle != null) _captionStyle.fontSize = _captionFontSize;
            if (_controlBindingStyle != null) _controlBindingStyle.fontSize = _controlBindingFontSize;
            if (_foldoutStyle != null) _foldoutStyle.fontSize = _foldoutFontSize;
            if (_scenarioStyle != null) _scenarioStyle.fontSize = _scenarioFontSize;
            if (_sectionStyle != null) _sectionStyle.fontSize = _sectionFontSize;
            if (_promptTitleStyle != null) _promptTitleStyle.fontSize = _promptTitleFontSize;
            if (_promptBodyStyle != null) _promptBodyStyle.fontSize = _promptBodyFontSize;
            if (_playerLabelStyle != null) _playerLabelStyle.fontSize = _playerLabelFontSize;
        }

        private void DrawControlsPanel()
        {
            if (!_showControlsPanel)
            {
                return;
            }

            Rect panelRect = GetControlsPanelRect();
            DrawTintedBox(panelRect, new Color(0.035f, 0.05f, 0.075f, 0.96f));
            GUI.Label(
                new Rect(
                    panelRect.x + _panelPadding,
                    panelRect.y + _panelPadding,
                    panelRect.width - _panelPadding * 2f,
                    _headerHeight),
                "CONTROLS  ·  " + GetCurrentControlSchemeLabel(),
                _headerStyle);
            DrawControlRow(panelRect, 0, GetControlBinding("Move"), "Move");
            DrawControlRow(panelRect, 1, GetControlBinding("Jump"), "Jump");
            DrawControlRow(panelRect, 2, GetControlBinding("Switch Physics"), "Switch Physics");
            DrawControlRow(panelRect, 3, GetControlBinding("Attack"), "Attack");
            DrawControlRow(panelRect, 4, GetControlBinding("Look Up"), "Look Up");
            DrawControlRow(panelRect, 5, GetControlBinding("Pause"), "Pause");

            string unassignedButtonLabel = GetUnassignedFaceButtonLabel();
            if (!string.IsNullOrEmpty(unassignedButtonLabel))
            {
                GUI.Label(
                    new Rect(
                        panelRect.x + _panelPadding,
                        panelRect.y + _panelPadding + _headerHeight + 6f * _controlRowHeight,
                        panelRect.width - _panelPadding * 2f,
                        18f),
                    unassignedButtonLabel,
                    _sectionStyle);
            }
        }

        private string GetUnassignedFaceButtonLabel()
        {
            string device = _gameInput != null
                ? _gameInput.GetCurrentDevice()
                : GameInput.KEYBOARD;

            if (device == GameInput.DUALSENSE || device == GameInput.DUALSHOCK)
            {
                return "Circle · Unassigned";
            }

            if (device == GameInput.GAMEPAD)
            {
                return "East Button · Unassigned";
            }

            return string.Empty;
        }

        private string GetCurrentControlSchemeLabel()
        {
            string device = _gameInput != null
                ? _gameInput.GetCurrentDevice()
                : GameInput.KEYBOARD;

            if (device == GameInput.MOUSE || device == GameInput.KEYBOARD)
            {
                return "KEYBOARD & MOUSE";
            }

            return device.ToUpperInvariant();
        }

        private string GetControlBinding(string action)
        {
            string device = _gameInput != null
                ? _gameInput.GetCurrentDevice()
                : GameInput.KEYBOARD;
            bool keyboardMouse = device == GameInput.MOUSE || device == GameInput.KEYBOARD;

            if (keyboardMouse)
            {
                switch (action)
                {
                    case "Move": return "A / D";
                    case "Jump": return "Space";
                    case "Switch Physics": return "Tab";
                    case "Attack": return "Left Mouse";
                    case "Look Up": return "W";
                    case "Pause": return "Esc";
                }
            }

            if (device == GameInput.DUALSENSE || device == GameInput.DUALSHOCK)
            {
                switch (action)
                {
                    case "Move": return "Left Stick / D-pad";
                    case "Jump": return "X";
                    case "Switch Physics": return "Triangle";
                    case "Attack": return "Square";
                    case "Look Up": return "Right Stick ↑";
                    case "Pause": return device == GameInput.DUALSENSE ? "Options" : "Options / Start";
                }
            }

            switch (action)
            {
                case "Move": return "Left Stick / D-pad";
                case "Jump": return "South Button";
                case "Switch Physics": return "North Button";
                case "Attack": return "West Button";
                case "Look Up": return "Right Stick ↑";
                case "Pause": return "Start Button";
                default: return string.Empty;
            }
        }

        private void DrawControlRow(
            Rect panelRect,
            int rowIndex,
            string bindings,
            string action)
        {
            float rowY =
                panelRect.y +
                _panelPadding +
                _headerHeight +
                rowIndex * _controlRowHeight;
            float contentWidth = panelRect.width - _panelPadding * 2f;
            float bindingWidth = contentWidth * 0.63f;
            Rect bindingRect = new Rect(
                panelRect.x + _panelPadding,
                rowY,
                bindingWidth,
                _controlRowHeight);
            Rect actionRect = new Rect(
                bindingRect.xMax,
                rowY,
                contentWidth - bindingWidth,
                _controlRowHeight);
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
            DrawTintedBox(panelRect, new Color(0.035f, 0.05f, 0.075f, 0.96f));
            Rect headerRect = new Rect(
                panelRect.x + _panelPadding,
                panelRect.y + _panelPadding,
                panelRect.width - _panelPadding * 2f,
                _headerHeight);
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
                panelRect.x + _panelPadding,
                headerRect.yMax + 8f,
                panelRect.width - _panelPadding * 2f,
                panelRect.height - _headerHeight - _panelPadding * 2f - 8f);
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
            float positionX = _activePlayer.position.x;
            Color scenarioColor = GetCurrentScenarioColor(positionX);
            DrawTintedBox(promptRect, new Color(0.035f, 0.05f, 0.075f, 0.96f));
            GUI.color = scenarioColor;
            GUI.DrawTexture(
                new Rect(promptRect.x, promptRect.y, 4f, promptRect.height),
                _scenarioTexture);
            GUI.color = Color.white;
            GUI.Label(
                new Rect(promptRect.x + 16f, promptRect.y + 7f, promptRect.width - 28f, 18f),
                "CURRENT TEST  /  " + GetCurrentScenarioName(positionX),
                _promptTitleStyle);
            GUI.Label(
                new Rect(promptRect.x + 16f, promptRect.y + 27f, promptRect.width - 28f, promptRect.height - 32f),
                GetCurrentScenarioPrompt(positionX),
                _promptBodyStyle);
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

        private Texture2D CreateTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private string GetCurrentScenarioName(float positionX)
        {
            if (positionX < _runwayRange.y) return "ACCELERATION & BRAKING";
            if (positionX < _rollingSlopeRange.y) return "SLOPE CONTROL";
            if (positionX < _uphillJumpRange.y) return "UPHILL JUMP";
            if (positionX < _lowCeilingRange.y) return "LOW CEILING";
            if (positionX < _jumpableStepRange.y) return "JUMPABLE STEP";
            if (positionX < _steepSlopeRange.y) return "STEEP SLOPE";
            return "COURSE REPEAT";
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
                _controlsPanelHeight);
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
                ? _physicsSummaryExpandedHeight
                : _physicsSummaryCollapsedHeight;
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
                    _scenarioPromptHeight);
            }

            return new Rect(
                _screenMargin,
                Mathf.Max(controlsRect.yMax, physicsRect.yMax) + _screenMargin,
                Screen.width - _screenMargin * 2f,
                _scenarioPromptHeight);
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
            float movementInput = _rigidbodyActive
                ? _rigidbodyController.MovementInput
                : _simulatedController.MovementInput;
            return
                $"{GetActivePhysicsLabel()}\n" +
                $"{GetSurfaceSummary(state)}  ·  Slope {state.SlopeAngle:0.0}°\n" +
                $"Velocity  X {velocity.x:0.00}  Y {velocity.y:0.00}  ·  Speed {velocity.magnitude:0.00}\n" +
                $"Input {movementInput:0.00}  ·  Jump {GetActiveJumpState()}  ·  Control {(state.ControlEnabled ? "Enabled" : "Disabled")}";
        }

        private string GetExpandedPhysicsText(LocomotionState state)
        {
            Vector2 velocity = GetActiveVelocity();
            float movementInput = _rigidbodyActive
                ? _rigidbodyController.MovementInput
                : _simulatedController.MovementInput;
            float facingDirection = _rigidbodyActive
                ? _rigidbodyController.FacingDirection
                : _simulatedController.FacingDirection;
            float horizontalDistancePerFixedFrame = _rigidbodyActive
                ? _rigidbodyController.HorizontalDistancePerFixedFrame
                : _simulatedController.HorizontalDistancePerFixedFrame;
            float jumpBufferRemainingTime = _rigidbodyActive
                ? _rigidbodyController.Jump.JumpBufferRemainingTime
                : _simulatedController.Jump.JumpBufferRemainingTime;
            float coyoteRemainingTime = _rigidbodyActive
                ? _rigidbodyController.Jump.CoyoteRemainingTime
                : _simulatedController.Jump.CoyoteRemainingTime;

            return
                $"{GetActivePhysicsLabel()}\n" +
                $"Motion  {GetActiveMotionDescription()}\n" +
                $"Collision  {GetActiveCollisionDescription()}\n" +
                $"Surface  {GetSurfaceSummary(state)}  ·  Slope {state.SlopeAngle:0.0}°\n" +
                $"Normal  X {state.SurfaceNormal.x:0.00}  Y {state.SurfaceNormal.y:0.00}\n" +
                $"Velocity  X {velocity.x:0.00}  Y {velocity.y:0.00}  ·  Speed {velocity.magnitude:0.00}\n" +
                $"Input  {movementInput:0.00}  ·  Facing {facingDirection:0.0}  ·  ΔX/fixed {horizontalDistancePerFixedFrame:0.000}\n" +
                $"Jump {GetActiveJumpState()}  ·  Buffer {jumpBufferRemainingTime:0.000}s  ·  Coyote {coyoteRemainingTime:0.000}s\n" +
                $"{GetActiveGroundContactSummary()}\n" +
                $"Control {(state.ControlEnabled ? "Enabled" : "Disabled")}  ·  State {state.CurrentState}\n" +
                $"State transitions {state.TransitionCount}";
        }

        private string GetActiveGroundContactSummary()
        {
            if (_rigidbodyActive)
            {
                PlayerRigidbodyPhysics physics = _rigidbodyController.PlayerRigidbodyPhysics;
                string contactName = physics != null && physics.GroundContactObject != null
                    ? physics.GroundContactObject.name
                    : "None";
                float groundedTime = physics != null ? physics.GroundedTime : 0f;
                return $"Ground time {groundedTime:0.00}s  ·  Contact {contactName}";
            }

            Vector2 intended = _simulatedController.LastIntendedDisplacement;
            Vector2 resolved = _simulatedController.LastResolvedDisplacement;
            return
                $"Step intended  X {intended.x:0.000}  Y {intended.y:0.000}\n" +
                $"Step resolved  X {resolved.x:0.000}  Y {resolved.y:0.000}  ·  Steep-slope block {_simulatedController.MovementBlockedBySteepSlope}";
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
            GUIStyle labelStyle = new GUIStyle(UnityEditor.EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = _courseGizmoFontSize,
                normal = { textColor = Color.white },
                padding = new RectOffset(8, 8, 4, 4)
            };
            UnityEditor.Handles.color = color;
            UnityEditor.Handles.DrawLine(
                new Vector3(range.x, _courseGizmoY - _courseGizmoHeight, 0f),
                new Vector3(range.x, _courseGizmoY + _courseGizmoHeight, 0f));
            UnityEditor.Handles.DrawLine(
                new Vector3(range.y, _courseGizmoY - _courseGizmoHeight, 0f),
                new Vector3(range.y, _courseGizmoY + _courseGizmoHeight, 0f));
            UnityEditor.Handles.Label(
                center + Vector3.up * (_courseGizmoHeight + 0.2f),
                label,
                labelStyle);
        }

        private void OnValidate()
        {
            _screenMargin = Mathf.Max(8f, _screenMargin);
            _controlsPanelWidth = Mathf.Max(220f, _controlsPanelWidth);
            _physicsPanelWidth = Mathf.Max(260f, _physicsPanelWidth);
            _playerLabelWidth = Mathf.Max(140f, _playerLabelWidth);
            _playerLabelHeight = Mathf.Max(24f, _playerLabelHeight);
            _controlsPanelHeight = Mathf.Max(1f, _controlsPanelHeight);
            _physicsSummaryCollapsedHeight = Mathf.Max(1f, _physicsSummaryCollapsedHeight);
            _physicsSummaryExpandedHeight = Mathf.Max(
                _physicsSummaryCollapsedHeight,
                _physicsSummaryExpandedHeight);
            _scenarioPromptHeight = Mathf.Max(1f, _scenarioPromptHeight);
            _panelPadding = Mathf.Max(0f, _panelPadding);
            _headerHeight = Mathf.Max(1f, _headerHeight);
            _controlRowHeight = Mathf.Max(1f, _controlRowHeight);
            _headerFontSize = Mathf.Max(1, _headerFontSize);
            _bodyFontSize = Mathf.Max(1, _bodyFontSize);
            _captionFontSize = Mathf.Max(1, _captionFontSize);
            _controlBindingFontSize = Mathf.Max(1, _controlBindingFontSize);
            _foldoutFontSize = Mathf.Max(1, _foldoutFontSize);
            _scenarioFontSize = Mathf.Max(1, _scenarioFontSize);
            _sectionFontSize = Mathf.Max(1, _sectionFontSize);
            _promptTitleFontSize = Mathf.Max(1, _promptTitleFontSize);
            _promptBodyFontSize = Mathf.Max(1, _promptBodyFontSize);
            _playerLabelFontSize = Mathf.Max(1, _playerLabelFontSize);
            _courseGizmoFontSize = Mathf.Max(1, _courseGizmoFontSize);
            _courseGizmoHeight = Mathf.Max(0.05f, _courseGizmoHeight);

            if (_panelStyle != null)
            {
                _panelStyle.padding = new RectOffset(
                    Mathf.RoundToInt(_panelPadding),
                    Mathf.RoundToInt(_panelPadding),
                    Mathf.RoundToInt(_panelPadding),
                    Mathf.RoundToInt(_panelPadding));
            }

            ApplyGuiFontSizes();

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
