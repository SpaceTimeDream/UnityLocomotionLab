using Portfolio.Game;
using Portfolio.Input;
using Portfolio.Locomotion.Core;
using System;
using System.IO;
using UnityEngine;

namespace Portfolio.Locomotion.Rigidbody
{
    [RequireComponent(typeof(UnityEngine.Rigidbody2D))]
    [RequireComponent(typeof(CapsuleCollider2D))]
    public class PlayerController : MonoBehaviour
    {
        //------------------------------- Game Pause -------------------------------//

        public virtual void OnGameStateChanged(GameState newGameState)
        {
            if (newGameState == GameState.Paused)
            {
                FreezeMovement();
                return;
            }

            UnFreezeMovement();
        }

        //------------------------------- Debugging Methods -------------------------------//

        //------------------------------- Parameters -------------------------------//

        #region Components

        [Header("COMPONENTS")]
        [SerializeField] private UnityEngine.Rigidbody2D _body;
        [SerializeField] private CapsuleCollider2D _capsuleCollider;
        [SerializeField] private Transform _visualRoot;
        [SerializeField] private GameInput _gameInput;

        [Header("PLAYER COMPONENTS")]
        [SerializeField] private PlayerHandler _playerHandler;
        [SerializeField] private PlayerJump _playerJump;
        [SerializeField] private PlayerMovement _playerMovement;
        [SerializeField] private PlayerPhysics _playerPhysics;
        

        [Header("MODULES")]
        [SerializeField] private LocomotionState _state = new LocomotionState();
        [SerializeField] private RigidbodyMovement _movement = new RigidbodyMovement();
        [SerializeField] private RigidbodyJump _jump = new RigidbodyJump();
        [SerializeField] private SurfaceProbe2D _surfaceProbe = new SurfaceProbe2D();
        [SerializeField] private ExternalForceReceiver2D _externalForces = new ExternalForceReceiver2D();
        [SerializeField] private MovingPlatformTracker2D _movingPlatforms = new MovingPlatformTracker2D();

        [Header("GRAVITY PARAMETERS")]
        [SerializeField] private float _gravityScale = 3.4f;
        [SerializeField] private float _jumpAscentGravityMultiplier = 1.1f;
        [SerializeField] private float _fallGravityMultiplier = 2.1f;
        [SerializeField] private float _maximumFallSpeed = 22f;

        [Header("SLOPE PARAMETERS")]
        [SerializeField] private float _steepSlopeJumpCheckDistance = 0.35f;

        [Header("GROUND ADHESION PARAMETERS")]
        [SerializeField] private float _groundAdhesionForce = 20f;
        [SerializeField] private bool _exportGroundAdhesionLog;
        [SerializeField] private float _groundAdhesionLogInterval = 0.05f;
        [SerializeField] private string _groundAdhesionLogPath;
        [SerializeField] private string _groundAdhesionLogError;

        [Header("GROUNDING DEBUG")]
        [SerializeField] private bool _debugGrounding;
        [SerializeField] private bool _pauseOnAirborne;
        [SerializeField] private bool _drawGroundingGizmos = true;
        [SerializeField] private float _groundingDebugLogInterval = 0.25f;

        [Header("FACING PARAMETERS")]
        [SerializeField] private bool _flipVisualWithFacing = true;

        private float _movementInput;
        private Vector2 _fixedStepStartPosition;
        private float _facingDirection = 1f;
        private float _horizontalDistancePerFixedFrame;
        private float _steepSlopeBlockDirection;
        private StreamWriter _groundAdhesionLogWriter;
        private float _groundAdhesionLogTimer;
        private string _lastGroundAdhesionDecision = "Not evaluated";
        private bool _lastGroundAdhesionApplied;
        private bool _wasGroundedForDebug;
        private bool _airbornePauseTriggered;
        private float _groundingDebugLogTimer;

        public UnityEngine.Rigidbody2D Body => _body;
        public CapsuleCollider2D MainCollider => _capsuleCollider;
        public Transform VisualRoot => _visualRoot;
        public GameInput Input => _gameInput;
        public LocomotionState State => _state;
        public RigidbodyMovement Movement => _movement;
        public RigidbodyJump Jump => _jump;
        public SurfaceProbe2D SurfaceProbe => _surfaceProbe;
        public ExternalForceReceiver2D ExternalForces => _externalForces;
        public MovingPlatformTracker2D MovingPlatforms => _movingPlatforms;
        public PlayerHandler PlayerHandler => _playerHandler;
        public PlayerJump PlayerJump => _playerJump;
        public PlayerMovement PlayerMovement => _playerMovement;
        public PlayerPhysics PlayerPhysics => _playerPhysics;
        
        public float MovementInput => _movementInput;
        public float FacingDirection => _facingDirection;
        public Vector2 Velocity => _body != null ? _body.linearVelocity : Vector2.zero;
        public float Speed => Velocity.magnitude;
        public float VerticalVelocity => Velocity.y;
        public float HorizontalDistancePerFixedFrame => _horizontalDistancePerFixedFrame;
        public float SteepSlopeBlockDirection => _steepSlopeBlockDirection;
        public string LastGroundAdhesionDecision => _lastGroundAdhesionDecision;
        public bool IsMovementFrozen => _playerHandler != null && _playerHandler.IsMovementFrozen;
        public bool IsScriptedControlActive => _playerHandler != null && _playerHandler.IsScriptedControlActive;

        public event Action<float> DirectionChanged;
        public event Action JumpTriggered;

        #endregion

        #region Flags

        private bool _initialized;
        private bool _movementFrozen => _playerHandler != null && _playerHandler.IsMovementFrozen;
        private bool _scriptedControlActive => _playerHandler != null && _playerHandler.IsScriptedControlActive;
        private Vector2 _scriptedMoveInput => _playerHandler != null ? _playerHandler.ScriptedMoveInput : Vector2.zero;

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
        }

        private void Update()
        {
            UpdateLoop();
        }

        private void FixedUpdate()
        {
            if (!_initialized || _movementFrozen) return;
            _playerPhysics.ApplyPlatformDelta();
            _playerPhysics.UpdatePhysics();
            _playerMovement.UpdateMovement();
            _playerJump.UpdateJump();
            _playerPhysics.FinalizePlatformSync();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
            SetMovementInput(0f);
            CloseGroundAdhesionLog();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
            CloseGroundAdhesionLog();
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

            InitializeModules();
            InitializeComponents();
            InitializeEvents();
            _initialized = true;
        }

        private void InitializeModules()
        {
            if (_gameInput == null)
            {
                _gameInput = GameInput.Instance;
            }

            InitializePlayerComponents();
            _playerHandler.Initialize(_state);
            _playerMovement.Initialize(this);
            _playerPhysics.Initialize(this);
            _playerJump.Initialize(this);
            _surfaceProbe.Initialize();
        }

        private void InitializePlayerComponents()
        {
            if (_playerHandler == null)
                _playerHandler = GetComponent<PlayerHandler>();
            if (_playerHandler == null)
                _playerHandler = gameObject.AddComponent<PlayerHandler>();

            if (_playerJump == null)
                _playerJump = GetComponent<PlayerJump>();
            if (_playerJump == null)
                _playerJump = gameObject.AddComponent<PlayerJump>();

            if (_playerMovement == null)
                _playerMovement = GetComponent<PlayerMovement>();
            if (_playerMovement == null)
                _playerMovement = gameObject.AddComponent<PlayerMovement>();

            if (_playerPhysics == null)
                _playerPhysics = GetComponent<PlayerPhysics>();
            if (_playerPhysics == null)
                _playerPhysics = gameObject.AddComponent<PlayerPhysics>();

        }

        private void InitializeComponents()
        {
            if (_body == null)
            {
                _body = GetComponent<UnityEngine.Rigidbody2D>();
            }

            if (_capsuleCollider == null)
            {
                _capsuleCollider = GetComponent<CapsuleCollider2D>();
            }

            if (_visualRoot == null)
            {
                _visualRoot = transform;
            }
        }

        private void InitializeEvents()
        {
            UnsubscribeEvents();

            if (_gameInput != null)
            {
                _gameInput.JumpPressed += HandleJumpPressed;
                _gameInput.JumpReleased += HandleJumpReleased;
            }

            GameManager.GameStateChanged += OnGameStateChanged;
        }

        private void UnsubscribeEvents()
        {
            if (_gameInput != null)
            {
                _gameInput.JumpPressed -= HandleJumpPressed;
                _gameInput.JumpReleased -= HandleJumpReleased;
            }

            GameManager.GameStateChanged -= OnGameStateChanged;
        }

        #endregion

        //------------------------------- Core / Routines Methods -------------------------------//

        public void SetMovementInput(Vector2 movementInput)
        {
            SetMovementInput(movementInput.x);
        }

        public void SetMovementInput(float movementInput)
        {
            _movementInput = Mathf.Clamp(movementInput, -1f, 1f);
        }
        public void BeginScriptedControl()
        {
            _playerHandler.BeginScriptedControl();
        }

        public void SetScriptedMoveInput(Vector2 input)
        {
            _playerHandler.SetScriptedMoveInput(input);
        }

        public void StopScriptedHorizontalMotion()
        {
            if (_body == null)
            {
                return;
            }

            Vector2 velocity = _body.linearVelocity;
            velocity.x = 0f;
            _body.linearVelocity = velocity;
        }

        public void EndScriptedControl()
        {
            _playerHandler.EndScriptedControl();
            SetMovementInput(0f);
        }

        public void RequestJump()
        {
            _playerJump.RequestJump(_jump);
        }

        public void ReleaseJump()
        {
            if (_body != null)
            {
                _playerJump.ReleaseJump(_jump, _body);
            }
        }

        public void SetControlEnabled(bool controlEnabled)
        {
            _state.SetControlEnabled(controlEnabled);
            PlayerStatus.SetStatus(controlEnabled ? PlayerStatus.Status.Normal : PlayerStatus.Status.Disabled);

            if (!controlEnabled)
            {
                SetMovementInput(0f);
                _jump.ResetRuntimeState();
            }
        }

        public void FreezeMovement()
        {
            if (_body == null || _playerHandler.IsMovementFrozen)
                return;

            _playerHandler.Freeze(_body);
            SetMovementInput(0f);
        }

        public void UnFreezeMovement()
        {
            if (_body == null || !_playerHandler.IsMovementFrozen)
                return;

            _playerHandler.Unfreeze(_body);
        }

        public void ZeroVelocity()
        {
            if (_body == null)
            {
                return;
            }

            _body.linearVelocity = Vector2.zero;
            _body.angularVelocity = 0f;
        }

        public void ResetLocomotionRuntimeState()
        {
            SetMovementInput(0f);
            _playerHandler.ResetRuntimeState();
            SetFacingDirection(1f);
            _state.ResetRuntimeState();
            _movement.ResetRuntimeState();
            _jump.ResetRuntimeState();
            _externalForces.ClearRuntimeState();
            _movingPlatforms.ClearRuntimeState();
            _steepSlopeBlockDirection = 0f;
            ZeroVelocity();
        }

        public void AddExternalImpulse(
            Vector2 impulse,
            bool protectHorizontalMomentum = true,
            bool clearOnGrounded = false)
        {
            float previousHorizontalVelocity = _body.linearVelocity.x;
            _externalForces.AddImpulse(_body, impulse, protectHorizontalMomentum, clearOnGrounded);
            float expectedHorizontalVelocity =
                previousHorizontalVelocity + impulse.x / Mathf.Max(_body.mass, 0.0001f);
            _movement.AddMoveSpeedClamp(expectedHorizontalVelocity);
        }

        public void AddExternalVelocity(
            Vector2 velocity,
            bool protectHorizontalMomentum = true,
            bool clearOnGrounded = false)
        {
            _externalForces.AddVelocity(_body, velocity, protectHorizontalMomentum, clearOnGrounded);
            _movement.AddMoveSpeedClamp(_body.linearVelocity.x);
        }

        public void SetExternalVelocity(
            Vector2 velocity,
            bool protectHorizontalMomentum = true,
            bool clearOnGrounded = false)
        {
            _externalForces.SetVelocity(_body, velocity, protectHorizontalMomentum, clearOnGrounded);
            _movement.SetMoveSpeedClamp(Mathf.Abs(velocity.x));
        }

        private void UpdateLoop()
        {
            if (!_initialized)
                return;

            RefreshGameInput();
            UpdateParameters();
            UpdateModules();
        }

        private void UpdateParameters()
        {
            UpdateMoveInput();
        }

        private void UpdateModules()
        {
            UpdateFacingDirection();
        }

        internal void StageApplyPlatformDelta()
        {
            _fixedStepStartPosition = _body.position;
            _movingPlatforms.ApplyPlatformMotion(_body, _jump.IsInJumpAscentState);
        }

        internal void StageUpdatePhysics()
        {
            _playerPhysics.CheckGrounded(_capsuleCollider, _body, _state, _surfaceProbe, _jump);
            _playerJump.UpdateJumpState(_jump, _body, _state);
            _movingPlatforms.Track(_state, _surfaceProbe.SurfaceCollider, _body);
            _steepSlopeBlockDirection = GetSteepSlopeBlockDirection();
            _state.SetSliding(false);
        }

        internal void StageUpdateMovement()
        {
            _movement.ApplyMovement(
                _body, _state, _surfaceProbe, _externalForces, _movementInput,
                _steepSlopeBlockDirection, _jump.IsInJumpAscentState);
        }

        internal void StageUpdateJump()
        {
            if (_playerJump.TryApplyJump(_jump, _body, _state, _steepSlopeBlockDirection))
                JumpTriggered?.Invoke();
        }

        internal void StageFinalizePlatformSync()
        {
            ApplyGravity();
            ApplyGroundAdhesion();
            bool hadExternalForce = _externalForces.HasExternalForce;
            _externalForces.UpdateRuntimeState(_body, _state);
            if (hadExternalForce && !_externalForces.HasExternalForce)
                _movement.ResetRuntimeState();

            _movingPlatforms.FinalizePlatformSync(_body);
            _state.SetVerticalMotion(_body.linearVelocity.y);
            _state.RefreshPhysicsState(
                _movementInput, _body.linearVelocity,
                _jump.IsInAnticipationFrame || _jump.IsInJumpAscentState,
                _externalForces.HasExternalForce);
            _horizontalDistancePerFixedFrame = Mathf.Abs(_body.position.x - _fixedStepStartPosition.x);
            RecordGroundAdhesionLog();
            CheckGroundingDebug();
        }

        private void RefreshGameInput()
        {
            if (_gameInput != null)
            {
                return;
            }

            _gameInput = GameInput.Instance;

            if (_gameInput != null)
            {
                InitializeEvents();
            }
        }

        private void UpdateMoveInput()
        {
            SetMovementInput(_playerHandler.ResolveMovementInput(
                _state.ControlEnabled,
                _gameInput));
        }

        private void UpdateFacingDirection()
        {
            float direction = _playerMovement.ResolveFacingDirection(
                _movementInput, _body, _externalForces, _facingDirection);

            if (!Mathf.Approximately(direction, _facingDirection))
                SetFacingDirection(direction);
        }

        private void SetFacingDirection(float direction)
        {
            float nextDirection = direction >= 0f ? 1f : -1f;

            if (Mathf.Approximately(_facingDirection, nextDirection))
            {
                return;
            }

            _facingDirection = nextDirection;
            ApplyVisualFacing();
            DirectionChanged?.Invoke(_facingDirection);
        }

        private void ApplyVisualFacing()
        {
            if (!_flipVisualWithFacing || _visualRoot == null)
            {
                return;
            }

            Vector3 eulerAngles = _visualRoot.localEulerAngles;
            eulerAngles.y = _facingDirection > 0f ? 0f : 180f;
            _visualRoot.localEulerAngles = eulerAngles;
        }

        private float GetSteepSlopeBlockDirection()
        {
            return _playerMovement.ResolveSteepSlopeBlockDirection(
                _movementInput,
                _facingDirection,
                _body,
                _capsuleCollider,
                _surfaceProbe,
                _steepSlopeJumpCheckDistance);
        }

        private void ApplyGravity()
        {
            _playerPhysics.ApplyGravity(
                _body,
                _gravityScale,
                _jumpAscentGravityMultiplier,
                _fallGravityMultiplier,
                _maximumFallSpeed);
        }

        private void ApplyGroundAdhesion()
        {
            _lastGroundAdhesionDecision = _playerPhysics.GetGroundAdhesionDecision(
                _body,
                _state,
                _surfaceProbe,
                _jump,
                _externalForces,
                _groundAdhesionForce,
                out _lastGroundAdhesionApplied);
        }

        private void RecordGroundAdhesionLog()
        {
            if (!_exportGroundAdhesionLog)
            {
                CloseGroundAdhesionLog();
                return;
            }

            if (_groundAdhesionLogWriter == null && !OpenGroundAdhesionLog())
            {
                return;
            }

            Vector2 velocity = _body.linearVelocity;
            Vector2 normal = _surfaceProbe.SurfaceNormal.normalized;
            float normalVelocity = Vector2.Dot(velocity, normal);
            float slopeAngle = Vector2.Angle(normal, Vector2.up);
            string surfaceName = _surfaceProbe.SurfaceCollider != null
                ? _surfaceProbe.SurfaceCollider.name
                : string.Empty;

            _groundAdhesionLogWriter.WriteLine(
                $"{Time.time:F3},{Time.fixedTime:F3}," +
                $"{_body.position.x:F4},{_body.position.y:F4}," +
                $"{velocity.x:F4},{velocity.y:F4}," +
                $"{_state.Grounded},{_surfaceProbe.HasDirectSurface}," +
                $"{EscapeCsv(surfaceName)}," +
                $"{normal.x:F4},{normal.y:F4},{slopeAngle:F2},{normalVelocity:F4}," +
                $"{_jump.IsInJumpAscentState},{_externalForces.HasExternalForce}," +
                $"{_groundAdhesionForce:F2},{_lastGroundAdhesionApplied}," +
                $"{EscapeCsv(_lastGroundAdhesionDecision)}," +
                $"{EscapeCsv(_surfaceProbe.LastProbeResult)}");

            _groundAdhesionLogTimer += Time.fixedDeltaTime;

            if (_groundAdhesionLogTimer >= _groundAdhesionLogInterval)
            {
                _groundAdhesionLogWriter.Flush();
                _groundAdhesionLogTimer = 0f;
            }
        }

        private bool OpenGroundAdhesionLog()
        {
            try
            {
                string fileName = $"GroundAdhesion_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                _groundAdhesionLogPath = Path.Combine(Application.persistentDataPath, fileName);
                _groundAdhesionLogWriter = new StreamWriter(_groundAdhesionLogPath, false);
                _groundAdhesionLogWriter.WriteLine(
                    "Time,FixedTime,PositionX,PositionY,VelocityX,VelocityY," +
                    "Grounded,HasDirectSurface,SurfaceCollider,NormalX,NormalY," +
                    "SlopeAngle,VelocityAlongNormal,JumpAscent,ExternalForce," +
                    "AdhesionForce,AdhesionApplied,AdhesionDecision,ProbeResult");
                _groundAdhesionLogWriter.Flush();
                _groundAdhesionLogError = string.Empty;
                _groundAdhesionLogTimer = 0f;
                return true;
            }
            catch (Exception exception)
            {
                _groundAdhesionLogError = exception.Message;
                CloseGroundAdhesionLog();
                return false;
            }
        }

        private void CloseGroundAdhesionLog()
        {
            if (_groundAdhesionLogWriter == null)
            {
                return;
            }

            try
            {
                _groundAdhesionLogWriter.Flush();
                _groundAdhesionLogWriter.Dispose();
            }
            catch (IOException exception)
            {
                _groundAdhesionLogError = exception.Message;
            }

            _groundAdhesionLogWriter = null;
        }

        private string EscapeCsv(string value)
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        private void CheckGroundingDebug()
        {
            if (_state.Grounded)
            {
                _wasGroundedForDebug = true;
                _airbornePauseTriggered = false;
                _groundingDebugLogTimer = 0f;
                return;
            }

            bool justBecameAirborne = _wasGroundedForDebug;
            _wasGroundedForDebug = false;

            if (!_debugGrounding && !_pauseOnAirborne)
            {
                return;
            }

            _groundingDebugLogTimer += Time.fixedDeltaTime;
            if (justBecameAirborne || _groundingDebugLogTimer >= _groundingDebugLogInterval)
            {
                LogGroundingFailure(justBecameAirborne ? "Grounded -> Airborne transition" : "Still not grounded");
                _groundingDebugLogTimer = 0f;
            }

            if (!_pauseOnAirborne || _airbornePauseTriggered)
            {
                return;
            }

            _airbornePauseTriggered = true;
            LogGroundingFailure("PAUSING PLAY MODE: player is not grounded");

#if UNITY_EDITOR
            if (Application.isPlaying)
            {
                Debug.Break();
            }
#endif
        }

        private void LogGroundingFailure(string reason)
        {
            Vector2 velocity = _body != null ? _body.linearVelocity : Vector2.zero;
            Bounds bounds = _capsuleCollider != null ? _capsuleCollider.bounds : new Bounds();
            Vector2 normal = _surfaceProbe != null ? _surfaceProbe.SurfaceNormal : Vector2.up;
            float normalVelocity = Vector2.Dot(velocity, normal.normalized);
            string surfaceName = _surfaceProbe != null && _surfaceProbe.SurfaceCollider != null
                ? _surfaceProbe.SurfaceCollider.name
                : "None";

            Debug.LogWarning(
                $"[Rigidbody Grounding Debug] {reason}\n" +
                $"Object: {name}\n" +
                $"Time: {Time.time:F3}, FixedTime: {Time.fixedTime:F3}, Frame: {Time.frameCount}\n" +
                $"Position: {_body.position}, Collider Bounds Center: {bounds.center}, Size: {bounds.size}\n" +
                $"Grounded: {_state.Grounded}, Direct Surface: {_surfaceProbe.HasDirectSurface}, Surface: {surfaceName}\n" +
                $"Probe Result: {_surfaceProbe.LastProbeResult}\n" +
                $"Probe Details: {_surfaceProbe.LastProbeDetails}\n" +
                $"Surface Normal: {normal}, Slope Angle: {Vector2.Angle(normal, Vector2.up):F2}, Velocity Along Normal: {normalVelocity:F3}\n" +
                $"Velocity: {velocity}, Movement Input: {_movementInput}, Horizontal Distance / Fixed Frame: {_horizontalDistancePerFixedFrame:F4}\n" +
                $"Probe Distance: {_surfaceProbe.ProbeDistance:F3}, Probe Inset: {_surfaceProbe.ProbeInset:F3}, Ground Point: {(_surfaceProbe.GroundPoint != null ? _surfaceProbe.GroundPoint.position.ToString() : "Not assigned")}\n" +
                $"Jump Ascent: {_jump.IsInJumpAscentState}, Anticipation: {_jump.IsInAnticipationFrame}, Ignore Grounding: {_jump.ShouldIgnoreGrounding}\n" +
                $"External Force: {_externalForces.HasExternalForce}, Adhesion Applied: {_lastGroundAdhesionApplied}, Adhesion Decision: {_lastGroundAdhesionDecision}\n" +
                $"Gravity Scale: {_body.gravityScale}, Rigidbody Type: {_body.bodyType}, Simulated: {_body.simulated}",
                this);
        }

        private void OnDrawGizmosSelected()
        {
            if (!_drawGroundingGizmos)
            {
                return;
            }

            CapsuleCollider2D capsule = _capsuleCollider != null
                ? _capsuleCollider
                : GetComponent<CapsuleCollider2D>();

            if (capsule == null)
            {
                return;
            }

            Bounds bounds = capsule.bounds;
            float probeDistance = _surfaceProbe != null ? _surfaceProbe.ProbeDistance : 0.2f;
            float probeInset = _surfaceProbe != null ? _surfaceProbe.ProbeInset : 0.04f;
            float originOffset = _surfaceProbe != null ? _surfaceProbe.GroundPointOriginOffset : 0.03f;
            Transform groundPoint = _surfaceProbe != null ? _surfaceProbe.GroundPoint : null;
            Vector2 center = groundPoint != null
                ? (Vector2)groundPoint.position + Vector2.up * originOffset
                : (Vector2)bounds.center;
            float rayLength = groundPoint != null
                ? probeDistance + originOffset
                : bounds.extents.y + probeDistance;
            float leftX = bounds.min.x + probeInset;
            float rightX = bounds.max.x - probeInset;

            Gizmos.color = _state != null && _state.Grounded
                ? (_surfaceProbe != null && _surfaceProbe.HasDirectSurface ? Color.green : Color.yellow)
                : Color.red;
            Gizmos.DrawWireCube(bounds.center, bounds.size);
            DrawGroundProbeGizmo(center, rayLength);
            DrawGroundProbeGizmo(new Vector2(leftX, center.y), rayLength);
            DrawGroundProbeGizmo(new Vector2(rightX, center.y), rayLength);

            if (groundPoint != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawSphere(groundPoint.position, 0.035f);
            }

            if (_surfaceProbe != null && _surfaceProbe.SurfaceCollider != null)
            {
                Gizmos.color = Color.blue;
                Vector3 normalStart = bounds.center;
                Gizmos.DrawLine(normalStart, normalStart + (Vector3)(_surfaceProbe.SurfaceNormal * 0.5f));
            }
        }

        private void DrawGroundProbeGizmo(Vector2 origin, float distance)
        {
            Gizmos.DrawSphere(origin, 0.025f);
            Gizmos.DrawLine(origin, origin + Vector2.down * distance);
        }

        private void HandleJumpPressed()
        {
            if (_state.ControlEnabled && !_movementFrozen)
            {
                RequestJump();
            }
        }

        private void HandleJumpReleased()
        {
            ReleaseJump();
        }

#if UNITY_EDITOR
        //------------------------------- Editor -------------------------------//

        private void OnValidate()
        {
            _gravityScale = Mathf.Max(0f, _gravityScale);
            _jumpAscentGravityMultiplier = Mathf.Max(0f, _jumpAscentGravityMultiplier);
            _fallGravityMultiplier = Mathf.Max(0f, _fallGravityMultiplier);
            _maximumFallSpeed = Mathf.Max(0f, _maximumFallSpeed);
            _steepSlopeJumpCheckDistance = Mathf.Max(0.01f, _steepSlopeJumpCheckDistance);
            _groundAdhesionForce = Mathf.Max(0f, _groundAdhesionForce);
            _groundAdhesionLogInterval = Mathf.Max(0.01f, _groundAdhesionLogInterval);
            _groundingDebugLogInterval = Mathf.Max(0.01f, _groundingDebugLogInterval);
            _movement?.Validate();
            _jump?.Validate();
            _surfaceProbe?.Validate();
            _externalForces?.Validate();

            if (!Application.isPlaying)
            {
                InitializeComponents();
            }
        }
#endif
    }
}
