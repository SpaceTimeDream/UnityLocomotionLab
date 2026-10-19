using GameSystem;
using PlayerSystem;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    // ------------------------------- Debug ------------------------------- //

    public float HorizontalDistancePerFrame => _horizontalDistancePerFrame;

    //------------------------------- Components -------------------------------//

    private Rigidbody2D _rb2D;

    //------------------------------- Modules -------------------------------//

    private PlayerController _controller;
    private PlayerPhysics _physics;
    private PlayerJump _jump;

    private GameInput _gameInput;

    //------------------------------- Move Parameters -------------------------------//

    [Header("MOVE PARAMETERS")]

    [SerializeField] private float _moveSpeed = 5f;
    public float moveSpeed
    {
        get { return _moveSpeed; }
        set { _moveSpeed = value; }
    }

    [SerializeField] private float _moveAcceleration = 20f;
    public float moveAcceleration
    {
        get { return _moveAcceleration; }
        set { _moveAcceleration = value; }
    }

    [SerializeField] private float _moveDeceleration = 20f;
    public float moveDeceleration
    {
        get { return _moveDeceleration; }
        set { _moveDeceleration = value; }
    }

    [SerializeField] private float _moveSpeedClampModifier;
    public float moveSpeedClampModifier => _moveSpeedClampModifier;

    [SerializeField] private float _slopeMoveAcceleration = 20f;
    [SerializeField] private float _slopeMoveDeceleration = 20f;

    [SerializeField] private float _airMoveAcceleration = 20f;
    [SerializeField] private float _airMoveDeceleration = 20f;

    [Header("FLOATING MOVE PARAMETERS")]
    [SerializeField] private float _floatingMoveAcceleration = 55f;
    [SerializeField] private float _floatingMoveDeceleration = 30f;
    [SerializeField] private float _floatingMoveSpeedClamp = 18f;
    [SerializeField] private float _floatingSpeedClampModifier;

    private Vector2 _floatingExternalVelocity;

    private float Default_Max_Horizontal_Speed;
    private float Default_Horizontal_Acceleration;

    private float Default_MoveSpeedClampModifier;

    private bool _isInitialized;

    //------------------------------- Unity Methods -------------------------------//

    #region Unity

    private void Start()
    {
        Initialize();
    }

    private void OnDisable()
    {
    }

    private void OnDestroy()
    {
    }

    private Vector2 _previousPosition;
    private float _horizontalDistancePerFrame;

    private void FixedUpdate()
    {
        CalculateHorizontalDistance();
    }

    private void CalculateHorizontalDistance()
    {
        _horizontalDistancePerFrame = Mathf.Abs(_rb2D.position.x - _previousPosition.x);
        _previousPosition = _rb2D.position;

        //Debug.Log($"Distance Per Frame: {_horizontalDistancePerFrame}, Expected: {_moveSpeed * Time.fixedDeltaTime}");
    }

    #endregion

    //------------------------------- Initialization -------------------------------//

    #region Initialization

    private void Initialize()
    {
        if (_isInitialized) { return; }
        InitializeModules();
        InitializeComponents();

        Default_MoveSpeedClampModifier = _moveSpeedClampModifier;
        _isInitialized = true;
    }

    private void InitializeComponents()
    {
        if (_rb2D == null) { _rb2D = _controller.rb2D; }
    }

    private void InitializeModules()
    {
        if (_controller == null) _controller = GetComponent<PlayerController>();
        if (_physics == null) _physics = _controller.physics;
        if (_jump == null) _jump = _controller.jump;

        _gameInput = GameInput.Instance;
    }

    #endregion

    //------------------------------- Update Methods -------------------------------//

    #region Update

    public void UpdateMovement()
    {
        if (!_isInitialized) { return; }
        if (GameplayManager.IsGamePaused) { return; }
        if (!PlayerStatus.HasMode(Mode.FloatingArea))
        {
            Move();
        }
        else
        {
            FloatingMove();
        }
    }

    #endregion

    //------------------------------- Move Method -------------------------------//

    #region Move

    private void Move()
    {
        // Mirror launches/pushes are protected by an external-force lock window
        // so directional input cannot immediately weaken authored launch velocity.
        if (_physics.IsExternalForceControlActive)
        {
            return;
        }

        if (_physics.IsSteepSlopeAhead)
        {
            _controller.moveInput = Vector2.zero;
            _rb2D.velocity = new Vector2(0f, _rb2D.velocity.y);
            _physics.SetPhysics2DMaterial();
            return;
        }

        Vector2 moveInput = _controller.moveInput;
        Vector2 targetVelocity = _rb2D.velocity;
        float targetHorizontalSpeed = 0f;
        float targetVerticalSpeed = 0f;

        if (_jump != null && _jump.IsInJumpAscentState && !_physics.IsOnSteepSlope)
        {
            targetHorizontalSpeed = _rb2D.velocity.x;
            if (moveInput.x != 0)
            {
                targetHorizontalSpeed += moveInput.x * (_airMoveAcceleration / _physics.externalForceResistance) * Time.fixedDeltaTime;
                targetHorizontalSpeed = ClampHorizontalSpeed(targetHorizontalSpeed, moveInput.x);
            }
            else
            {
                targetHorizontalSpeed = Mathf.MoveTowards(_rb2D.velocity.x, 0, _airMoveDeceleration * Time.fixedDeltaTime);
            }

            _rb2D.velocity = new Vector2(targetHorizontalSpeed, _rb2D.velocity.y);
            return;
        }

        if (moveInput.x != 0)
        {
            if (_physics.IsGrounded)
            {
                targetHorizontalSpeed = moveInput.x * (_moveAcceleration / _physics.externalForceResistance) * Time.fixedDeltaTime;
            }
            else
            {
                targetHorizontalSpeed = moveInput.x * (_airMoveAcceleration / _physics.externalForceResistance) * Time.fixedDeltaTime;
            }
            targetHorizontalSpeed = ClampHorizontalSpeed(_rb2D.velocity.x + targetHorizontalSpeed, moveInput.x);
            targetVerticalSpeed = _rb2D.velocity.y;
            targetVelocity = new Vector2(targetHorizontalSpeed, targetVerticalSpeed);

            if (_physics.IsGrounded)
            {
                //_hsMovementClampModifer = 0f; // Added By Abdulaziz (Reset clamp after exiting mirror)
                if (!_physics.IsOnSlope && !_physics.IsOnSteepSlope)
                {
                    targetVelocity = new Vector2(targetHorizontalSpeed, _rb2D.velocity.y);
                }
                else if (_physics.IsOnSlope && !_physics.IsOnSteepSlope)
                {
                    Vector2 slopeTangent = GetSlopeTangent(moveInput.x);

                    float accel = (_slopeMoveAcceleration / _physics.externalForceResistance);
                    float desiredSpeed = GetHorizontalMoveSpeedLimit(moveInput.x);
                    Vector2 desiredVelocity = slopeTangent * desiredSpeed;


                    // Move velocity toward desired along-slope velocity
                    targetVelocity = MoveTowardsVelocity(_rb2D.velocity, desiredVelocity, accel);

                    // Safety: keep horizontal clamp (optional, but nice)
                    targetVelocity = ClampHorizontalOnly(targetVelocity, moveInput.x);

                    // This prevents "extra energy" downhill/uphill.
                    if (!_physics.IsMovingUpSlope)
                    {
                        targetVelocity.y = Mathf.Min(targetVelocity.y, 0f);
                    }

                    _physics.SetPhysics2DMaterial();
                    _rb2D.velocity = targetVelocity;
                    return;
                }
                else if (_physics.IsOnSteepSlope)
                {
                    float steepSlopeSlideSpeedMultiplier = _physics.WallsAndRoofsSteepSlopeSlideSpeedMultiplier;

                    if (_physics.IsMovingUpSlope)
                    {
                        targetVelocity.x = 0;
                        targetVelocity.y = Mathf.Abs(_rb2D.velocity.y) * -1;

                        Vector2 slopeDownwardForce = _physics.slopeNormalPerp * -_rb2D.velocity.magnitude;
                        slopeDownwardForce.x = Mathf.Min(slopeDownwardForce.x, -2f);
                        slopeDownwardForce.y = Mathf.Abs((_slopeMoveAcceleration * 1.5f * steepSlopeSlideSpeedMultiplier) * _physics.slopeNormalPerp.y * -1 * _physics.facingDir * Time.fixedDeltaTime) * -1;

                        targetVelocity.y = slopeDownwardForce.y;
                    }
                    else
                    {
                        targetVelocity.x = targetHorizontalSpeed;
                        targetVelocity.y = Mathf.Abs(_rb2D.velocity.y) * -1;

                        Vector2 slopeDownwardForce = _physics.slopeNormalPerp * -_rb2D.velocity.magnitude * steepSlopeSlideSpeedMultiplier;
                        targetVelocity.y = slopeDownwardForce.y;
                    }

                    targetVelocity = PreventMovementAgainstActiveSteepSlopeSlide(targetVelocity);
                }
                else
                {
                    targetVelocity = new Vector2(targetHorizontalSpeed, _rb2D.velocity.y);
                }

                _physics.SetPhysics2DMaterial();
                if (targetVelocity.y > 0 && !_physics.IsMovingUpSlope)
                {
                    targetVelocity.y = 0;
                }
                _rb2D.velocity = targetVelocity;
            }
            else
            {
                _rb2D.velocity = targetVelocity;
            }
        }
        else
        {
            if (_physics.IsGrounded)
            {
                ApplyGroundDeceleration();
            }
            else if (!_physics.IsGrounded && !_physics.HasExternalForce)
            {
                ApplyAirDeceleration();
            }
        }
    }

    private Vector2 PreventMovementAgainstActiveSteepSlopeSlide(Vector2 targetVelocity)
    {
        if (!_physics.IsWallsAndRoofsSteepSlopeSlideInfluenceActive)
        {
            return targetVelocity;
        }

        Vector2 downhillDirection = _physics.slopeNormalPerp.normalized;
        if (downhillDirection.y > 0f)
        {
            downhillDirection = -downhillDirection;
        }

        float targetDownhillSpeed = Mathf.Max(Vector2.Dot(targetVelocity, downhillDirection), 0f);
        float currentDownhillSpeed = Mathf.Max(Vector2.Dot(_rb2D.velocity, downhillDirection), 0f);
        float protectedDownhillSpeed = Mathf.Max(
            targetDownhillSpeed,
            currentDownhillSpeed,
            _physics.CurrentWallsAndRoofsEjectionSpeed
        );

        return downhillDirection * protectedDownhillSpeed;
    }

    private void FloatingMove()
    {
        if (_physics.IsExternalForceControlActive)
        {
            return;
        }

        Vector2 moveInput = _controller.moveInput;
        float acceleration = _floatingMoveAcceleration / _physics.externalForceResistance;
        Vector2 inputDeltaVelocity = moveInput * acceleration * Time.fixedDeltaTime;

        Vector2 playerVelocity = _rb2D.velocity - _floatingExternalVelocity;
        playerVelocity += inputDeltaVelocity;

        if (moveInput == Vector2.zero)
        {
            playerVelocity = Vector2.MoveTowards(
                playerVelocity,
                Vector2.zero,
                _floatingMoveDeceleration * Time.fixedDeltaTime);
        }

        Vector2 targetVelocity = playerVelocity + _floatingExternalVelocity;
        targetVelocity = ClampFloatingSpeed(targetVelocity);
        _rb2D.velocity = targetVelocity;
    }


    private float ClampHorizontalSpeed(float speed, float moveX)
    {
        float moveSpeedLimit = GetHorizontalMoveSpeedLimit(moveX);
        float minSpeed = Mathf.Min(-moveSpeedLimit, -_moveSpeedClampModifier);
        float maxSpeed = Mathf.Max(moveSpeedLimit, _moveSpeedClampModifier);
        return Mathf.Clamp(speed, minSpeed, maxSpeed);
    }

    private float GetHorizontalMoveSpeedLimit(float moveX)
    {
        if (!_controller.IsScriptedControlActive)
        {
            return Mathf.Abs(_moveSpeed);
        }

        return Mathf.Abs(_moveSpeed) * Mathf.Clamp01(Mathf.Abs(moveX));
    }
    private float ClampVerticalSpeed(float speed)
    {
        float minSpeed = Mathf.Min(-_moveSpeed, -_moveSpeedClampModifier);
        float maxSpeed = Mathf.Max(_moveSpeed, _moveSpeedClampModifier);
        return Mathf.Clamp(speed, minSpeed, maxSpeed);
    }

    public void SetMoveSpeedClamp(float value)
    {
        _moveSpeedClampModifier = value;
    }

    public void AddMoveSpeedClamp(float value)
    {
        _moveSpeedClampModifier += value;
    }


    /// <summary>
    /// Applies deceleration to the player's movement when grounded, adjusting horizontal and vertical speeds
    /// based on the player's state (on slope or steep slope). Updates the physics material accordingly.
    /// </summary>
    private void ApplyGroundDeceleration()
    {
        float targetHorizontalSpeed = Mathf.MoveTowards(_rb2D.velocity.x, 0, _moveDeceleration * Time.fixedDeltaTime);
        float targetVerticalSpeed = 0f;
        Vector2 targetVelocity = new(targetHorizontalSpeed, _rb2D.velocity.y);

        if (_controller.moveInput.x == 0 && _physics.IsGrounded)
        {
            targetHorizontalSpeed = Mathf.MoveTowards(_rb2D.velocity.x, 0, _moveDeceleration * Time.fixedDeltaTime);
            targetVerticalSpeed = 0f;

            if (PlayerStatus.IsOnSteepSlope)
            {
                float steepSlopeSlideSpeedMultiplier = _physics.WallsAndRoofsSteepSlopeSlideSpeedMultiplier;
                targetVerticalSpeed = Mathf.Abs((_slopeMoveAcceleration * 1.5f * steepSlopeSlideSpeedMultiplier) * _physics.slopeNormalPerp.y * -1 * _physics.facingDir * Time.fixedDeltaTime) * -1;
                targetHorizontalSpeed = Mathf.MoveTowards(_rb2D.velocity.x, 0, (_slopeMoveDeceleration * 1.5f * steepSlopeSlideSpeedMultiplier) * Time.fixedDeltaTime);

                targetVelocity = new Vector2(targetHorizontalSpeed, Mathf.Abs(targetVerticalSpeed) * -1);

            }
            else if (PlayerStatus.IsOnSlope)
            {
                targetHorizontalSpeed = Mathf.MoveTowards(_rb2D.velocity.x, 0, _slopeMoveDeceleration * Time.fixedDeltaTime);
                targetVerticalSpeed = Mathf.Abs(_slopeMoveAcceleration * _physics.slopeNormalPerp.y * -1 * _physics.facingDir * Time.fixedDeltaTime) * -1;
                targetVelocity = new Vector2(targetHorizontalSpeed, targetVerticalSpeed);
            }
            else
            {
                targetHorizontalSpeed = Mathf.MoveTowards(_rb2D.velocity.x, 0, _slopeMoveAcceleration * Time.fixedDeltaTime);
                targetVelocity = new Vector2(targetHorizontalSpeed, _rb2D.velocity.y);
            }

            _physics.SetPhysics2DMaterial();
            _rb2D.velocity = targetVelocity;
        }
    }

    private void ApplyAirDeceleration()
    {
        if (!_physics.IsGrounded)
        {
            if (_controller.moveInput.x != 0) return;

            float targetHorizontalSpeed = Mathf.MoveTowards(_rb2D.velocity.x, 0, _airMoveDeceleration * Time.fixedDeltaTime);
            float targetVerticalSpeed = _rb2D.velocity.y;

            _rb2D.velocity = new Vector2(targetHorizontalSpeed, targetVerticalSpeed);
        }
    }

    private Vector2 GetSlopeTangent(float moveX)
    {
        Vector2 t = _physics.slopeNormalPerp.normalized;

        // Choose the tangent direction that aligns with the intended world move direction.
        // This is independent of character visual flipping.
        Vector2 desiredDir = new Vector2(Mathf.Sign(moveX), 0f);

        if (Vector2.Dot(t, desiredDir) < 0f)
            t = -t;

        return t;
    }


    private Vector2 MoveTowardsVelocity(Vector2 current, Vector2 target, float accel)
    {
        float maxDelta = accel * Time.fixedDeltaTime;
        return Vector2.MoveTowards(current, target, maxDelta);
    }

    private Vector2 ClampHorizontalOnly(Vector2 v, float moveX)
    {
        v.x = ClampHorizontalSpeed(v.x, moveX);
        return v;
    }

    private Vector2 ClampFloatingSpeed(Vector2 velocity)
    {
        float clampValue = Mathf.Abs(_floatingMoveSpeedClamp) + Mathf.Max(0f, _floatingSpeedClampModifier);
        velocity.x = Mathf.Clamp(velocity.x, -clampValue, clampValue);
        velocity.y = Mathf.Clamp(velocity.y, -clampValue, clampValue);
        return velocity;
    }

    public void SetFloatingExternalVelocity(Vector2 velocity)
    {
        _floatingExternalVelocity = velocity;
    }

    public void AddFloatingSpeedClamp(float value)
    {
        _floatingSpeedClampModifier += value;
    }

    public void ResetFloatingRuntimeState()
    {
        _floatingExternalVelocity = Vector2.zero;
        _floatingSpeedClampModifier = 0f;
    }

    #endregion

    //------------------------------- Reset Methods -------------------------------//

    public void ResetMovement()
    {
        _moveSpeedClampModifier = 0;
    }

}
