using GameSystem;
using System;
using System.Collections;

using UnityEngine;
using Watcher.Visuals;

namespace PlayerSystem
{
    public class PlayerHandler : MonoBehaviour
    {
        [HideInInspector] public static PlayerHandler instance;
        [SerializeField] VoidEventChannelSO playerDeathChannel;
        public static bool IsPlayerDisabled { get; private set; }
        public static bool IsScriptedControlActive { get; private set; }
        public static event Action<bool> OnPlayerDisabledStateChanged;
        public static int DisabledStateChangedEventCount { get; private set; }
        public static float LastDisabledStateChangedRealtime { get; private set; } = -1f;
        public bool IsRestoringBaseline => _isRestoringBaseline;
        public int BaselineRestoreCount => _baselineRestoreCount;
        public float LastBaselineRestoreRealtime => _lastBaselineRestoreRealtime;
        public int ResetInvocationCount => _resetInvocationCount;
        public string ResetPipelineStage => _resetPipelineStage;
        public string LastResetPipelineLog => _lastResetPipelineLog;
        public string DisabledStateContributors => GetDisabledStateContributors();

        private bool _isRestoringBaseline;
        private int _baselineRestoreCount;
        private float _lastBaselineRestoreRealtime = -1f;
        private int _resetInvocationCount;
        private string _resetPipelineStage = "Idle";
        private string _lastResetPipelineLog = "Never";

        [SerializeField] private MirrorObjectBase _currentRefractingMirrorObject;
        public MirrorObjectBase currentRefractingMirrorObject
        {
            get => _currentRefractingMirrorObject;
            set => _currentRefractingMirrorObject = value;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticStateOnSubsystemRegistration()
        {
            instance = null;
            IsPlayerDisabled = false;
            IsScriptedControlActive = false;
            OnPlayerDisabledStateChanged = null;
            DisabledStateChangedEventCount = 0;
            LastDisabledStateChangedRealtime = -1f;
        }

        //------------------------------- Game Paused -------------------------------//

        public virtual void OnGameStateChanged(GameState newGameState)
        {
            AnimationManager.FreezeAllAnimaitons(_animator, newGameState == GameState.Paused);
            RefreshPlayerDisabledState();
        }

        //------------------------------- Unity Methods -------------------------------//

        #region Singleton
        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            Initialize();
        }
        #endregion

        private void OnEnable()
        {
            playerDeathChannel.OnEventRaised += ChangeState_Death;
        }
        private void OnDisable()
        {
            playerDeathChannel.OnEventRaised -= ChangeState_Death;

        }

        private void OnDestroy()
        {
            ResetEvents();
        }

        //------------------------------- Initialization Methods -------------------------------//

        #region GameSystem Components

        private GameManager _gameManager;

        private GameInput _gameInput;

        #endregion

        #region PlayerSystem Components

        [SerializeField] private PlayerPhysics _playerPhysics;
        public PlayerPhysics playerPhysics => _playerPhysics;

        //[SerializeField] private PlayerMirror _playerMirror;
        //public PlayerMirror playerMirror => _playerMirror;

        [SerializeField] private PlayerMirrorAbility _playerMirrorAbility;
        public PlayerMirrorAbility playerMirrorAbility => _playerMirrorAbility;

        [SerializeField] private PlayerTargeting _playerTargeting;
        public PlayerTargeting playerTargeting => _playerTargeting;

        [SerializeField] private PlayerBulletTime _playerBulletTime;
        public PlayerBulletTime playerBulletTime => _playerBulletTime;

        [SerializeField] private PlayerController _playerController;
        public PlayerController playerController => _playerController;

        [SerializeField] private PlayerIdle _playerIdle;
        public PlayerIdle playerIdle => _playerIdle;

        [SerializeField] private PlayerVisualHighlight _playerVisualHighlight;
        public PlayerVisualHighlight playerVisualHighlight => _playerVisualHighlight;

        [SerializeField] private PlayerScriptedControl _playerScriptedControl;
        public PlayerScriptedControl PlayerScriptedControl => _playerScriptedControl;

        #endregion

        #region Misc Components

        // Camera
        [SerializeField] private Camera _mainCamera;
        public Camera mainCamera => _mainCamera;


        // Mesh Renderers
        [SerializeField] private MeshRenderer _meshRenderer;
        public MeshRenderer meshRenderer
        {
            get => _meshRenderer;
            set => _meshRenderer = value;
        }


        // Animator
        [SerializeField] private Animator _animator;
        public Animator animator => _animator;

        [SerializeField] private Animator _cutsceneAnimator;
        public Animator cutsceneAnimator => _cutsceneAnimator;

        // Transform Points
        [SerializeField] private Transform _originPoint;
        public Transform originPoint
        {
            get => _originPoint;
            set => _originPoint = value;
        }

        private Vector3 _checkpoint;
        public Vector3 checkPoint
        {
            get => _checkpoint;
            set => _checkpoint = value;
        }

        #endregion

        #region VFX Components

        [SerializeField] private ReflectionTrailVFXController _reflectionTrailVFXController;
        public ReflectionTrailVFXController reflectionTrailVFXController => _reflectionTrailVFXController;

        #endregion
        private void Initialize()
        {
            InitializeEvents();
            InitializeComponents();
            RefreshPlayerInputState();
        }

        private void InitializeComponents()
        {
            _gameInput = GameInput.Instance;
            if (_gameInput != null)
            {
                _gameInput.playerActionGate = CanProcessPlayerGameplayInput;
            }
            else
            {
                StartCoroutine(WaitForGameInput());
            }

            _playerPhysics = GetComponent<PlayerPhysics>();
            _playerMirrorAbility = GetComponent<PlayerMirrorAbility>();
            _playerTargeting = GetComponent<PlayerTargeting>();
            _playerBulletTime = GetComponent<PlayerBulletTime>();

            // new components
            _playerController = GetComponent<PlayerController>();
            _playerIdle = GetComponent<PlayerIdle>();
            _playerVisualHighlight = GetComponentInChildren<PlayerVisualHighlight>();

            //_animator = GetComponent<Animator>();
            _checkpoint = transform.position;

            if (CameraManager.MainCamera != null)
            {
                _mainCamera = CameraManager.MainCamera;
            }
            else
            {
                StartCoroutine(WaitForMainCamera());
            }

        }

        private IEnumerator WaitForMainCamera()
        {
            while (CameraManager.MainCamera == null)
            {
                yield return null;
            }
            _mainCamera = CameraManager.MainCamera;
            Debug.Log("[PlayerHandler] MainCamera found and assigned via CameraManager.");
        }

        private IEnumerator WaitForGameInput()
        {
            while (GameInput.Instance == null)
            {
                yield return null;
            }

            _gameInput = GameInput.Instance;
            _gameInput.playerActionGate = CanProcessPlayerGameplayInput;
            RefreshPlayerInputState();
        }

        private void InitializeEvents()
        {
            // TODO: These need to be unsubscribed in OnDisable/Destroy
            PlayerStatus.OnBusyStateChanged += UpdateBusyState;
            PlayerStatus.OnStatusChanged += HandleStatusChanged;
            PlayerStatus.OnModeChanged += HandleModeChanged;
            PlayerStatus.OnModeRemoved += HandleModeRemoved;

            GameplayManager.GameStateChangedEvent += OnGameStateChanged;

            GameplayManager.NewSessionProcessingStarted += HandleNewSessionProcessingStarted;
            GameplayManager.NewSessionProcessingCompleted += HandleNewSessionProcessingCompleted;

            RefreshPlayerDisabledState();
        }

        //------------------------------- Game Input -------------------------------//

        private bool CanProcessPlayerGameplayInput()
        {
            return !GameplayManager.IsSessionProcessing && !IsPlayerDisabled;
        }

        public void SetScriptedControlActive(bool value)
        {
            if (IsScriptedControlActive == value) { return; }

            IsScriptedControlActive = value;
            RefreshPlayerDisabledState();
            RefreshPlayerInputState();
        }

        private void HandleNewSessionProcessingStarted()
        {
            RefreshPlayerInputState();
        }


        private void HandleNewSessionProcessingCompleted()
        {
            RefreshPlayerInputState();
        }

        private void RefreshPlayerInputState()
        {
            // TODO: This possibly the source of the input bug, evaluate it lateron

            if (_gameInput == null)
            {
                Debug.LogWarning("Game input is null!");
                return;
            }

            bool shouldEnableInput = !GameplayManager.IsSessionProcessing;

            if (shouldEnableInput)
            {
                _gameInput.EnableGameControls();
                Debug.Log("[PlayerHandler] Game input: " + _gameInput.m_gameControls);
                return;
            }

            _gameInput.DisableControls();
            Debug.Log("[PlayerHandler] Game input: " + _gameInput.m_gameControls);
        }

        //------------------------------- Player Status -------------------------------//

        private void UpdateBusyState(bool isBusy)
        {
            // Handle busy state changes
            RefreshPlayerDisabledState();
        }

        private void HandleStatusChanged(Status newStatus)
        {
            RefreshPlayerDisabledState();
            switch (newStatus)
            {
                case Status.Normal:
                    SetNormalStatus();
                    break;
                case Status.Resetting:
                    SetResettingStatus();
                    break;
                case Status.Dead:
                    SetDeadStatus();
                    break;
                case Status.Respawning:
                    SetRespawningStatus();
                    break;
                case Status.Disabled:
                    SetPlayerDisabledStatus();
                    break;
            }
        }

        private void HandleModeChanged(Mode newMode)
        {
            RefreshPlayerDisabledState();
            if (PlayerStatus.IsResetting)
            {
                return;
            }

            if ((newMode & Mode.Held) != Mode.Held)
            {
                _gameInput.BindPlayerMirrorItemExitAction(false);
            }

            if ((newMode & Mode.Refracted) == Mode.Refracted)
            {
                SetRefractedMode();
            }

            if ((newMode & Mode.Held) == Mode.Held)
            {
                Debug.Log("Setting Held Mode");
                SetHeldMode();
                if ((newMode & Mode.Melted) == Mode.Melted)
                {
                    SetBusy(false);
                }
            }




            if (newMode == Mode.Normal)
            {
                SetNormalStatus();
            }

            if (newMode == Mode.Cutscene)
            {
                SetBusy(true);
            }

            if (newMode == Mode.Dialogue)
            {
                SetBusy(true);
            }

            if (newMode == Mode.None)
            {
                _playerController.EnterRefraction(false);
                _playerController.ResetPlayer();
            }

            if (PlayerStatus.HasMode(Mode.NoGravity) || PlayerStatus.HasMode(Mode.FloatingArea))
            {
                _playerController.rb2D.gravityScale = 0f;
            }
        }

        private void HandleModeRemoved(Mode removedMode, Mode currentMode)
        {
            if ((removedMode & Mode.Refracted) == Mode.Refracted)
            {
                //_playerController.EnterRefraction(false);
                //SetInvisible(false);
                //_playerPhysics.DisableCollider(false);
                if (_currentRefractingMirrorObject != null)
                {
                    Debug.Log(_currentRefractingMirrorObject.ToString());
                }
                _currentRefractingMirrorObject = null;
                Debug.Log(_currentRefractingMirrorObject);
            }

            if (removedMode == Mode.Cutscene)
            {
                SetCutsceneMode(false);

            }

            if ((removedMode & Mode.FloatingArea) == Mode.FloatingArea)
            {
                _playerController?.movement?.ResetFloatingRuntimeState();

                if ((currentMode & (Mode.FloatingArea | Mode.NoGravity)) == Mode.None)
                {
                    _playerPhysics?.RestoreDefaultGravityScale();
                }
            }

            if (ShouldReleaseBusyAfterModeRemoved(currentMode))
            {
                SetBusy(false);
            }

            if (!IsPlayerDisabled && !PlayerStatus.IsCutscene && !PlayerStatus.IsRefracted)
            {
                _playerPhysics.ForceUnFreezeMovement();
                EnablePlayer();
            }
        }

        private static bool ShouldReleaseBusyAfterModeRemoved(Mode currentMode)
        {
            Mode busyModes = Mode.Refracted | Mode.Held | Mode.Cutscene | Mode.Dialogue;
            return (currentMode & busyModes) == Mode.None
                && !PlayerStatus.IsDead
                && !PlayerStatus.IsDisabled
                && !PlayerStatus.IsResetting;
        }


        private void SetNormalStatus()
        {
            SetPlayerDisabledStatus(false);
            _playerController.EnterRefraction(false);
            _playerController.ResetPlayer();
        }

        private void SetResettingStatus()
        {
            RefreshPlayerDisabledState();
        }

        private void SetDeadStatus()
        {
            //PlayerStatus.SetStatus(Status.Disabled);
            //SetResettingStatus();

            //PlayerStatus.SetBusy(true);
            //GameplayManager.DeathScreen();

            //_playerPhysics.ConstrainToVerticalOnly();
            //_playerPhysics.DisableCollider(true);
            //_playerMirrorAbility.ResetToDefaults();

            //int idle = Animator.StringToHash("Idle");
            //AnimationManager.ResetToIdle(_animator, idle);
        }
        private void SetRespawningStatus()
        {
            // TODO: Evaluate this
            MoveToCheckPoint();
            _playerPhysics.DisableCollider(false);
        }

        private void ChangeState_Death()
        {
            EnterDeathSequence();
            GameplayManager.TriggerPlayerDeath();
        }

        public void EnterDeathSequence()
        {
            PlayerStatus.SetBusy(true);
            _playerPhysics?.DisablePlayerControlPhysics();
            _playerPhysics?.DisableCollider(true);
        }
        private void SetRefractedMode()
        {
            PlayerStatus.SetBusy(true);

            _playerController.EnterRefraction(true);
        }

        private void SetHeldMode()
        {
            if (!PlayerStatus.IsMelted)
            {
                PlayerStatus.SetBusy(true);
            }
            _gameInput.BindPlayerMirrorItemExitAction(true);
        }

        public void SetBusy(bool value)
        {
            if (value == false && PlayerStatus.HasMode(Mode.Cutscene)) { return; }
            PlayerStatus.SetBusy(value);
        }

        public void SetCutsceneMode(bool value)
        {
            if (_cutsceneAnimator != null) { _cutsceneAnimator.enabled = value; }
            if (!value)
            {
                PlayerStatus.ForceSetStatus(Status.Normal);
            }
        }


        private static bool EvaluatePlayerDisabledState()
        {
            return GameplayManager.IsGamePaused
                || GameplayManager.IsSessionProcessing
                || PlayerStatus.IsDead
                || PlayerStatus.IsResetting
                || IsScriptedControlActive
                || (PlayerStatus.IsBusy && !PlayerStatus.IsDialogue && !PlayerStatus.IsHeld && !PlayerStatus.IsMelted)
                || PlayerStatus.IsCutscene;
        }

        private static string GetDisabledStateContributors()
        {
            return $"Paused={GameplayManager.IsGamePaused}, " +
                $"SessionProcessing={GameplayManager.IsSessionProcessing}, " +
                $"Dead={PlayerStatus.IsDead}, " +
                $"Resetting={PlayerStatus.IsResetting}, " +
                $"ScriptedControl={IsScriptedControlActive}, " +
                $"Busy={PlayerStatus.IsBusy}, " +
                $"BusyButNotDialogue={(PlayerStatus.IsBusy && !PlayerStatus.IsDialogue)}, " +
                $"BusyButNotDialogueOrHeld={(PlayerStatus.IsBusy && !PlayerStatus.IsDialogue && !PlayerStatus.IsHeld && !PlayerStatus.IsMelted)}, " +
                $"Cutscene={PlayerStatus.IsCutscene}, " +
                $"Dialogue={PlayerStatus.IsDialogue}";
        }

        //------------------------------- Baseline Restore -------------------------------//

        public void RestorePlayerBaseline()
        {
            RestorePlayerBaseline(null);
        }

        public void RestorePlayerBaseline(Vector3? restorePosition)
        {
            BeginPlayerBaselineRestore();

            if (restorePosition.HasValue)
            {
                Teleport(restorePosition.Value);
            }

            CompletePlayerBaselineRestore();
        }

        public void BeginPlayerBaselineRestore()
        {
            _isRestoringBaseline = true;
            _baselineRestoreCount++;
            _resetInvocationCount++;
            _lastBaselineRestoreRealtime = Time.realtimeSinceStartup;
            RecordResetPipelineStage("Entering Resetting");

            PlayerStatus.ForceSetStatus(Status.Resetting);
            RecordResetPipelineStage("Stopping runtime ownership");
            StopRuntimeOwnership();
            RecordResetPipelineStage("Resetting PlayerStatus runtime state");
            PlayerStatus.ResetRuntimeState();
            RecordResetPipelineStage("Resetting runtime modules");
            ResetRuntimeModules();
            RecordResetPipelineStage("Runtime cleanup complete");
        }

        public void CompletePlayerBaselineRestore()
        {
            RecordResetPipelineStage("Restoring animation state");
            ResetAnimationState();
            RecordResetPipelineStage("Restoring gameplay status");
            PlayerStatus.ForceSetStatus(Status.Normal);
            _isRestoringBaseline = false;
            transform.right = Vector3.right;
            //RefreshPlayerDisabledState();
            RecordResetPipelineStage("Restore complete");
        }

        private void StopRuntimeOwnership()
        {
            ResettablesManager.CancelAllRuntimeStates();

            _playerIdle?.StopAllCoroutines();
            _playerController?.StopAllCoroutines();
            _playerPhysics?.StopAllCoroutines();
            _playerMirrorAbility?.StopAllCoroutines();
        }

        private void ResetRuntimeModules()
        {
            if (transform.parent != null)
            {
                transform.parent = null;
            }

            _gameInput?.BindPlayerMirrorItemExitAction(false);
            SetInvisible(false);

            _playerPhysics?.DisableCollider(false);

            _playerController?.EnterRefraction(false);
            _playerController?.ResetPlayer();
            _playerPhysics?.RestorePlayerControlPhysics();
            _playerPhysics?.ResetPhysics();
            _playerPhysics?.ZeroVelocity();
            _playerController?.movement?.ResetMovement();
            _playerController?.jump?.DisableJump(false);

            _playerBulletTime?.ResetBulletTime();
            _playerMirrorAbility?.ResetToDefaults();
            _playerTargeting?.ResetLeftStickPosition();
            _playerTargeting?.ResetRightStickPosition();
        }

        private void RecordResetPipelineStage(string stage)
        {
            _resetPipelineStage = stage;
            _lastResetPipelineLog = $"#{_resetInvocationCount} {Time.realtimeSinceStartup:F2}s {stage} | status={PlayerStatus.CurrentStatus} mode={PlayerStatus.CurrentMode} state={PlayerStatus.CurrentState} busy={PlayerStatus.IsBusy} disabled={IsPlayerDisabled}";
            Debug.Log($"[PlayerHandler] Reset pipeline {_lastResetPipelineLog}", this);
        }

        private void ResetAnimationState()
        {
            if (_animator == null)
            {
                return;
            }

            _animator.Rebind();
            _animator.Update(0f);
            AnimationManager.ResetToIdle(_animator, AnimationManager.IdleHash);
        }

        //------------------------------- Utility Methods -------------------------------//

        public void Teleport(Vector3 worldPos)
        {
            transform.position = worldPos;
            //playerController.rb2D.position = worldPos;
            //Physics2D.SyncTransforms();
            CameraManager.Instance.TeleportCamera(worldPos);
        }

        public void SetCheckPoint(Transform transform)
        {
            _checkpoint = transform.position;
        }

        public void SetCheckPoint(Vector3 checkpoint)
        {
            _checkpoint = checkpoint;
        }

        public void MoveToCheckPoint()
        {
            GameplayManager.RestartCheckpoint();
            // Old
            //_playerController.MoveToCheckPoint(_checkpoint);
        }


        public void SetInvisible(bool value)
        {
            Debug.Log($"Is invisible: {value}");

            _meshRenderer.enabled = !value;

            _playerMirrorAbility.mirrorItem.ShowMirrorItem(!value);
            if (value)
            {
                _playerVisualHighlight.Hide();
            }
            else
            {
                _playerVisualHighlight.Show();
            }
        }

        private void SetPlayerDisabledStatus()
        {
            SetBusy(true);
            IsPlayerDisabled = true;
            DisablePlayer();
        }

        private void SetPlayerDisabledStatus(bool isDisabled)
        {
            IsPlayerDisabled = isDisabled;
            if (isDisabled)
            {
                SetBusy(true);
                DisablePlayer();
                return;
            }
            SetBusy(false);
            EnablePlayer();
        }


        private void RefreshPlayerDisabledState()
        {
            bool nextDisabledState = EvaluatePlayerDisabledState();
            if (IsPlayerDisabled == nextDisabledState) { return; }

            Debug.Log($"[PlayerHandler] Disabled state refresh {IsPlayerDisabled} -> {nextDisabledState}. Contributors: {GetDisabledStateContributors()}", this);
            IsPlayerDisabled = nextDisabledState;
            DisabledStateChangedEventCount++;
            LastDisabledStateChangedRealtime = Time.realtimeSinceStartup;
            //RefreshPlayerInputState();
            OnPlayerDisabledStateChanged?.Invoke(IsPlayerDisabled);
        }

        public void ForceRefreshPlayerDisabledState()
        {
            RefreshPlayerDisabledState();
        }

        public int GetPlayerDisabledStateSubscriberCount()
        {
            return OnPlayerDisabledStateChanged?.GetInvocationList()?.Length ?? 0;
        }

        private void DisablePlayer()
        {
            DisablePhysicsModule();
            DisableJumpModule();
            DisableAbilityModules();
            DisableTargetingModule();

            PlayerStatus.ResetToIdleState();
            AnimationManager.ResetToIdle(_animator, AnimationManager.IdleHash);
        }

        private void DisablePhysicsModule()
        {
            _playerPhysics?.DisablePlayerControlPhysics();
        }

        private void DisableJumpModule()
        {
            _playerController?.jump?.DisableJump(true);
        }

        private void DisableAbilityModules()
        {
            _playerBulletTime?.ResetBulletTime();
            _playerMirrorAbility?.ResetToDefaults();
        }

        private void DisableTargetingModule()
        {
            _playerTargeting?.ResetLeftStickPosition();
            _playerTargeting?.ResetRightStickPosition();
        }

        private void EnablePlayer()
        {
            EnablePhysicsModule();
            EnableJumpModule();
        }

        private void EnablePhysicsModule()
        {
            _playerPhysics?.RestorePlayerControlPhysics();
        }

        private void EnableJumpModule()
        {
            _playerController?.jump?.DisableJump(false);
        }

        //------------------------------- Reset Methods -------------------------------//

        private void ResetEvents()
        {
            PlayerStatus.OnBusyStateChanged -= UpdateBusyState;
            PlayerStatus.OnStatusChanged -= HandleStatusChanged;
            PlayerStatus.OnModeChanged -= HandleModeChanged;
            PlayerStatus.OnModeRemoved -= HandleModeRemoved;

            GameplayManager.GameStateChangedEvent -= OnGameStateChanged;
        }
    }
}