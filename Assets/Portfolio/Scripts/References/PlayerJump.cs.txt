using GameSystem;
using PlayerSystem;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerJump : MonoBehaviour
{
    public event Action OnJumpTriggered;

    public enum ScriptedJumpResult
    {
        None,
        InputDisabled,
        PlayerBusy,
        AlreadyJumped,
        JumpDisabled,
        SteepSlopeRestricted,
        NotGrounded,
        Started
    }

    //------------------------------- Constents -------------------------------//

    public const int DEFAULT_GROUND_CHECK_DELAY = 12;   // 12 FixedFrames
    public const int STAIRS_GROUND_CHECK_DELAY = 4;     // 4 FixedFrames


    //------------------------------- Components -------------------------------//

    private Rigidbody2D _rb2D;

    //------------------------------- Modules -------------------------------//

    private PlayerController _controller;
    private PlayerPhysics _physics;

    private GameInput _gameInput;

    //------------------------------- Jump Parameters -------------------------------//

    [Header("JUMP PARAMETERS")]

    [SerializeField] private float _jumpForce = 10f;
    [SerializeField] private float _maxJumpForce = 10f;
    public float maxJumpForce { get { return _maxJumpForce; } }

    [SerializeField] private float _jumpForceClampModifier;
    public float jumpForceClampModifier => _jumpForceClampModifier;

    [SerializeField] private float _jumpBufferDuration = 0.1f; // Time window for buffering jumps
    private float _jumpBufferRemainingTime; // Tracks remaining buffer time

    [SerializeField] private int _groundedCheckDelay = 4;
    public int groundedCheckDelay
    {
        get => _groundedCheckDelay;
        set => _groundedCheckDelay = value;
    }

    /// <summary>
    /// Determines if a buffered jump is available.
    /// </summary>
    public bool HasBufferedJump => _jumpBufferRemainingTime > 0f;
    public bool m_HasBufferedJump;

    private bool _jumpStarted;
    private bool _hasJumped; // Prevents multiple jumps mid-air
    public bool hasJumped => _hasJumped; // Prevents multiple jumps mid-air

    public bool IsJumpStarted => _jumpStarted;
    public bool IsInJumpAscentState { get; private set; }

    private bool _isJumpDisabled;
    public bool IsJumpDisabled => _isJumpDisabled;

    private bool _isSteepSlopeJumpRestricted;
    public bool IsSteepSlopeJumpRestricted => _isSteepSlopeJumpRestricted;


    //------------------------------- Coyote Time Parameters -------------------------------//

    [Header("COYOTE TIME PARAMETERS")]

    [SerializeField] private float _coyoteTimeThreshold = 0.15f; // How long coyote time lasts after leaving ground
    private float _coyoteTimer; // Tracks remaining coyote time

    /// <summary>
    /// Determines if the player can still jump during coyote time.
    /// </summary>
    public bool CanUseCoyoteTime => _coyoteTimer > 0f;

    //------------------------------- Aniticipation Frame Parameters -------------------------------//

    [Header("ANTICIPATION FRAME PARAMETERS")]

    [SerializeField] private float _jumpAnticipationDuration = 0.1f; // Duration in seconds

    private Coroutine _jumpAnticipationCoroutine;
    public bool IsInAnticipationFrame => _jumpAnticipationCoroutine != null;
    public bool HasActiveRuntimeCoroutine => _jumpAnticipationCoroutine != null;
    //------------------------------- Flags -------------------------------//

    bool _isInitialized = false;
    private bool _eventsInitialized;

    //------------------------------- Scripted Jump Debug -------------------------------//

    public int ScriptedJumpTriggerCount { get; private set; }
    public int ScriptedJumpTryCount { get; private set; }
    public int ScriptedJumpSuccessCount { get; private set; }
    public ScriptedJumpResult LastScriptedJumpResult { get; private set; }


    //------------------------------- Unity Methods -------------------------------//

    #region Unity

    private void Start()
    {
        Initialize();
    }

    private void OnDisable()
    {
        UninitializeEvents();
        PlayerHandler.OnPlayerDisabledStateChanged -= OnPlayerDisabledStateChanged;
    }

    private void OnDestroy()
    {
        UninitializeEvents();
        PlayerHandler.OnPlayerDisabledStateChanged -= OnPlayerDisabledStateChanged;
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

        _gameInput = GameInput.Instance;
    }

    private void InitializeEvents()
    {
        if (_eventsInitialized) { return; }
        if (_gameInput == null) { return; }

        _gameInput.jumpEvent += Jump;
        PlayerHandler.OnPlayerDisabledStateChanged -= OnPlayerDisabledStateChanged;
        PlayerHandler.OnPlayerDisabledStateChanged += OnPlayerDisabledStateChanged;
        _eventsInitialized = true;
    }

    private void UninitializeEvents()
    {
        if (!_eventsInitialized) { return; }
        if (_gameInput == null) { _eventsInitialized = false; return; }

        _gameInput.jumpEvent -= Jump;
        PlayerHandler.OnPlayerDisabledStateChanged -= OnPlayerDisabledStateChanged;
        _eventsInitialized = false;
    }

    #endregion

    //------------------------------- Update Methods -------------------------------//

    #region Update

    public void UpdateJump()
    {
        if (!_isInitialized) { return; }
        m_HasBufferedJump = HasBufferedJump; // Update buffered jump state

        if (IsInJumpAscentState && _jumpAnticipationCoroutine == null && _rb2D.velocity.y <= 0f)
        {
            IsInJumpAscentState = false;
        }

        if (!_jumpStarted)
        {
            _jumpBufferRemainingTime -= Time.fixedDeltaTime; // Reduce jump buffer time
        }

        if (_physics.IsGrounded && _jumpAnticipationCoroutine == null && !IsInJumpAscentState)
        {
            _coyoteTimer = _coyoteTimeThreshold; // Reset coyote time when grounded
            //if (_hasJumped == true) AudioManagerOLD.PlayLand();
            _hasJumped = false; // Reset jump ability when grounded
            if (HasBufferedJump)
            {
                Jump();
                _jumpBufferRemainingTime = 0f;
            }
        }
        else
        {
            _coyoteTimer -= Time.fixedDeltaTime; // Decrease coyote time when airborne
        }
    }

    #endregion

    //------------------------------- Jump Methods -------------------------------//

    #region Jump

    private bool IsPlayerInputDisabled => PlayerHandler.IsPlayerDisabled;

    private void OnPlayerDisabledStateChanged(bool isDisabled)
    {
        if (!isDisabled) { return; }
        ResetJumpBuffer();

        if (_jumpAnticipationCoroutine != null)
        {
            StopCoroutine(_jumpAnticipationCoroutine);
            _jumpAnticipationCoroutine = null;
        }

        _jumpStarted = false;
        IsInJumpAscentState = false;
    }

    private void Jump(InputAction.CallbackContext context)
    {
        if (IsPlayerInputDisabled)
        {
            _jumpStarted = false;
            return;
        }

        if (PlayerStatus.IsBusy) { return; }

        _jumpStarted = context.started;

        if (_jumpStarted)
        {
            if (_hasJumped && !_isJumpDisabled && !_isSteepSlopeJumpRestricted && !_physics.IsGrounded)
            {
                _jumpBufferRemainingTime = _jumpBufferDuration;
            }

            _jumpStarted = false;
        }

        TryJump();
    }

    public bool TriggerScriptedJump()
    {
        ScriptedJumpTriggerCount++;
        return TryJump();
    }

    private bool TryJump()
    {
        ScriptedJumpTryCount++;

        if (_hasJumped)
        {
            LastScriptedJumpResult = ScriptedJumpResult.AlreadyJumped;
            return false;
        }

        if (_isJumpDisabled)
        {
            LastScriptedJumpResult = ScriptedJumpResult.JumpDisabled;
            return false;
        }

        if (_isSteepSlopeJumpRestricted)
        {
            LastScriptedJumpResult = ScriptedJumpResult.SteepSlopeRestricted;
            return false;
        }

        if (_physics.IsGrounded || CanUseCoyoteTime || HasBufferedJump)
        {
            _hasJumped = true;
            ScriptedJumpSuccessCount++;
            LastScriptedJumpResult = ScriptedJumpResult.Started;
            OnJumpTriggered?.Invoke();
            IsInJumpAscentState = true;
            _jumpBufferRemainingTime = 0f;

            if (_jumpAnticipationCoroutine != null)
            {
                StopCoroutine(_jumpAnticipationCoroutine);
            }

            _jumpAnticipationCoroutine = StartCoroutine(JumpAnticipationCoroutine());
            return true;
        }

        LastScriptedJumpResult = ScriptedJumpResult.NotGrounded;
        return false;
    }

    private void Jump()
    {
        if (IsPlayerInputDisabled) { return; }
        if (PlayerStatus.IsBusy) { return; }
        if (_hasJumped || _isJumpDisabled || _isSteepSlopeJumpRestricted) return;
        if (_physics.IsGrounded || CanUseCoyoteTime || HasBufferedJump)
        {
            _hasJumped = true;
            OnJumpTriggered?.Invoke();
            IsInJumpAscentState = true;
            _jumpBufferRemainingTime = 0f;
            if (_jumpAnticipationCoroutine != null)
                StopCoroutine(_jumpAnticipationCoroutine);
            _jumpAnticipationCoroutine = StartCoroutine(JumpAnticipationCoroutine());

            //_physics.TemporarilyDisableGroundCheck(4);
            //AudioManager.PlayJump();

            //float targetVerticalSpeed = _jumpForce;

            ////_rb2D.velocity = new Vector2(_rb2D.velocity.x, 0f);
            //_rb2D.velocity = new Vector2(_rb2D.velocity.x, Mathf.Max(_rb2D.velocity.y, 0) + targetVerticalSpeed);
        }
    }

    private IEnumerator JumpAnticipationCoroutine()
    {
        float elapsedTime = 0f;
        float wait = _jumpAnticipationDuration;
        while (elapsedTime < wait)
        {
            elapsedTime += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        //yield return new WaitForSeconds(_jumpAnticipationDuration);

        yield return new WaitUntil(() => !GameplayManager.IsGamePaused);

        _physics.TemporarilyDisableGroundCheck(_groundedCheckDelay);
        float targetVerticalSpeed = _jumpForce;
        float baseY = 0f;

        // Ignore any upward velocity caused by slope movement or physics noise
        if (!_physics.IsGrounded)
        {
            baseY = Mathf.Max(_rb2D.velocity.y, 0f);
        }

        // Apply jump but NEVER exceed max jump force
        float newY = Mathf.Min(baseY + _jumpForce, _maxJumpForce);

        _rb2D.velocity = new Vector2(_rb2D.velocity.x, newY);
        _jumpAnticipationCoroutine = null;

    }

    public void SetJumpForceClamp(float value)
    {
        _jumpForceClampModifier = value;
    }

    public void AddJumpForceClamp(float value)
    {
        _jumpForceClampModifier += value;
    }

    public void DisableJump(bool value)
    {
        _isJumpDisabled = value;
    }

    public void SetHasJumped(bool value)
    {
        _hasJumped = value;
        if (!value)
        {
            IsInJumpAscentState = false;
        }
    }

    public void SetSteepSlopeJumpRestriction(bool value)
    {
        _isSteepSlopeJumpRestricted = value;
    }

    #endregion

    //------------------------------- Reset Methods -------------------------------//

    public void ResetJumpBuffer()
    {
        _jumpBufferRemainingTime = 0f; // Reset the jump buffer
    }

    public void ResetJump()
    {
        if (_jumpAnticipationCoroutine != null)
        {
            StopCoroutine(_jumpAnticipationCoroutine);
            _jumpAnticipationCoroutine = null;
        }

        _jumpStarted = false;
        _hasJumped = false;
        _jumpForceClampModifier = 0;
        _jumpBufferRemainingTime = 0f;
        _coyoteTimer = 0f;
        _isJumpDisabled = false;
        _isSteepSlopeJumpRestricted = false;
        IsInJumpAscentState = false;
    }
}
