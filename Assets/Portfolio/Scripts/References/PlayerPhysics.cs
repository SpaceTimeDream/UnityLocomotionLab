using PlayerSystem;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class PlayerPhysics : MonoBehaviour
{
    public enum PlatformSyncMode
    {
        PlatformDetection = 0,
        Parenting = 1
    }

    //------------------------------- Components -------------------------------//

    private Rigidbody2D _rb2D;
    private CapsuleCollider2D _mainCollider2D;

    //------------------------------- Transform Points -------------------------------//

    [SerializeField] private Transform _groundPoint;

    //------------------------------- PhysicsMaterial2Ds -------------------------------//

    [SerializeField] private PhysicsMaterial2D _noFricitionMaterial;
    public PhysicsMaterial2D noFricitionMaterial => _noFricitionMaterial;

    [SerializeField] private PhysicsMaterial2D _noFricitionMaterialStandingStill;
    public PhysicsMaterial2D noFricitionMaterialStandingStill => _noFricitionMaterial;

    [SerializeField] private PhysicsMaterial2D _fullFricitionMaterial;
    public PhysicsMaterial2D fullFricitionMaterial => _fullFricitionMaterial;

    //------------------------------- Modules -------------------------------//

    private PlayerController _controller;
    private PlayerMovement _movement;
    private PlayerJump _jump;
    private PlayerIdle _idle;
    private AnimationBinder _animationBinder;

    //------------------------------- Events -------------------------------//

    public event Action<PhysicsState, bool> OnPhysicsStateChanged;

    public event Action<PlayerPhysics> OnGroundAction;

    public event Action OnTurn;

    //------------------------------- State -------------------------------//

    private readonly Dictionary<int, bool> _isStairsCache = new();


    //------------------------------- Parameters -------------------------------//

    #region Ground Check

    [Header("GROUNDED CHECK PARAMETERS")]

    [SerializeField] private float _minGroundedDistance = 0.1f;
    [SerializeField] private float _platformPersistenceVerticalThreshold = 0.35f;
    [SerializeField] private float _platformPersistenceHorizontalPadding = 0.15f;

    [Header("WALLS AND ROOFS EJECTION")]
    [FormerlySerializedAs("_unwalkableSurfaceTranslationSpeed")]
    [FormerlySerializedAs("_unwalkableSurfaceMaximumTranslationSpeed")]
    [SerializeField, Min(0f), Tooltip("Maximum speed used to smoothly eject the player from WallsAndRoofs surfaces.")]
    private float _wallsAndRoofsEjectionMaximumSpeed = 1f;
    [FormerlySerializedAs("_unwalkableSurfaceTranslationSpeedAccumulationRate")]
    [SerializeField, Min(0f), Tooltip("Ejection speed added per second while the WallsAndRoofs ejection is active.")]
    private float _wallsAndRoofsEjectionAcceleration = 2f;
    [SerializeField, Min(0f), Tooltip("Minimum distance the player continues to travel after its last WallsAndRoofs contact.")]
    private float _wallsAndRoofsPostContactEjectionDistance = 1.5f;

    [Header("WALLS AND ROOFS EJECTION DEBUG")]
    [SerializeField, Tooltip("When enabled, WallsAndRoofs contacts cannot set or preserve the player's grounded state.")]
    private bool _debugExcludeWallsAndRoofsFromGrounding;
    [SerializeField, Tooltip("When enabled, the current WallsAndRoofs ejection speed increases steep-slope sliding speed.")]
    private bool _debugApplyEjectionSpeedToSteepSlopeSliding = true;
    [FormerlySerializedAs("_debugDrawUnwalkableSurfaceGizmos")]
    [SerializeField] private bool _debugDrawWallsAndRoofsEjectionGizmos = true;
    [FormerlySerializedAs("_debugUnwalkableSurfaceGizmoLength")]
    [SerializeField] private float _debugWallsAndRoofsEjectionGizmoLength = 0.75f;
    [FormerlySerializedAs("_debugUnwalkableSurfaceContactRadius")]
    [SerializeField] private float _debugWallsAndRoofsEjectionContactRadius = 0.06f;
    [FormerlySerializedAs("_debugUnwalkableSurfaceArrowHeadLength")]
    [SerializeField] private float _debugWallsAndRoofsEjectionArrowHeadLength = 0.15f;

    [Header("PLATFORM DEBUG")]
    [SerializeField] private PlatformSyncMode _debugPlatformSyncMode = PlatformSyncMode.PlatformDetection;
    [SerializeField] private bool _debugEnablePlatformSyncLogs = false;
    [SerializeField] private int _debugLogEveryNFixedFrames = 5;


    private GameObject _groundContactObject;
    public GameObject groundedCotactObject => _groundContactObject;

    private bool _wasOnSlopeLastFrame;
    private bool _wasGroundedLastFrame;
    private Transform _platform;
    private Vector2 _lastPlatformPosition;
    private Vector2 _platformVelocity;
    private PlatformDetection _currentPlatform;
    private PlatformDetection _lastPlatform;
    private Vector2 _currentPlatformDelta;
    private Vector2 _platformLocalOffset;
    private Vector2 _platformContactBaselineOffset;
    private bool _hasPlatformContactBaseline;
    private Transform _originalParent;
    private int _platformDebugFrameCounter;

    private Vector2 _wallsAndRoofsEjectionDirection;
    private readonly ContactPoint2D[] _wallsAndRoofsContacts = new ContactPoint2D[8];
    private Vector2 _wallsAndRoofsContactPoint;
    private Vector2 _wallsAndRoofsContactNormal;
    private float _currentWallsAndRoofsEjectionSpeed;
    private float _remainingWallsAndRoofsEjectionDistance;

    #endregion

    #region Gravity Parameters

    [Header("GRAVITY PARAMETERS")]

    [SerializeField] private float Default_GravityScale = 3f;

    [SerializeField] private float _baseGravity = 9.81f; // Default gravity
    public float baseGravity => _baseGravity;
    [SerializeField] private float _jumpAscentGravityMultiplier = 1.2f; // Slightly stronger gravity while rising
    public float jumpAscentGravityMultiplier => _jumpAscentGravityMultiplier;
    [SerializeField] private float _fallGravityMultiplier = 2.5f; // Much stronger gravity while falling
    public float fallGravityMultiplier => _fallGravityMultiplier;

    [SerializeField] private float _fallClamp = 40f;
    [SerializeField] private float _fallClampModifer = 40f;
    public float fallClampModifer
    {
        get => _fallClampModifer;
        set => _fallClampModifer = value;
    }

    private float _currentGravity;

    //--- Velocity Metrics

    private Vector2 _velocity;
    public Vector2 Velocity => _velocity;

    private float _speed;
    public float Speed => _speed;

    private float _verticalVelocity;
    public float VerticalVelocity => _verticalVelocity;

    #endregion

    #region Slope Check

    [Header("SLOPE CHECK PARAMETERS")]

    [SerializeField] private float _slopeAdherenceFactor = 2.5f;
    public float slopeAdherenceFactor => _slopeAdherenceFactor;


    //--- Slope Detection Distances

    [SerializeField] private float _slopeCheckDistance;
    public float slopeCheckDistance => _slopeCheckDistance;

    [SerializeField] private float _slopeAheadCheckDistance;
    public float slopeAheadCheckDistance => _slopeAheadCheckDistance;

    [SerializeField] private float _steepSlopeAheadCheckDistance;
    public float steepSlopeAheadCheckDistance => _steepSlopeAheadCheckDistance;

    [SerializeField] private float _obstacleAheadCheckDistance;
    public float obstacleAheadCheckDistance => _obstacleAheadCheckDistance;

    //--- Slope Angle Thresholds

    [SerializeField] private float _slopeVerticalCheckThreshold;
    public float slopeVerticalCheckThreshold => _slopeVerticalCheckThreshold;

    [SerializeField] private float _slopeAngleThreshold = 10f;
    public float slopeAngleThreshold => _slopeAngleThreshold;

    [SerializeField] private float _steepSlopeAngleThreshold = 25f;
    public float steepSlopeAngleThreshold => _steepSlopeAngleThreshold;

    [SerializeField] private float _steepSlopeAngleJumpThreshold = 35f;
    public float steepSlopeAngleJumpThreshold => _steepSlopeAngleJumpThreshold;

    [SerializeField] private float _slopeSideAngleThreshold = 10f;
    public float slopeSideAngleThreshold => _slopeSideAngleThreshold;

    [SerializeField] private float _clifAngleThreshold = 10f;
    public float clifAngleThreshold => _clifAngleThreshold;

    #endregion

    #region External Force

    [Header("EXTERNAL FORCE PARAMETERS")]

    [SerializeField] private float _externalForceResistance = 3f;
    public float externalForceResistance => HasExternalForce ? _externalForceResistance : Default_ExternalForceResistance;
    private float Default_ExternalForceResistance = 1f;

    private Coroutine _resetExternalForceCoroutine;

    #endregion

    #region Misc Parameters

    [Header("MISC PARAMETERS")]

    private float _facingDir = 1;
    public float facingDir
    {
        get => _facingDir;
    }
    private Vector3 _lastPosition;
    public Vector3 lastPosition
    {
        get => _lastPosition;
    }
    private Vector3 _lastMoveDirection;
    public Vector3 lastMoveDirection
    {
        get => _lastMoveDirection;
    }

    private Vector2 _cachedVelocity;
    private bool _cachedControlLockKinematicState;
    private float _cachedControlLockGravityScale;
    private Vector2 _cachedControlLockVelocity;

    #endregion

    //------------------------------- Flags -------------------------------//
    private bool _isFreezed;
    public bool IsMovementFrozen => _isFreezed;
    private bool _isControlLocked;
    public bool IsControlLocked => _isControlLocked;
    //--- Ground Check
    private bool _isInitialized;
    private bool _grounded;
    private float _groundedTime;
    public bool IsGrounded => _grounded;
    public float GroundedTime => _groundedTime;
    public bool grounded { set => _grounded = value; }
    public bool IsGroundCheckDelayed => _isGroundCheckDelayed;

    //--- Turn

    public bool IsTurning => PlayerStatus.HasState(PhysicsState.Turning);

    //--- Move

    public bool IsIdle => !IsWalking && !IsTurning && _grounded && (Mathf.Approximately(_controller.moveInput.x, 0f) || IsForwardMovementBlockedBySteepSlope);
    public bool IsMoving => Mathf.Abs(_rb2D.velocity.x) > 0;
    public bool IsWalking => Mathf.Abs(_rb2D.velocity.x) > 0 && _grounded && _controller.moveInput.x != 0 && !IsForwardMovementBlockedBySteepSlope;
    public bool IsRefractedMoving => PlayerStatus.HasMode(Mode.Refracted) && _rb2D.velocity.sqrMagnitude > 0.01f;
    //public bool IsRefractedMoving => PlayerStatus.HasMode(Mode.Refracted);

    //--- Jump and Fall

    public bool IsFalling => !_grounded && _rb2D.velocity.y < -0.1f && !IsJumping;
    public bool IsJumping => _jump.IsInAnticipationFrame || (_rb2D.velocity.y > 0 && _jump.hasJumped);
    public bool IsFloatingUp => _rb2D.velocity.y > 0 && !IsJumping && !_grounded;

    //--- External Force

    private bool _externalForce;
    public bool HasExternalForce => _externalForce;

    private bool _clearExternalForceOnGrounded;

    // A short lock window used by mirror launches so movement input cannot
    // immediately dampen externally authored launch velocity.
    private int _externalForceLockFramesRemaining;
    public bool IsExternalForceControlActive => _externalForce && _externalForceLockFramesRemaining > 0;

    //--- Slope & Obstacle Angles

    private float _slopeDownAngle;
    public float slopeDownAngle => _slopeDownAngle;

    private float _slopeSideAngle;
    public float slopeSideAngle => _slopeSideAngle;

    private float _slopeAheadAngle;
    public float slopeAheadAngle => _slopeAheadAngle;

    private Vector2 _slopeNormalPerp;
    public Vector2 slopeNormalPerp => _slopeNormalPerp;

    //--- Slope & Surface States

    private bool _isOnSlope;
    public bool IsOnSlope => _isOnSlope;

    private bool _isSlopeAhead;
    public bool IsSlopeAhead => _isSlopeAhead;

    private bool _isSteepSlopeAhead;
    public bool IsSteepSlopeAhead => _isSteepSlopeAhead;

    public bool IsForwardMovementBlockedBySteepSlope
    {
        get
        {
            float moveX = _controller != null ? _controller.moveInput.x : 0f;
            if (Mathf.Approximately(moveX, 0f)) { return false; }
            return _isSteepSlopeAhead && Mathf.Sign(moveX) == Mathf.Sign(_facingDir);
        }
    }

    private bool _isOnSteepSlope;
    public bool IsOnSteepSlope => _isOnSteepSlope;

    private bool _isOnFlatSurface;
    public bool IsOnFlatSurface => _isOnFlatSurface;

    private bool _isOnVerticalSlope;
    public bool IsOnVerticalSlope => _isOnVerticalSlope;

    private bool _isOnHorizontalSlope;
    public bool IsOnHorizontalSlope => _isOnHorizontalSlope;

    private bool _isTouchingWallsAndRoofs;
    public bool IsTouchingWallsAndRoofs => _isTouchingWallsAndRoofs;
    public bool IsWallsAndRoofsEjectionActive => _isTouchingWallsAndRoofs || _remainingWallsAndRoofsEjectionDistance > Mathf.Epsilon;
    public bool IsWallsAndRoofsSteepSlopeSlideInfluenceActive =>
        _debugApplyEjectionSpeedToSteepSlopeSliding && IsWallsAndRoofsEjectionActive;
    public float CurrentWallsAndRoofsEjectionSpeed => _currentWallsAndRoofsEjectionSpeed;
    public float WallsAndRoofsSteepSlopeSlideSpeedMultiplier =>
        IsWallsAndRoofsSteepSlopeSlideInfluenceActive
            ? 1f + _currentWallsAndRoofsEjectionSpeed
            : 1f;

    //--- Movement on Slopes & Obstacles

    private bool _isMovingUpSlope;
    public bool IsMovingUpSlope => _isMovingUpSlope;

    private bool _isObstacleAhead;
    public bool IsObstacleAhead => _isObstacleAhead;
    public Vector2 CurrentPlatformDelta => _currentPlatformDelta;

    //--- Coroutines

    public int ActiveRuntimeCoroutineCount
    {
        get
        {
            int count = 0;
            if (_resetExternalForceCoroutine != null) { count++; }
            if (_temporarilyDisableGroundCheckCoroutine != null) { count++; }
            if (_updateSlopeGroundedCoroutine != null) { count++; }
            if (_changeFacingAfterTurnCoroutine != null) { count++; }
            if (_moveTowardsZeroVelocityCoroutine != null) { count++; }
            return count;
        }
    }

    //------------------------------- Unity Methods -------------------------------//

    #region Unity

    private void Start()
    {
        Initialize();
    }
    private void OnEnable()
    {
        GameplayManager.NewSessionProcessingStarted += FreezeMovement;
        GameplayManager.NewSessionProcessingCompleted += UnFreezeMovement;
    }

    private void OnDisable()
    {
        RestoreOriginalParentIfNeeded();
        OnPhysicsStateChanged = null;
        GameplayManager.NewSessionProcessingStarted -= FreezeMovement;
        GameplayManager.NewSessionProcessingCompleted -= UnFreezeMovement;
    }

    private void OnDestroy()
    {
        RestoreOriginalParentIfNeeded();
        OnPhysicsStateChanged = null;
    }

    #endregion

    //------------------------------- Initialization -------------------------------//

    #region Initialization

    private void Initialize()
    {
        if (_isInitialized) { return; }
        InitializeModules();
        InitializeComponents();
        InitializeEvents();
        _originalParent = transform.parent;
        _isInitialized = true;
    }

    private void InitializeComponents()
    {
        if (_rb2D == null) { _rb2D = _controller.rb2D; }
        if (_mainCollider2D == null) { _mainCollider2D = _controller.mainCollider2D; }
    }

    private void InitializeModules()
    {
        if (_idle == null) _idle = GetComponent<PlayerIdle>();
        if (_controller == null) _controller = GetComponent<PlayerController>();
        if (_movement == null) _movement = _controller.movement;
        if (_jump == null) _jump = _controller.jump;
        if (_animationBinder == null) _animationBinder = GetComponent<AnimationBinder>();
    }

    private void InitializeEvents()
    {
        OnPhysicsStateChanged += PlayerStatus.HandlePhysicsState;
    }

    #endregion

    //------------------------------- Update Methods -------------------------------//

    #region Update

    public void UpdatePhysics()
    {
        if (!_isInitialized) { return; }
        UpdateFacingDirection();
        UpdatePosition();
        UpdateVelocityMetrics();
        UpdateSlopeDetection();
        CheckSteepSlopeAhead();
        CheckWallsAndRoofsContact();
        UpdateGrounded();
        UpdateWallsAndRoofsEjection();
        UpdatePlatformDetection();
        UpdateGravity();
        UpdateExternalForceState();
        UpdatePhysicState();
        LogPlatformSyncDiagnostics();
    }

    public void ApplyPlatformDelta()
    {
        if (_platform == null) { return; }
        if (_jump != null && _jump.IsInJumpAscentState) { return; }
        if (_debugPlatformSyncMode == PlatformSyncMode.Parenting) { return; }

        Vector2 platformPosition = _platform.position;
        Vector2 expectedFromLastFrame = _lastPlatformPosition + _platformLocalOffset;
        Vector2 relativeDrift = _rb2D.position - expectedFromLastFrame;
        Vector2 targetPosition = platformPosition + _platformLocalOffset + relativeDrift;
        if ((_rb2D.position - targetPosition).sqrMagnitude <= Mathf.Epsilon)
        {
            _lastPlatformPosition = platformPosition;
            return;
        }

        _rb2D.position = targetPosition;
        _lastPlatformPosition = platformPosition;
    }

    public void FinalizePlatformSync()
    {
        if (_platform == null)
        {
            _currentPlatformDelta = Vector2.zero;
            return;
        }

        Vector2 platformPosition = _platform.position;
        _platformLocalOffset = _rb2D.position - platformPosition;
        _currentPlatformDelta = platformPosition - _lastPlatformPosition;
        _lastPlatformPosition = platformPosition;
    }

    private void CheckWallsAndRoofsContact()
    {
        _isTouchingWallsAndRoofs = false;

        if (_mainCollider2D == null || !_mainCollider2D.enabled)
        {
            return;
        }

        ContactFilter2D contactFilter = new();
        contactFilter.useTriggers = false;
        contactFilter.SetLayerMask(1 << LayerManager.WallsAndRoofs);

        int contactCount = _mainCollider2D.GetContacts(contactFilter, _wallsAndRoofsContacts);
        if (contactCount == 0)
        {
            return;
        }

        Vector2 colliderCenter = _mainCollider2D.bounds.center;
        Vector2 combinedEjectionDirection = Vector2.zero;
        Vector2 combinedContactPoint = Vector2.zero;

        for (int i = 0; i < contactCount; i++)
        {
            Vector2 contactPoint = _wallsAndRoofsContacts[i].point;
            combinedContactPoint += contactPoint;
            combinedEjectionDirection += (colliderCenter - contactPoint).normalized;
        }

        if (combinedEjectionDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        _isTouchingWallsAndRoofs = true;
        _wallsAndRoofsContactPoint = combinedContactPoint / contactCount;
        _wallsAndRoofsContactNormal = combinedEjectionDirection.normalized;
    }

    private void UpdateWallsAndRoofsEjection()
    {
        if (_isTouchingWallsAndRoofs)
        {
            if (_wallsAndRoofsContactNormal.sqrMagnitude <= Mathf.Epsilon)
            {
                ResetWallsAndRoofsEjection();
                return;
            }

            _wallsAndRoofsEjectionDirection = _wallsAndRoofsContactNormal.normalized;
            _remainingWallsAndRoofsEjectionDistance = _wallsAndRoofsPostContactEjectionDistance;
        }

        if (!IsWallsAndRoofsEjectionActive || _wallsAndRoofsEjectionDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            ResetWallsAndRoofsEjection();
            return;
        }

        _currentWallsAndRoofsEjectionSpeed = Mathf.MoveTowards(
            _currentWallsAndRoofsEjectionSpeed,
            _wallsAndRoofsEjectionMaximumSpeed,
            _wallsAndRoofsEjectionAcceleration * Time.fixedDeltaTime
        );

        float ejectionStepDistance = _currentWallsAndRoofsEjectionSpeed * Time.fixedDeltaTime;
        if (!_isTouchingWallsAndRoofs)
        {
            ejectionStepDistance = Mathf.Min(ejectionStepDistance, _remainingWallsAndRoofsEjectionDistance);
        }
        if (ejectionStepDistance <= Mathf.Epsilon)
        {
            ResetWallsAndRoofsEjection();
            return;
        }

        //_rb2D.position += _wallsAndRoofsEjectionDirection * ejectionStepDistance;

        if (!_isTouchingWallsAndRoofs)
        {
            _remainingWallsAndRoofsEjectionDistance -= ejectionStepDistance;
        }

        if (!IsWallsAndRoofsEjectionActive)
        {
            ResetWallsAndRoofsEjection();
        }
    }

    private void ResetWallsAndRoofsEjection()
    {
        _currentWallsAndRoofsEjectionSpeed = 0f;
        _remainingWallsAndRoofsEjectionDistance = 0f;
        _wallsAndRoofsEjectionDirection = Vector2.zero;
    }

    private void UpdatePlatformDetection()
    {
        _currentPlatformDelta = Vector2.zero;
        bool shouldIgnoreGrounding = _isGroundCheckDelayed || PlayerStatus.IsRefracted || PlayerStatus.HasMode(Mode.NoClip) || IsGravityBypassedByMode();
        if (shouldIgnoreGrounding)
        {
            DetachPlatform();
            _wasGroundedLastFrame = false;
            return;
        }

        if (_grounded)
        {
            _currentPlatform = ResolvePlatformFromGroundObject(_groundContactObject);
            if (_currentPlatform == null)
            {
                DetachPlatform();
                _wasGroundedLastFrame = true;
                return;
            }

            AttachOrRefreshPlatform(_currentPlatform);
            _platformVelocity = _currentPlatform.FrameVelocity;
            _currentPlatformDelta = _currentPlatform.FrameDelta;
            _lastPlatform = _currentPlatform;
            _wasGroundedLastFrame = true;
            return;
        }

        DetachPlatform();
        _wasGroundedLastFrame = false;
    }

    private PlatformDetection ResolvePlatformFromGroundObject(GameObject groundObject)
    {
        if (groundObject == null) { return null; }

        PlatformDetection platform = groundObject.GetComponentInParent<PlatformDetection>();
        if (platform == null || !platform.IsStandableSurface)
        {
            return null;
        }

        return platform;
    }

    private void AttachOrRefreshPlatform(PlatformDetection platform)
    {
        if (platform == null)
        {
            DetachPlatform();
            return;
        }

        Transform nextPlatform = platform.PlatformTransform;
        if (_platform != nextPlatform)
        {
            _platform = nextPlatform;
            _lastPlatformPosition = _platform.position;
            _platformContactBaselineOffset = _rb2D.position - (Vector2)_platform.position;
            _hasPlatformContactBaseline = true;
        }

        _platformLocalOffset = _rb2D.position - (Vector2)_platform.position;
        ApplyDebugParentingMode();
    }

    private void DetachPlatform()
    {
        _currentPlatform = null;
        _platform = null;
        _platformVelocity = Vector2.zero;
        _currentPlatformDelta = Vector2.zero;
        _platformLocalOffset = Vector2.zero;
        _hasPlatformContactBaseline = false;
        _platformDebugFrameCounter = 0;
        RestoreOriginalParentIfNeeded();
    }

    public void SetDebugPlatformSyncMode(PlatformSyncMode mode)
    {
        _debugPlatformSyncMode = mode;
        if (_debugPlatformSyncMode == PlatformSyncMode.Parenting)
        {
            ApplyDebugParentingMode();
        }
        else
        {
            RestoreOriginalParentIfNeeded();
        }
    }

    private void ApplyDebugParentingMode()
    {
        if (_debugPlatformSyncMode != PlatformSyncMode.Parenting) { return; }
        if (_platform == null) { return; }
        if (transform.parent == _platform) { return; }
        if (_originalParent == null)
        {
            _originalParent = transform.parent;
        }
        transform.SetParent(_platform, true);
    }

    private void RestoreOriginalParentIfNeeded()
    {
        if (transform.parent == null) { return; }
        if (_platform != null && transform.parent == _platform && _debugPlatformSyncMode == PlatformSyncMode.Parenting)
        {
            return;
        }

        if (_originalParent != null && transform.parent != _originalParent)
        {
            transform.SetParent(_originalParent, true);
        }
        else if (_originalParent == null && _debugPlatformSyncMode != PlatformSyncMode.Parenting)
        {
            transform.SetParent(null, true);
        }
    }

    private void LogPlatformSyncDiagnostics()
    {
        if (!_debugEnablePlatformSyncLogs) { return; }
        if (_platform == null || !_grounded) { return; }

        _platformDebugFrameCounter++;
        int interval = Mathf.Max(1, _debugLogEveryNFixedFrames);
        if (_platformDebugFrameCounter % interval != 0) { return; }

        Vector2 playerPos = _rb2D.position;
        Vector2 platformPos = _platform.position;
        Vector2 relativeOffset = playerPos - platformPos;
        Vector2 baselineDrift = _hasPlatformContactBaseline ? relativeOffset - _platformContactBaselineOffset : Vector2.zero;
        Vector2 deltaError = _currentPlatformDelta - _platformVelocity * Time.fixedDeltaTime;

        Debug.Log(
            $"[PlatformSyncDebug] mode={_debugPlatformSyncMode} grounded={_grounded} " +
            $"player={playerPos} platform={platformPos} rel={relativeOffset} baseline={_platformContactBaselineOffset} " +
            $"drift={baselineDrift} driftMag={baselineDrift.magnitude:F6} platformDelta={_currentPlatformDelta} " +
            $"platformVel={_platformVelocity} deltaError={deltaError}",
            this);
    }




    private void UpdateExternalForceState()
    {
        if (_externalForceLockFramesRemaining > 0)
        {
            _externalForceLockFramesRemaining--;
            return;
        }
        //if (_externalForceLockFramesRemaining > 0)
        //{
        //    _externalForceLockFramesRemaining--;
        //    return;
        //}

        if (_externalForce && _clearExternalForceOnGrounded && _grounded)
        {
            ClearExternalForce();
            return;
        }

        // External force is automatically released once launch/push momentum has
        // naturally settled on the ground.
        if (_externalForce && _grounded && _rb2D.velocity.sqrMagnitude <= 0.05f)
        {
            ClearExternalForce();
        }
    }


    private void ClearExternalForce()
    {
        _externalForce = false;
        _clearExternalForceOnGrounded = false;
    }
     public void ForceFacingDirection(float directionX)
    {
        if (Mathf.Approximately(directionX, 0f))
        {
            return;
        }

        _facingDir = Mathf.Sign(directionX);

        Vector3 eulerAngles = transform.eulerAngles;
        eulerAngles.y = _facingDir > 0f ? 0f : 180f;
        transform.eulerAngles = eulerAngles;
    }

    public void UpdateFacingDirection()
    {
        // During externally-authored mirror launch frames, avoid turn logic that can
        // zero horizontal velocity on slopes when players mash directional input.
        if (IsExternalForceControlActive)
        {
            if (Mathf.Abs(_rb2D.velocity.x) > 0.01f)
            {
                _facingDir = _rb2D.velocity.x > 0f ? 1f : -1f;
                transform.rotation = Quaternion.Euler(transform.rotation.x, _facingDir == 1 ? 0f : 180f, 0f);
            }
            return;
        }

        float zRotation = 0f;
        Vector2 moveInput = _controller.moveInput;

        if (moveInput.x != 0)
        {
            int newFacingDir = moveInput.x > 0 ? 1 : -1;

            if (_facingDir != newFacingDir)
            {
                _facingDir = newFacingDir;

                if (_changeFacingAfterTurnCoroutine != null) { StopCoroutine(_changeFacingAfterTurnCoroutine); }
                _changeFacingAfterTurnCoroutine = StartCoroutine(ChangeFacingAfterTurnCoroutine(zRotation));

                if (PlayerStatus.IsOnSlope && !PlayerStatus.IsOnSteepSlope)
                {
                    _rb2D.velocity = new Vector2(0f, _rb2D.velocity.y);
                }
                OnTurn?.Invoke();
            }
        }
        else if (!PlayerStatus.IsRefracted)
        {
            // Update _facingDir based on current rotation.y
            float yRotation = transform.rotation.eulerAngles.y;
            if (Mathf.Approximately(yRotation, 0f))
            {
                _facingDir = 1;
            }
            else if (Mathf.Approximately(yRotation, 180f))
            {
                _facingDir = -1;
            }
        }

        if (moveInput.x != 0 && _changeFacingAfterTurnCoroutine == null)
        {
            int newFacingDir = moveInput.x > 0 ? 1 : -1;
            transform.rotation = Quaternion.Euler(transform.rotation.x, newFacingDir == 1 ? 0 : 180f, 0);
        }

    }

    public void RefreshFacingDirection()
    {
        transform.rotation = Quaternion.Euler(transform.rotation.x, _facingDir == 1 ? 0 : 180f, 0);
    }

    private void UpdatePosition()
    {
        Vector3 currentPos = transform.position;
        if (currentPos != _lastPosition)
        {
            _lastMoveDirection = (currentPos - _lastPosition).normalized;
            _lastPosition = currentPos;
        }

        transform.position = new Vector3(transform.position.x, transform.position.y, 0f);
    }
    private void UpdateVelocityMetrics()
    {
        _velocity = _rb2D.velocity;
        _speed = _velocity.magnitude;
        _verticalVelocity = _velocity.y;
    }

    public void UpdateGrounded()
    {
        if (PlayerStatus.IsRefracted || PlayerStatus.HasMode(Mode.NoClip) || IsGravityBypassedByMode()) { _grounded = false; return; }
        if (_isGroundCheckDelayed) { _grounded = false; return; }

        int groundingLayerMask = LayerManager.GroundLayer;
        if (_debugExcludeWallsAndRoofsFromGrounding)
        {
            groundingLayerMask &= ~(1 << LayerManager.WallsAndRoofs);
        }

        RaycastHit2D hit = Physics2D.CircleCast(_groundPoint.position, _mainCollider2D.size.x / 2, -transform.up, _minGroundedDistance, groundingLayerMask);
        const float smallThreshold = 0.01f;
        _grounded = hit.collider != null;
        bool isBottomContact = _grounded && hit.point.y <= _groundPoint.position.y;
        if (_rb2D.velocity.y > smallThreshold && !isBottomContact)
        {
            _grounded = false;
        }

        if (!_grounded && TryPersistGroundingFromLastPlatform())
        {
            _grounded = true;
        }

        if (_grounded)
        {
            if (hit.collider != null)
            {
                _groundContactObject = hit.collider.gameObject;
            }

            if (!PlayerStatus.IsRefracted && Mathf.Abs(_rb2D.velocity.y) < smallThreshold && isBottomContact)
            {
                Vector2 moveInput = _controller.moveInput;
                if (moveInput.x != 0f)
                {
                    _rb2D.sharedMaterial = _noFricitionMaterial;
                }
                else
                {
                    _rb2D.sharedMaterial = _noFricitionMaterialStandingStill;
                }
                //ResetToDefaults();
            }

            _wasOnSlopeLastFrame = PlayerStatus.IsOnSlope || PlayerStatus.IsOnSteepSlope;
            if (_updateSlopeGroundedCoroutine == null)
            {
                //_updateSlopeGroundedCoroutine = StartCoroutine(UpdateSlopeGrounded(_groundContactObject));
            }

            _groundedTime += Time.fixedDeltaTime;
            OnGroundAction?.Invoke(this);
        }
        else
        {
            _groundContactObject = null;
            _noFricitionMaterial.friction = 0;
            _rb2D.sharedMaterial = _noFricitionMaterial;
            _groundedTime = 0;
        }

    }
    

    private bool TryPersistGroundingFromLastPlatform()
    {
        if (!_wasGroundedLastFrame || _lastPlatform == null || !_lastPlatform.IsStandableSurface || _groundPoint == null)
        {
            return false;
        }

        Collider2D platformCollider = _lastPlatform.GetComponentInChildren<Collider2D>();
        if (platformCollider == null)
        {
            return false;
        }

        if (_debugExcludeWallsAndRoofsFromGrounding && platformCollider.gameObject.layer == LayerManager.WallsAndRoofs)
        {
            return false;
        }

        float feetY = _groundPoint.position.y;
        float topY = platformCollider.bounds.max.y;
        float verticalGap = Mathf.Abs(feetY - topY);
        if (verticalGap > _platformPersistenceVerticalThreshold)
        {
            return false;
        }

        float left = platformCollider.bounds.min.x - _platformPersistenceHorizontalPadding;
        float right = platformCollider.bounds.max.x + _platformPersistenceHorizontalPadding;
        if (_groundPoint.position.x < left || _groundPoint.position.x > right)
        {
            return false;
        }

        _groundContactObject = platformCollider.gameObject;
        return true;
    }

    public void UpdateGravity()
    {
        if (PlayerStatus.IsRefracted || GameplayManager.IsGamePaused || IsGravityBypassedByMode()) { return; }
        if (_isControlLocked) { return; }
        if (IsFalling)
        {
            _currentGravity = _baseGravity * _fallGravityMultiplier;
        }
        else
        {
            _currentGravity = _baseGravity * _jumpAscentGravityMultiplier;
        }

        _rb2D.velocity += Vector2.down * _currentGravity * Time.fixedDeltaTime;
        _rb2D.velocity = new Vector2(_rb2D.velocity.x, Mathf.Clamp(_rb2D.velocity.y, -_fallClamp, _fallClamp));
    }

    public void UpdateSlopeDetection()
    {
        CheckSlope();
    }

    private static bool IsGravityBypassedByMode()
    {
        return PlayerStatus.HasMode(Mode.NoGravity) || PlayerStatus.HasMode(Mode.FloatingArea);
    }

    private void UpdatePhysicState()
    {
        OnPhysicsStateChanged?.Invoke(PhysicsState.Idle, IsIdle);
        OnPhysicsStateChanged?.Invoke(PhysicsState.Falling, IsFalling);
        OnPhysicsStateChanged?.Invoke(PhysicsState.Jumping, IsJumping);
        OnPhysicsStateChanged?.Invoke(PhysicsState.FloatingUp, IsFloatingUp);
        OnPhysicsStateChanged?.Invoke(PhysicsState.Moving, IsMoving);
        OnPhysicsStateChanged?.Invoke(PhysicsState.Walking, IsWalking);
        OnPhysicsStateChanged?.Invoke(PhysicsState.Pushed, HasExternalForce);

        bool isRefractedMoving = PlayerStatus.HasMode(Mode.Refracted) &&
                         _lastMoveDirection.sqrMagnitude > 0.01f;
        OnPhysicsStateChanged?.Invoke(PhysicsState.RefractedMoving, isRefractedMoving);
    }

    #endregion

    //------------------------------- Constraints Methods -------------------------------//

    public void DisableCollider(bool value)
    {
        _mainCollider2D.enabled = !value;
    }

    public void ConstrainToVerticalOnly()
    {
        _rb2D.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;
        _controller.moveInput = Vector2.zero;
        _rb2D.velocity = Vector2.zero;
    }
    public void SetFreeze(bool freeze)
    {
        if (freeze)
        {
            FreezeMovement();
        }
        else
        {
            UnFreezeMovement();
        }
    }
    public void FreezeMovement()
    {
        //if (_isFreezed) { return; }
        //_isFreezed = true;
        _cachedVelocity = _rb2D.velocity;

        _rb2D.velocity = Vector2.zero;
        _rb2D.gravityScale = 0f;
    }

    public void UnFreezeMovement()
    {
        if (PlayerStatus.HasMode(Mode.Cutscene) || PlayerStatus.HasMode(Mode.Refracted))
        {
            Debug.LogWarning("[PlayerPhysics] UnFreezeMovement ignored because player is in Cutscene or Refracted mode.");
            return;
        }

        ForceUnFreezeMovement();
    }

    public void ForceUnFreezeMovement()
    {
        //if (!_isFreezed) { return; }
        //_isFreezed = false;
        _rb2D.velocity = _cachedVelocity;
        _rb2D.gravityScale = Default_GravityScale;
    }

    public void RestoreDefaultGravityScale()
    {
        _rb2D.gravityScale = Default_GravityScale;
    }

    public void ZeroVelocity()
    {
        _rb2D.velocity = Vector2.zero;
    }

    public void MoveTowardsZeroVelocity(float duration = 1f, float rate = 0.25f)
    {
        if (_moveTowardsZeroVelocityCoroutine != null)
        {
            StopCoroutine(_moveTowardsZeroVelocityCoroutine);
        }

        _moveTowardsZeroVelocityCoroutine =
            StartCoroutine(MoveTowardsZeroVelocityCoroutine(duration, rate));
    }

    public void DisablePlayerControlPhysics()
    {
        if (_isControlLocked) { return; }
        _isControlLocked = true;

        _cachedControlLockVelocity = _rb2D.velocity;
        _rb2D.bodyType = RigidbodyType2D.Kinematic;
        _cachedControlLockGravityScale = _rb2D.gravityScale;

        _rb2D.velocity = Vector2.zero;
        _rb2D.gravityScale = 0f;
        _rb2D.isKinematic = true;
    }

    public void RestorePlayerControlPhysics()
    {
        if (!_isControlLocked) { return; }
        _isControlLocked = false;

        _rb2D.bodyType = RigidbodyType2D.Dynamic;
        _rb2D.gravityScale = _cachedControlLockGravityScale;
        _rb2D.velocity = _cachedControlLockVelocity;
    }

    public void EnterRefractedState()
    {
        PlayerStatus.AddMode(Mode.Refracted);
        _rb2D.velocity = Vector2.zero;
        _rb2D.gravityScale = 0f;
    }


    //------------------------------- Slope Detection Methods -------------------------------//

    #region Slope
    private void CheckSlope()
    {
        Vector2 checkPos = (Vector2)transform.position - new Vector2(0f, (_mainCollider2D.size.y / 2));

        CheckObstaclesAhead();
        CheckHorizontalSlope(checkPos);
        CheckVerticalSlope(checkPos);
        CheckSlopeAhead(checkPos);
        CheckSteepSlopeAhead();

        // Update overall slope state based on conditions
        _isOnSlope = _isOnSteepSlope || _isOnVerticalSlope || _isOnHorizontalSlope && !_isSlopeAhead;

        if (_isOnSlope) { PlayerStatus.AddState(PhysicsState.OnSlope); }
        else { PlayerStatus.RemoveState(PhysicsState.OnSlope); }
    }

    /// <summary>
    /// Detects obstacles ahead when the player is grounded and not on a steep slope.
    /// Uses overlap checks and raycasts to identify obstacles, adjusting friction,
    /// stopping movement, and disabling jumping if a steep obstacle is detected.
    /// </summary>
    private void CheckObstaclesAhead()
    {
        if (!_grounded || _isOnSteepSlope) return;

        Bounds bounds = _mainCollider2D.bounds;
        float xBounds = (_facingDir > 0) ? bounds.max.x : bounds.min.x;
        Vector2 topLeft = new(xBounds, bounds.center.y);
        Vector2 bottomRight = new(xBounds, bounds.max.y);

        // Combine obstacle and ground checks
        Collider2D[] hits = Physics2D.OverlapAreaAll(topLeft, bottomRight, LayerManager.ObstacleAheadLayer | LayerManager.GroundLayer);
        bool obstacleDetected = DetectObstacle(hits, bounds);

        if (!obstacleDetected)
        {
            Vector2 rayOrigin = new(bounds.center.x, bounds.max.y);
            RaycastHit2D[] rayHits = Physics2D.RaycastAll(rayOrigin, transform.right, _obstacleAheadCheckDistance, LayerManager.ObstacleAheadLayer);
            obstacleDetected = DetectObstacleFromRaycast(rayHits);
        }

        _noFricitionMaterial.friction = obstacleDetected ? 100f : 0f;
        _isObstacleAhead = obstacleDetected;
    }

    private bool DetectObstacle(Collider2D[] hits, Bounds bounds)
    {
        bool obstacleDetected = false;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hitCol = hits[i];
            if (!hitCol) { continue; }

            if (IsStairs(hitCol))
            {
                _jump.groundedCheckDelay = PlayerJump.STAIRS_GROUND_CHECK_DELAY;
            }

            Vector2 contactPoint = hitCol.ClosestPoint(bounds.center);
            Vector2 contactNormal = (bounds.center - (Vector3)contactPoint).normalized;
            float obstacleAngle = Vector2.Angle(contactNormal, Vector2.up);

            if ((Mathf.Ceil(obstacleAngle) >= 89.999f && IsMovingUpSlope && _controller.moveInput.x != 0) || _isSteepSlopeAhead)
            {
                _rb2D.velocity = Vector2.zero;
                obstacleDetected = true;
                break;
            }
        }

        _jump.DisableJump(obstacleDetected);
        return obstacleDetected;
    }


    private bool DetectObstacleFromRaycast(RaycastHit2D[] rayHits)
    {
        for (int i = 0; i < rayHits.Length; i++)
        {
            if (rayHits[i].collider != null)
            {
                float raycastObstacleAngle = Vector2.Angle(rayHits[i].normal, Vector2.up);
                if (Mathf.Ceil(raycastObstacleAngle) >= 89.999f && IsMovingUpSlope && _controller.moveInput.x != 0)
                {
                    _rb2D.velocity = Vector2.zero;
                    _jump.DisableJump(true);
                    return true;
                }
                else
                {
                    _jump.DisableJump(false);
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Checks for horizontal slopes in the specified position by casting raycasts in both directions (front and back).
    /// Determines if the player is on a horizontal slope and if they are moving up the slope, based on the raycast hits.
    /// Sets the appropriate flags (_isOnHorizontalSlope, _movingUpSlope) and calculates the slope angle for further processing.
    /// </summary>
    private void CheckHorizontalSlope(Vector2 checkPos)
    {
        RaycastHit2D slopeHitFront = Physics2D.Raycast(checkPos, transform.right, _slopeCheckDistance, LayerManager.GroundLayer);
        RaycastHit2D slopeHitBack = Physics2D.Raycast(checkPos, -transform.right, _slopeCheckDistance, LayerManager.GroundLayer);

        _isOnHorizontalSlope = false;
        _isMovingUpSlope = false;

        if (slopeHitFront || slopeHitBack)
        {
            RaycastHit2D hit = slopeHitFront ? slopeHitFront : slopeHitBack;

            _slopeSideAngle = Vector2.Angle(hit.normal, Vector2.up);
            _isOnHorizontalSlope = Mathf.Abs(_slopeSideAngle) < _slopeSideAngleThreshold;

            if (slopeHitFront)
            {
                _isMovingUpSlope = hit.normal.y > 0 && (_isOnSlope || _isOnSteepSlope);
            }
        }
    }

    /// <summary>
    /// Checks the vertical slope at the given position and updates the related flags and restrictions based on the detected slope angle.
    /// </summary>
    private void CheckVerticalSlope(Vector2 checkPos)
    {
        RaycastHit2D slopeHit = Physics2D.Raycast(checkPos, Vector2.down, _slopeCheckDistance, LayerManager.GroundLayer);

        _isOnVerticalSlope = false;
        _isOnSteepSlope = false;
        _isOnFlatSurface = false;
        _jump.SetSteepSlopeJumpRestriction(false);


        if (slopeHit)
        {
            _slopeNormalPerp = Vector2.Perpendicular(slopeHit.normal * _facingDir).normalized;
            _slopeDownAngle = Vector2.Angle(slopeHit.normal, Vector2.up);

            float perpAngle = Vector2.Angle(Vector2.down, _slopeNormalPerp * _facingDir);

            _isOnVerticalSlope = (_slopeDownAngle > _slopeAngleThreshold) && (_slopeDownAngle < _steepSlopeAngleThreshold);
            _isOnSteepSlope = _slopeDownAngle >= _steepSlopeAngleThreshold;
            _isOnFlatSurface = Mathf.Abs(perpAngle - 90f) <= 5f;

            _jump.SetSteepSlopeJumpRestriction(_slopeDownAngle >= _steepSlopeAngleJumpThreshold);

            if (slopeHit.transform.gameObject.layer == LayerManager.WallsAndRoofs)
            {
                _isOnSteepSlope = true;
            }

            if (_isOnSteepSlope) { PlayerStatus.AddState(PhysicsState.OnSteepSlope); }
            else { PlayerStatus.RemoveState(PhysicsState.OnSteepSlope); }
        }
    }

    /// <summary>
    /// Checks if there is a slope ahead of the player by performing multiple raycasts.
    /// Determines the slope angle and updates the <c>_isSlopeAhead</c> and <c>_slopeAheadAngle</c> based on the detected slope conditions.
    /// The method also checks if the slope is steep enough to meet the specified threshold to be considered a valid slope for the player to interact with.
    /// </summary>
    /// <param name="checkPos">The position from which the raycast will be fired to check for slopes ahead of the player.</param>

    private void CheckSlopeAhead(Vector2 checkPos)
    {

        if (!_grounded)
        {
            _isSlopeAhead = false;
            return;
        }

        RaycastHit2D slopeHitAhead = Physics2D.Raycast(checkPos, transform.right, _slopeAheadCheckDistance, LayerManager.GroundLayer);

        if (slopeHitAhead)
        {
            Vector2 contactPoint = slopeHitAhead.point;
            RaycastHit2D slopeHitBelow = Physics2D.Raycast(contactPoint, Vector2.down, _slopeVerticalCheckThreshold, LayerManager.GroundLayer);

            if (slopeHitBelow && slopeHitBelow.collider != slopeHitAhead.collider)
            {
                RaycastHit2D slopeHitBack = Physics2D.Raycast(slopeHitBelow.point, -transform.right, _slopeCheckDistance, LayerManager.GroundLayer);
                float slopeAheadAngle = Vector2.Angle(slopeHitBack.normal, Vector2.up);

                _slopeAheadAngle = slopeAheadAngle;
                _isSlopeAhead = slopeAheadAngle > 0 && slopeAheadAngle >= _clifAngleThreshold;
            }
            else
            {
                _isSlopeAhead = false;
            }
        }
        else
        {
            Vector2 downwardCheckPos = checkPos + (Vector2)transform.right * _slopeAheadCheckDistance;
            RaycastHit2D slopeHitDirectBelow = Physics2D.Raycast(downwardCheckPos, Vector2.down, _slopeVerticalCheckThreshold, LayerManager.GroundLayer);

            if (slopeHitDirectBelow)
            {
                float slopeAheadAngle = Vector2.Angle(slopeHitDirectBelow.normal, Vector2.up);
                _slopeAheadAngle = slopeAheadAngle;
                _isSlopeAhead = slopeAheadAngle > 0 && slopeAheadAngle <= _clifAngleThreshold;
            }
            else
            {
                _isSlopeAhead = false;
            }
        }
    }

    public void CheckSteepSlopeAhead()
    {
        // State
        _isSteepSlopeAhead = false;

        // Early outs
        if (!_grounded) return;

        float moveX = _controller != null ? _controller.moveInput.x : 0f;
        if (Mathf.Approximately(moveX, 0f)) return;

        bool isMovingForward = Mathf.Sign(moveX) == Mathf.Sign(_facingDir);
        if (!isMovingForward) return;

        // Parameters
        float halfWidth = _mainCollider2D.size.x * 0.5f * transform.lossyScale.x;

        float baseOffset = halfWidth + 0.05f; // just outside collider
        float step = _steepSlopeAheadCheckDistance * 0.5f;

        // Sample positions ahead (2–3 points)
        float[] offsets =
        {
        baseOffset,
        baseOffset + step,
        baseOffset + step * 2f
    };

        // Cast distance (ensure it reaches below feet)
        float castDistance = _mainCollider2D.size.y * transform.lossyScale.y + _slopeVerticalCheckThreshold;

        for (int i = 0; i < offsets.Length; i++)
        {
            Vector2 origin = (Vector2)transform.position
                + Vector2.right * _facingDir * offsets[i];

            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, castDistance, LayerManager.GroundLayerFiltered);
            if (!hit) continue;

            float slopeAngle = Vector2.Angle(hit.normal, Vector2.up);

            if (slopeAngle >= _steepSlopeAngleThreshold)
            {
                _isSteepSlopeAhead = true;
                return;
            }
        }
    }

    #endregion

    //------------------------------- External Force Methods -------------------------------//

    #region External Force

    public enum MirrorLaunchMode
    {
        Parabola,
        Normal
    }

    public IEnumerator LaunchFromMirror(
        Vector3 startPosition,
        Transform directionTransform,
        float verticalForce,
        float horizontalForce,
        float force,
        MirrorLaunchMode launchMode,
        PlayerExitFacing exitFacing = PlayerExitFacing.Default,
        int inputProtectionFixedFrames = 8
    )
    {
        while (GameplayManager.IsGamePaused)
        {
            yield return null;
        }

        _externalForce = true;
        _clearExternalForceOnGrounded = false;
        _externalForceLockFramesRemaining = Mathf.Max(_externalForceLockFramesRemaining, inputProtectionFixedFrames);
        transform.position = startPosition;

        Vector2 launchDirection = directionTransform.right;
        Vector2 upDirection = directionTransform.up;
        Vector2 velocity;

        if (launchMode == MirrorLaunchMode.Parabola)
        {
            velocity = (upDirection * verticalForce) + (launchDirection * horizontalForce);
            _rb2D.velocity = velocity;
        }
        else
        {
            _rb2D.velocity = Vector2.zero;
            _rb2D.AddForce(launchDirection.normalized * force, ForceMode2D.Impulse);
            velocity = _rb2D.velocity;
        }

        int horizontalDirection = velocity.x >= 0 ? 1 : -1;
        transform.rotation = Quaternion.Euler(0f, horizontalDirection > 0 ? 0f : 180f, 0f);

        float deltaVX = Mathf.Abs(velocity.x);
        float deltaVY = Mathf.Abs(velocity.y);
        ApplySpeedClampModifiers(deltaVX, deltaVY);

        switch (exitFacing)
        {
            case PlayerExitFacing.ForceRight:
                transform.right = Vector2.right;
                break;
            case PlayerExitFacing.ForceLeft:
                transform.right = Vector2.left;
                break;
            default:
                transform.right = Vector2.Dot(directionTransform.right, Vector2.right) > 0 ? Vector2.right : Vector2.left;
                break;
        }

        yield break;
    }

    public IEnumerator LaunchFromMirror(
        Vector3 startPosition,
        Vector2 direction,
        float force,
        PlayerExitFacing exitFacing = PlayerExitFacing.Default,
        int inputProtectionFixedFrames = 8
    )
    {
        while (GameplayManager.IsGamePaused)
        {
            yield return null;
        }

        _externalForce = true;
        _clearExternalForceOnGrounded = false;
        _externalForceLockFramesRemaining = Mathf.Max(_externalForceLockFramesRemaining, inputProtectionFixedFrames);
        transform.position = startPosition;

        Vector2 launchDirection = direction.normalized;
        _rb2D.velocity = Vector2.zero;
        _rb2D.AddForce(launchDirection * force, ForceMode2D.Impulse);
        Vector2 velocity = _rb2D.velocity;

        int horizontalDirection = velocity.x >= 0 ? 1 : -1;
        transform.rotation = Quaternion.Euler(0f, horizontalDirection > 0 ? 0f : 180f, 0f);

        ApplySpeedClampModifiers(Mathf.Abs(velocity.x), Mathf.Abs(velocity.y));

        switch (exitFacing)
        {
            case PlayerExitFacing.ForceRight:
                transform.right = Vector2.right;
                break;
            case PlayerExitFacing.ForceLeft:
                transform.right = Vector2.left;
                break;
            default:
                transform.right = Vector2.Dot(direction, Vector2.right) > 0 ? Vector2.right : Vector2.left;
                break;
        }

        yield break;
    }

    public IEnumerator LaunchFromShatteredDimension(Vector2 direction, float force, Vector2 capturedVelocity, int inputProtectionFixedFrames = 8)
    {
        while (GameplayManager.IsGamePaused)
        {
            yield return null;
        }

        _externalForce = true;
        _clearExternalForceOnGrounded = true;
        _externalForceLockFramesRemaining = Mathf.Max(_externalForceLockFramesRemaining, inputProtectionFixedFrames);

        Vector2 launchDirection = GetShatteredDimensionLaunchDirection(capturedVelocity, direction);
        _rb2D.velocity = capturedVelocity;
        _rb2D.AddForce(launchDirection * force, ForceMode2D.Impulse);
        Vector2 velocity = _rb2D.velocity;

        int horizontalDirection = velocity.x >= 0 ? 1 : -1;
        transform.rotation = Quaternion.Euler(0f, horizontalDirection > 0 ? 0f : 180f, 0f);

        yield break;
    }

    public IEnumerator LaunchFromShatteredDimension(Vector2 direction, float force, int inputProtectionFixedFrames = 8)
    {
        Vector2 capturedVelocity = _rb2D != null ? _rb2D.velocity : Vector2.zero;
        yield return LaunchFromShatteredDimension(direction, force, capturedVelocity, inputProtectionFixedFrames);
    }

    private Vector2 GetShatteredDimensionLaunchDirection(Vector2 capturedVelocity, Vector2 fallbackDirection)
    {
        const float minLaunchDirectionSqrMagnitude = 0.0001f;

        if (capturedVelocity.sqrMagnitude > minLaunchDirectionSqrMagnitude)
        {
            return capturedVelocity.normalized;
        }

        if (fallbackDirection.sqrMagnitude > minLaunchDirectionSqrMagnitude)
        {
            return fallbackDirection.normalized;
        }

        if (Mathf.Abs(_facingDir) > 0.01f)
        {
            return new Vector2(Mathf.Sign(_facingDir), 0f);
        }

        return Vector2.up;
    }

    public void ApplyForce(Vector2 direction, float force, ForceMode2D forceMode, bool momentum)
    {
        _externalForce = true;
        _clearExternalForceOnGrounded = false;

        if (!momentum) { _rb2D.velocity = Vector2.zero; }

        float deltaVX = Mathf.Abs((direction.x * force / _rb2D.mass) * Time.fixedDeltaTime);
        float deltaVY = Mathf.Abs((direction.y * force / _rb2D.mass) * Time.fixedDeltaTime);

        ApplySpeedClampModifiers(deltaVX, deltaVY);
        //_rb2D.gravityScale = 1f; // Refactor
        _rb2D.AddForce(direction.normalized * force, forceMode);
    }

    public void LaunchInParabola(Vector3 startPosition, Transform launchPoint, float initialVerticalForce, float initialHorizontalForce, int facingDir)
    {
        _externalForce = true;
        _clearExternalForceOnGrounded = false;
        transform.position = startPosition;

        // Determine launch directions based on launch point's orientation
        Vector2 launchDirection = launchPoint.right * facingDir;
        Vector2 upDirection = launchPoint.up;

        // Calculate the initial velocity using the proper directions
        Vector2 velocity = (upDirection * initialVerticalForce) + (launchDirection * initialHorizontalForce);
        _rb2D.velocity = velocity;

        // Set player's facing direction to match horizontal movement direction
        int horizontalDirection = velocity.x > 0 ? 1 : -1;
        transform.rotation = Quaternion.Euler(0f, horizontalDirection > 0 ? 0f : 180f, 0f);

        // Apply speed clamp modifiers for proper movement constraints
        float deltaVX = Mathf.Abs(velocity.x);
        float deltaVY = Mathf.Abs(velocity.y);
        ApplySpeedClampModifiers(deltaVX, deltaVY);
    }

    public void LaunchInParabola(
        Vector3 startPosition,
        Transform directionTransform,
        float verticalForce,
        float horizontalForce,
        float duration,
        PlayerExitFacing exitFacing = PlayerExitFacing.Default // <-- new parameter, default for backward compatibility
    )
    {
        LaunchFromMirror(
            startPosition,
            directionTransform,
            verticalForce,
            horizontalForce,
            0f,
            MirrorLaunchMode.Parabola,
            exitFacing
        );
    }

    public void MoveAndApplyForce(Vector3 position, Vector2 direction, float force, ForceMode2D forceMode, bool momentum)
    {
        transform.position = position;
        ApplyForce(direction, force, forceMode, momentum);
    }

    public void MoveAndApplyForce(
        Vector3 position,
        Vector2 direction,
        float force,
        ForceMode2D forceMode,
        bool someFlag,
        PlayerExitFacing exitFacing = PlayerExitFacing.Default
    )
    {
        // Legacy wrapper kept for compatibility with non-mirror call sites.
        LaunchFromMirror(position, direction, force, exitFacing);
    }

    private void ApplySpeedClampModifiers(float xMod, float yMod)
    {
        _movement.AddMoveSpeedClamp(xMod);
        _jump.AddJumpForceClamp(yMod);
        //_fallClampModifer += yMod;
    }

    #endregion

    //------------------------------- Physics2D Material Methods -------------------------------//
    public void SetPhysics2DMaterial()
    {
        if (_controller.moveInput.x == 0 || IsForwardMovementBlockedBySteepSlope)
        {
            _rb2D.sharedMaterial = _isOnSteepSlope ? _noFricitionMaterial : _noFricitionMaterialStandingStill;
            return;
        }

        switch (PlayerStatus.CurrentState)
        {
            case PhysicsState.OnSlope:
                _rb2D.sharedMaterial = _noFricitionMaterial;
                break;

            case PhysicsState.OnSteepSlope:
                //_rb2D.sharedMaterial = _fullFricitionMaterial;
                _rb2D.sharedMaterial = _noFricitionMaterial;
                break;

            default:
                _rb2D.sharedMaterial = _noFricitionMaterial;
                break;
        }
    }

    //------------------------------- Coroutines -------------------------------//

    #region Coroutines

    private Coroutine _temporarilyDisableGroundCheckCoroutine;
    private bool _isGroundCheckDelayed;

    private Coroutine _updateSlopeGroundedCoroutine;
    public float translateAdjust = 10f;

    private Coroutine _changeFacingAfterTurnCoroutine;

    private Coroutine _moveTowardsZeroVelocityCoroutine;

    /// <summary>
    /// Temporarily disables the ground check for a specified number of FixedUpdate frames.
    /// Ensures only one delay coroutine runs at a time.
    /// </summary>
    /// <param name="fixedUpdateCount">Number of FixedUpdate frames to delay the ground check.</param>
    public void TemporarilyDisableGroundCheck(int fixedUpdateCount = 4)
    {
        if (_temporarilyDisableGroundCheckCoroutine != null)
        {
            StopCoroutine(_temporarilyDisableGroundCheckCoroutine);
        }

        _temporarilyDisableGroundCheckCoroutine = StartCoroutine(GroundCheckDelayCoroutine(fixedUpdateCount));
    }

    private IEnumerator ChangeFacingAfterTurnCoroutine(float zRotation)
    {
        PlayerStatus.AddState(PhysicsState.Turning);
        AnimationEvents.InvokeTurn(this, true);

        //yield return new WaitForSeconds(0.1f);
        yield return new WaitForFixedUpdate();

        AnimationEvents.InvokeTurn(this, false);

        int turnHash = AnimationManager.CharacterAnimations.turnHash;
        while (_animationBinder.animator.GetCurrentAnimatorStateInfo(0).shortNameHash == turnHash)
        {
            yield return null;
        }

        PlayerStatus.RemoveState(PhysicsState.Turning);

        transform.rotation = Quaternion.Euler(transform.rotation.x, _facingDir == 1 ? 0 : 180f, -zRotation);
        _changeFacingAfterTurnCoroutine = null;
    }


    private IEnumerator GroundCheckDelayCoroutine(int fixedUpdateCount)
    {
        _isGroundCheckDelayed = true;

        for (int i = 0; i < fixedUpdateCount; i++)
        {
            yield return new WaitForFixedUpdate();
        }

        _jump.groundedCheckDelay = PlayerJump.DEFAULT_GROUND_CHECK_DELAY;

        _jump.ResetJumpBuffer();
        _isGroundCheckDelayed = false;
        _temporarilyDisableGroundCheckCoroutine = null;
    }

    private IEnumerator UpdateSlopeGrounded(GameObject groundContactObject)
    {
        // Wait a few frames to confirm the player is truly airborne
        for (int i = 0; i < 4; i++)
        {
            yield return new WaitForFixedUpdate();
        }

        if (!_externalForce && _wasOnSlopeLastFrame && !IsMovingUpSlope && !_grounded)
        {
            float thresholdDistance = 0.2f; // Threshold before considering airborne
            float downwardForce = -2f; // Strength of downward correction
            int airborneFrames = 0;
            int maxAirborneFrames = 5; // Max frames before applying correction

            // Wait until the distance from the last ground contact object exceeds the threshold
            while (groundContactObject && Vector2.Distance(transform.position, groundContactObject.transform.position) <= thresholdDistance)
            {
                if (IsMovingUpSlope || _externalForce) { yield break; }

                if (!_grounded)
                {
                    airborneFrames++;
                }
                else
                {
                    airborneFrames = 0; // Reset if player regains ground
                }

                if (airborneFrames > maxAirborneFrames) { break; }

                yield return new WaitForFixedUpdate();
            }

            if (IsMovingUpSlope || _externalForce) { yield break; }

            // Perform a downward velocity adjustment to smoothly land
            RaycastHit2D hit = Physics2D.CircleCast(
                _groundPoint.position,
                _mainCollider2D.size.x / 2,
                -transform.up,
                _minGroundedDistance * 100f,
                LayerManager.GroundLayer
            );

            if (hit.collider != null)
            {
                float targetY = hit.point.y;

                while (Mathf.Abs(transform.position.y - targetY) > 0.01f || !_grounded)
                {
                    if (IsMovingUpSlope || _externalForce) { break; }

                    // Apply a downward force instead of direct translation
                    _rb2D.velocity = new Vector2(_rb2D.velocity.x, downwardForce);

                    yield return new WaitForFixedUpdate();
                }

                // Ensure final grounding
                _grounded = true;
            }
        }

        _updateSlopeGroundedCoroutine = null;
    }

    private IEnumerator MoveTowardsZeroVelocityCoroutine(float duration, float rate)
    {
        float timer = 0f;

        while (timer < duration)
        {
            _rb2D.velocity = Vector2.Lerp(
                _rb2D.velocity,
                Vector2.zero,
                rate * Time.fixedDeltaTime);

            timer += Time.fixedDeltaTime;

            yield return new WaitForFixedUpdate();
        }

        _moveTowardsZeroVelocityCoroutine = null;
    }

    #endregion

    //------------------------------- Reset Methods -------------------------------//

    public void ResetPhysics()
    {
        //_externalForceResistance = Default_ExternalForceResistance;
        ClearExternalForce();
        _externalForceLockFramesRemaining = 0;

        _rb2D.gravityScale = Default_GravityScale;
        _rb2D.constraints = RigidbodyConstraints2D.FreezeRotation;

        //if (_externalForce)
        //{
        //    if (_resetExternalForceCoroutine != null)
        //    {
        //        StopCoroutine(_resetExternalForceCoroutine);
        //        _resetExternalForceCoroutine = null;
        //    }
        //    _resetExternalForceCoroutine = StartCoroutine(ResetExternalForce());
        //}


    }

    public void ResetState()
    {
        if (_temporarilyDisableGroundCheckCoroutine != null)
        {
            StopCoroutine(_temporarilyDisableGroundCheckCoroutine);

            if (_jump != null)
            {
                _jump.groundedCheckDelay = PlayerJump.DEFAULT_GROUND_CHECK_DELAY;
                _jump.ResetJumpBuffer();
            }
            _isGroundCheckDelayed = false;
            _temporarilyDisableGroundCheckCoroutine = null;
        }

        StopTrackedCoroutine(ref _resetExternalForceCoroutine);
        StopTrackedCoroutine(ref _updateSlopeGroundedCoroutine);
        StopTrackedCoroutine(ref _changeFacingAfterTurnCoroutine);
        StopTrackedCoroutine(ref _moveTowardsZeroVelocityCoroutine);

        RestorePlayerControlPhysics();
        ForceUnFreezeMovement();

        DetachPlatform();
        _currentPlatform = null;
        _lastPlatform = null;
        _groundContactObject = null;
        _currentPlatformDelta = Vector2.zero;
        _platformVelocity = Vector2.zero;
        _hasPlatformContactBaseline = false;

        _isOnSlope = false;
        _isSlopeAhead = false;
        _isSteepSlopeAhead = false;
        _isOnSteepSlope = false;
        _isOnFlatSurface = false;
        _isOnVerticalSlope = false;
        _isOnHorizontalSlope = false;
        _isMovingUpSlope = false;
        _isObstacleAhead = false;
        _wasOnSlopeLastFrame = false;
        _wasGroundedLastFrame = false;
        _isTouchingWallsAndRoofs = false;

        _wallsAndRoofsEjectionDirection = Vector2.zero;
        _wallsAndRoofsContactPoint = Vector2.zero;
        _wallsAndRoofsContactNormal = Vector2.zero;
        ResetWallsAndRoofsEjection();

        _rb2D.bodyType = RigidbodyType2D.Dynamic;
        _rb2D.isKinematic = false;
        _rb2D.velocity = Vector2.zero;
        _rb2D.angularVelocity = 0f;
    }

    private void StopTrackedCoroutine(ref Coroutine coroutine)
    {
        if (coroutine == null) { return; }
        StopCoroutine(coroutine);
        coroutine = null;
    }

    IEnumerator ResetExternalForce()
    {
        //float j = _externalForceLockFramesRemaining;
        //for (int i = 0; i < _externalForceLockFramesRemaining; i++)
        //{
        //    yield return new WaitForFixedUpdate();
        //    Debug.Log("Resetting external force... Frame " + (i + 1));
        //}

        ////_externalForce = false;
        ////_externalForceLockFramesRemaining = 0;
        ///

        int i = _externalForceLockFramesRemaining;

        while (true)
        {
            if ((_externalForce && _grounded) || i >= 0)
            {
                _externalForce = false;
                break;
            }
            else
            {
                i--;
                yield return new WaitForFixedUpdate();
            }
        }

        _resetExternalForceCoroutine = null;
    }

    private void OnDrawGizmos()
    {
        DrawWallsAndRoofsEjectionGizmos();
    }

    private void DrawWallsAndRoofsEjectionGizmos()
    {
        if (!_debugDrawWallsAndRoofsEjectionGizmos)
        {
            return;
        }

        if (!IsWallsAndRoofsEjectionActive)
        {
            return;
        }

        Vector2 ejectionDirection = _wallsAndRoofsEjectionDirection;
        if (ejectionDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            ejectionDirection = _wallsAndRoofsContactNormal;
        }

        Vector3 contactPoint = new(
            _wallsAndRoofsContactPoint.x,
            _wallsAndRoofsContactPoint.y,
            transform.position.z
        );

        Vector3 ejectionEndPoint = contactPoint + (Vector3)(ejectionDirection.normalized * _debugWallsAndRoofsEjectionGizmoLength);

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(contactPoint, _debugWallsAndRoofsEjectionContactRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(contactPoint, ejectionEndPoint);
        DrawGizmoArrowHead(contactPoint, ejectionEndPoint, _debugWallsAndRoofsEjectionArrowHeadLength);

        Collider2D playerCollider = _mainCollider2D != null ? _mainCollider2D : GetComponent<CapsuleCollider2D>();

        if (playerCollider != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(playerCollider.bounds.center, contactPoint);
        }

#if UNITY_EDITOR
    UnityEditor.Handles.Label(
        contactPoint + Vector3.up * 0.15f,
        "WallsAndRoofs ejection active: " + _remainingWallsAndRoofsEjectionDistance.ToString("F2") + " remaining"
    );
#endif
    }

    private void DrawGizmoArrowHead(Vector3 startPoint, Vector3 endPoint, float length)
    {
        Vector3 direction = endPoint - startPoint;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        direction.Normalize();

        Vector3 right = Quaternion.Euler(0f, 0f, 150f) * direction;
        Vector3 left = Quaternion.Euler(0f, 0f, -150f) * direction;

        Gizmos.DrawLine(endPoint, endPoint + right * length);
        Gizmos.DrawLine(endPoint, endPoint + left * length);
    }

    //------------------------------- Helpers -------------------------------//

    private bool IsStairs(Collider2D col)
    {
        if (!col) { return false; }

        Transform t = col.transform;
        //int id = t.GetInstanceID();

        if (t.CompareTag("Stairs"))
        {
            //_isStairsCache[id] = true;
            Debug.Log("Stairs detected: " + t.name);
            return true;
        }
        else return false;

        //if (_isStairsCache.TryGetValue(id, out bool isStairs)) { return isStairs; }

        //isStairs = t.GetComponentInParent<StairsTool>() != null;
        //_isStairsCache[id] = isStairs;
        //return isStairs;
    }


}
