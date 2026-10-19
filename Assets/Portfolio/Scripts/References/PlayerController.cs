using GameSystem;
using PlayerSystem;
using System;
using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{

    //------------------------------- Components -------------------------------//

    [SerializeField] private Rigidbody2D _rb2D;
    public Rigidbody2D rb2D
    {
        get => _rb2D;
        set => _rb2D = value;
    }

    [SerializeField] private CapsuleCollider2D _mainCollider2D;
    public CapsuleCollider2D mainCollider2D
    {
        get => _mainCollider2D;
        set => _mainCollider2D = value;
    }

    //------------------------------- Modules -------------------------------//

    [SerializeField] private PlayerMovement _movement;
    public PlayerMovement movement => _movement;


    [SerializeField] private PlayerActions _actions;
    public PlayerActions actions => _actions;


    [SerializeField] private PlayerPhysics _physics;
    public PlayerPhysics physics => _physics;


    [SerializeField] private PlayerJump _jump;
    public PlayerJump jump => _jump;


    private GameInput _gameInput;


    private float _lastMoveInputX = 0f;

    //------------------------------- Scripted Control -------------------------------//

    private bool _scriptedControlActive;
    private Vector2 _scriptedMoveInput;

    public bool IsScriptedControlActive => _scriptedControlActive;


    //------------------------------- Actions -------------------------------//

    public event Action<float> DirectionChanged; // Passes new direction: -1 (left), 1 (right)

    //public static event Action<bool> OnPlayerWalk;
    //public static event Action<bool> OnPlayerJump;

    //------------------------------- Parameters -------------------------------//

    private Vector2 _moveInput;
    public Vector2 moveInput
    {
        get => _moveInput;
        set => _moveInput = value;
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

    //------------------------------- Game Pause -------------------------------//

    public virtual void OnGameStateChanged(GameState newGameState)
    {
        if (newGameState == GameState.Paused)
        {
            _physics.FreezeMovement();
        }
        else
        {
            _physics.UnFreezeMovement();
        }
    }

    //------------------------------- Unity Methods -------------------------------//

    #region Unity

    private void Start()
    {
        InitializeModules();
        InitializeComponents();
        InitializeEvents();
    }

    private void Update()
    {
        UpdateParameters();
        UpdateModules();

    }

    private void FixedUpdate()
    {
        HandlePhysicsInteractions();
    }

    private void LateUpdate()
    {
        //UpdateEvents();
    }

    private void OnDestroy()
    {
        GameplayManager.GameStateChangedEvent -= OnGameStateChanged;
        //GameplayManager.NewSessionProcessingStarted -= ResetPlayer;
    }

    #endregion

    //------------------------------- Initialization -------------------------------//

    #region Initialization

    private void InitializeEvents()
    {
        GameplayManager.GameStateChangedEvent += OnGameStateChanged;
        //GameplayManager.NewSessionProcessingStarted += ResetPlayer;
    }



    private void InitializeComponents()
    {
        if (_rb2D == null) { _rb2D = GetComponent<Rigidbody2D>(); }
        if (_mainCollider2D == null) { _mainCollider2D = GetComponent<CapsuleCollider2D>(); }
    }

    private void InitializeModules()
    {
        if (_movement == null) _movement = GetComponent<PlayerMovement>();
        if (_actions == null) _actions = GetComponent<PlayerActions>();
        if (_physics == null) _physics = GetComponent<PlayerPhysics>();
        if (_jump == null) _jump = GetComponent<PlayerJump>();

        _gameInput = GameInput.Instance;
    }

    #endregion

    //------------------------------- Update Methods -------------------------------//

    #region Update

    private void UpdateModules()
    {
    }

    private void UpdateParameters()
    {
        UpdateMoveInput();
    }

    private void UpdateMoveInput()
    {
        if (_scriptedControlActive)
        {
            _moveInput = _scriptedMoveInput;
            return;
        }

        if (PlayerHandler.IsPlayerDisabled || PlayerStatus.IsBusy) { _moveInput = Vector2.zero; return; }
        else
        {
            if(_gameInput.inputManager != null)
            {
                _moveInput = _gameInput.inputManager.GameControls.MoveDN.ReadValue<Vector2>();
            }
            else
            {
                _moveInput = Vector2.zero;
            }

        }
    }

    private void UpdateEvents()
    {
        AnimationEvents.InvokeWalk(this, PlayerStatus.IsWalking);
        AnimationEvents.InvokeJump(this, PlayerStatus.IsJumping);
        AnimationEvents.InvokeFall(this, PlayerStatus.IsFalling);
        AnimationEvents.InvokeFloatingUp(this, PlayerStatus.IsFloatingUp);
    }

    private void HandlePhysicsInteractions()
    {
        if (GameplayManager.IsSessionProcessing) { return; }

        // Scripted control is the one intentional exception to the normal disabled gate.
        // Do not let it bypass death/reset/pause/other ownership states.
        if (PlayerHandler.IsPlayerDisabled && !_scriptedControlActive) { return; }
        if (_scriptedControlActive && !CanRunScriptedPhysics()) { return; }

        _physics.ApplyPlatformDelta();
        _physics.UpdatePhysics();
        _movement.UpdateMovement();
        _jump.UpdateJump();
        _physics.FinalizePlatformSync();

    }

    #endregion

    //------------------------------- Scripted Control Methods -------------------------------//

    public void BeginScriptedControl()
    {
        _scriptedMoveInput = Vector2.zero;
        _scriptedControlActive = true;
    }

    public void SetScriptedMoveInput(Vector2 input)
    {
        if (!_scriptedControlActive) { return; }
        _scriptedMoveInput = Vector2.ClampMagnitude(input, 1f);
    }

    public void StopScriptedHorizontalMotion()
    {
        if (_rb2D == null) { return; }
        _rb2D.velocity = new Vector2(0f, _rb2D.velocity.y);
    }

    public void EndScriptedControl()
    {
        _scriptedMoveInput = Vector2.zero;
        _moveInput = Vector2.zero;
        _scriptedControlActive = false;
    }

    private bool CanRunScriptedPhysics()
    {
        return PlayerHandler.IsScriptedControlActive
            && !GameplayManager.IsGamePaused
            && !GameplayManager.IsSessionProcessing
            && !PlayerStatus.IsBusy
            && !PlayerStatus.IsDead
            && !PlayerStatus.IsResetting
            && !PlayerStatus.IsDisabled
            && !PlayerStatus.IsRefracted
            && !PlayerStatus.IsHeld;
    }

    //------------------------------- Misc Methods -------------------------------//

    public void EnterNoClip(bool value)
    {
        if (_mainCollider2D != null)
        {
            _mainCollider2D.isTrigger = value;
            if (value == true) { gameObject.tag = "PlayerRefracted"; }
            else { gameObject.tag = "Player"; }
        }
    }

    public void EnterRefraction(bool value)
    {
        EnterNoClip(value);

        if (value == true)
        {
            if (_resetPlayerWithDelayCoroutine != null && _resetPlayerWithDelayCoroutine.MoveNext())
            {
                StopCoroutine(_resetPlayerWithDelayCoroutine);
                _resetPlayerWithDelayCoroutine = null;
            }

            if (transform != null && transform.parent != null)
            {
                transform.parent = null;
            }

            _physics.grounded = false;
            _physics.FreezeMovement();
        }
        else
        {
            if (_rb2D != null)
            {
                //_rb2D.gravityScale = Default_Gravity_Scale;
            }
            else
            {
                Debug.LogError("_rb2D is null! Check initialization.");
            }
        }

        _jump.DisableJump(value);
        if (value)
        {
            PlayerStatus.AddMode(Mode.Refracted);
        }
        else
        {
            PlayerStatus.RemoveMode(Mode.Refracted);
        }
    }

    public void MoveToCheckPoint(Vector3 checkpoint)
    {
        StartCoroutine(MoveAfterFrame(checkpoint));
    }

    private IEnumerator MoveAfterFrame(Vector3 checkpoint)
    {
        yield return new WaitForFixedUpdate();

        _rb2D.position = checkpoint;
        Physics2D.SyncTransforms();
        physics.ResetPhysics();
    }

    //------------------------------- Reset Methods -------------------------------//

    public void ResetPlayer()
    {
        if (_resetPlayerWithDelayCoroutine != null)
        {
            StopCoroutine(_resetPlayerWithDelayCoroutine);
            _resetPlayerWithDelayCoroutine = null;
        }

        _moveInput = Vector2.zero;
        _lastMoveInputX = 0f;
        _physics.ResetPhysics();
        _movement.ResetMovement();
        _jump.ResetJump();
    }

    private IEnumerator _resetPlayerWithDelayCoroutine = null;
    public bool IsResetPlayerWithDelayActive => _resetPlayerWithDelayCoroutine != null;

    public void ResetPlayerWithDelay()
    {
        if (_resetPlayerWithDelayCoroutine != null)
        {
            StopCoroutine(_resetPlayerWithDelayCoroutine);
        }
        _resetPlayerWithDelayCoroutine = ResetPlayerWithDelayCoroutine();
        StartCoroutine(_resetPlayerWithDelayCoroutine);
    }

    private IEnumerator ResetPlayerWithDelayCoroutine()
    {
        while (!_physics.IsGrounded)
        {
            yield return null;
        }
        ResetPlayer();
        _resetPlayerWithDelayCoroutine = null;
    }


}