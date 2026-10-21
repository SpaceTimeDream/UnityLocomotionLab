using System;
using System.Collections.Generic;
using UnityEngine;
using Portfolio.Locomotion.Core;
using Portfolio.Locomotion.Rigidbody;
using Portfolio.Locomotion.Spline;

namespace Portfolio.Character
{
    public enum CharacterArm2D
    {
        Front,
        Back
    }

    [DisallowMultipleComponent]
    public class CharacterAnimationController2D : MonoBehaviour
    {
        //------------------------------- Game Pause -------------------------------//
        // This component only drives animation and bone poses. Gameplay systems own input and movement.

        //------------------------------- Parameters -------------------------------//

        #region Components

        [Header("COMPONENTS")]
        [SerializeField] private Animator _animator;
        [Tooltip("Transform whose world-facing direction is used for head look-at.")]
        [SerializeField] private Transform _facingRoot;
        [SerializeField] private RigidbodyLocomotionController _rigidbodyLocomotion;
        [SerializeField] private SimulatedSplineLocomotionController _simulatedSplineLocomotion;

        [Header("LOCOMOTION ANIMATION")]
        [SerializeField] private bool _driveLocomotionAnimation = true;
        [SerializeField, Min(0f)] private float _movementInputThreshold = 0.1f;
        [SerializeField, Min(0f)] private float _movementSpeedThreshold = 0.1f;

        [Header("HEAD AIM")]
        [SerializeField] private AimBoneSettings _head = new AimBoneSettings(
            "Skeletal/bone_1/bone_2/bone_3/bone_4", -75f, 75f);

        [Header("FRONT ARM AIM")]
        [SerializeField] private AimBoneSettings _frontUpperArm = new AimBoneSettings(
            "Skeletal/bone_1/bone_2/bone_3/bone_5", -130f, 130f, 720f);
        [SerializeField] private AimBoneSettings _frontForearm = new AimBoneSettings(
            "Skeletal/bone_1/bone_2/bone_3/bone_5/bone_6", -145f, 145f, 720f);

        [Header("BACK ARM AIM")]
        [SerializeField] private AimBoneSettings _backUpperArm = new AimBoneSettings(
            "Skeletal/bone_1/bone_2/bone_3/bone_7", -130f, 130f, 720f);
        [SerializeField] private AimBoneSettings _backForearm = new AimBoneSettings(
            "Skeletal/bone_1/bone_2/bone_3/bone_7/bone_8", -145f, 145f, 720f);

        [Header("AUTOMATIC HEAD LOOK")]
        [SerializeField] private bool _automaticHeadLook = true;
        [SerializeField] private float _lookAtRadius = 5f;
        [SerializeField, Range(0f, 90f)] private float _maximumInterestAngle = 85f;

        [Header("DEBUG PARAMETERS")]
        [SerializeField] private bool _drawAimGizmos = true;

        public Animator Animator => _animator;
        public CharacterLookTarget2D CurrentInterestTarget => _currentInterestTarget;
        public bool AutomaticHeadLookEnabled => _automaticHeadLook;
        public Transform HeadBone => _head != null ? _head._bone : null;
        public Transform FrontUpperArmBone => _frontUpperArm != null ? _frontUpperArm._bone : null;
        public Transform FrontForearmBone => _frontForearm != null ? _frontForearm._bone : null;
        public Transform BackUpperArmBone => _backUpperArm != null ? _backUpperArm._bone : null;
        public Transform BackForearmBone => _backForearm != null ? _backForearm._bone : null;

        #endregion

        #region Flags

        private bool _initialized;
        private CharacterLookTarget2D _currentInterestTarget;
        private bool _manualHeadControl;
        private bool _hasFacingDirectionOverride;
        private float _facingDirectionOverride = 1f;
        private RuntimeAnimatorController _cachedAnimatorController;
        private readonly Dictionary<int, AnimatorControllerParameterType> _animatorParameters =
            new Dictionary<int, AnimatorControllerParameterType>();

        #endregion

        //------------------------------- Unity Methods -------------------------------//

        #region Unity

        private void Start()
        {
            Initialize();
        }

        private void Update()
        {
            if (!_initialized)
            {
                return;
            }

            UpdateLocomotionAnimation();
        }

        private void LateUpdate()
        {
            if (!_initialized)
            {
                return;
            }

            UpdateAutomaticHeadLook();
            UpdateHeadAim();
            UpdateAim(_frontUpperArm);
            UpdateAim(_frontForearm);
            UpdateAim(_backUpperArm);
            UpdateAim(_backForearm);
        }

        private void OnDisable()
        {
            ResetAimOnDisable(_head);
            ResetAimOnDisable(_frontUpperArm);
            ResetAimOnDisable(_frontForearm);
            ResetAimOnDisable(_backUpperArm);
            ResetAimOnDisable(_backForearm);
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

            InitializeComponents();
            InitializeAim(_head);
            InitializeAim(_frontUpperArm);
            InitializeAim(_frontForearm);
            InitializeAim(_backUpperArm);
            InitializeAim(_backForearm);
            RefreshAnimatorParameters();

            _initialized = true;
        }

        private void InitializeComponents()
        {
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>(true);
            }

            if (_rigidbodyLocomotion == null)
            {
                _rigidbodyLocomotion = GetComponentInParent<RigidbodyLocomotionController>();
            }

            if (_simulatedSplineLocomotion == null)
            {
                _simulatedSplineLocomotion = GetComponentInParent<SimulatedSplineLocomotionController>();
            }

            if (_facingRoot == null)
            {
                if (_rigidbodyLocomotion != null && _rigidbodyLocomotion.VisualRoot != null)
                {
                    _facingRoot = _rigidbodyLocomotion.VisualRoot;
                }
                else if (_simulatedSplineLocomotion != null && _simulatedSplineLocomotion.VisualRoot != null)
                {
                    _facingRoot = _simulatedSplineLocomotion.VisualRoot;
                }
                else if (_animator != null)
                {
                    _facingRoot = _animator.transform;
                }
                else
                {
                    _facingRoot = transform;
                }
            }

            ResolveBone(_head);
            ResolveBone(_frontUpperArm);
            ResolveBone(_frontForearm);
            ResolveBone(_backUpperArm);
            ResolveBone(_backForearm);
        }

        private void ResolveBone(AimBoneSettings settings)
        {
            if (settings == null || settings._bone != null || _animator == null ||
                string.IsNullOrWhiteSpace(settings._bonePath))
            {
                return;
            }

            settings._bone = _animator.transform.Find(settings._bonePath);
        }

        private void InitializeAim(AimBoneSettings settings)
        {
            if (settings == null || settings._bone == null)
            {
                return;
            }

            settings._baseLocalRotation = settings._bone.localRotation;
            settings._currentLocalRotation = settings._baseLocalRotation;
            settings._currentAimOffset = 0f;
            settings._lastAppliedAimOffset = 0f;
            settings._hasLastAppliedAimPose = false;
            settings._hasBaseRotation = true;
            settings._isOverriding = false;
        }

        private void RefreshAnimatorParameters()
        {
            if (_animator == null)
            {
                _cachedAnimatorController = null;
                _animatorParameters.Clear();
                return;
            }

            if (_cachedAnimatorController == _animator.runtimeAnimatorController)
            {
                return;
            }

            _cachedAnimatorController = _animator.runtimeAnimatorController;
            _animatorParameters.Clear();

            if (_cachedAnimatorController == null)
            {
                return;
            }

            AnimatorControllerParameter[] parameters = _animator.parameters;

            for (int i = 0; i < parameters.Length; i++)
            {
                _animatorParameters[parameters[i].nameHash] = parameters[i].type;
            }
        }

        #endregion

        //------------------------------- Core / Routines Methods -------------------------------//

        #region Animation Parameters

        public void SetAnimatorFloat(string parameterName, float value, float dampTime = 0f)
        {
            if (!HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Float))
            {
                return;
            }

            if (dampTime > 0f)
            {
                _animator.SetFloat(parameterName, value, dampTime, Time.deltaTime);
                return;
            }

            _animator.SetFloat(parameterName, value);
        }

        public void SetAnimatorBool(string parameterName, bool value)
        {
            if (HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Bool))
            {
                _animator.SetBool(parameterName, value);
            }
        }

        public void SetAnimatorInteger(string parameterName, int value)
        {
            if (HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Int))
            {
                _animator.SetInteger(parameterName, value);
            }
        }

        public void SetAnimatorTrigger(string parameterName)
        {
            if (HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Trigger))
            {
                _animator.SetTrigger(parameterName);
            }
        }

        public void ResetAnimatorTrigger(string parameterName)
        {
            if (HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Trigger))
            {
                _animator.ResetTrigger(parameterName);
            }
        }

        public void SetAnimatorLayerWeight(int layerIndex, float weight)
        {
            EnsureInitialized();

            if (_animator == null || layerIndex < 0 || layerIndex >= _animator.layerCount)
            {
                return;
            }

            _animator.SetLayerWeight(layerIndex, Mathf.Clamp01(weight));
        }

        public void PlayState(string stateName, int layerIndex = 0, float normalizedTime = 0f)
        {
            if (string.IsNullOrWhiteSpace(stateName))
            {
                return;
            }

            EnsureInitialized();
            int stateHash = Animator.StringToHash(stateName);

            if (_animator != null && layerIndex >= 0 && _animator.HasState(layerIndex, stateHash))
            {
                _animator.Play(stateHash, layerIndex, normalizedTime);
            }
        }

        #endregion

        #region Character Animation Methods

        public void SetRunning(bool running)
        {
            SetAnimatorBool("isRun", running);
        }

        public void SetJumping(bool jumping)
        {
            SetAnimatorBool("isJump", jumping);
        }

        public void SetLookingUp(bool lookingUp)
        {
            SetAnimatorBool("isLookUp", lookingUp);
        }

        public void Idle()
        {
            ResetLocomotionAnimationFlags();
            SetAnimatorTrigger("idle");
        }

        public void Attack()
        {
            ResetLocomotionAnimationFlags();
            SetAnimatorTrigger("attack");
        }

        public void TripOver()
        {
            ResetLocomotionAnimationFlags();
            SetAnimatorTrigger("tripOver");
        }

        public void Hurt()
        {
            ResetLocomotionAnimationFlags();
            SetAnimatorTrigger("hurt");
        }

        public void Die()
        {
            ResetLocomotionAnimationFlags();
            SetAnimatorTrigger("die");
        }

        public void LookUp()
        {
            ResetLocomotionAnimationFlags();
            SetLookingUp(true);
        }

        public void Run()
        {
            ResetLocomotionAnimationFlags();
            SetRunning(true);
        }

        public void Jump()
        {
            ResetLocomotionAnimationFlags();
            SetJumping(true);
        }

        public void ResetLocomotionAnimationFlags()
        {
            SetLookingUp(false);
            SetRunning(false);
            SetJumping(false);
        }

        #endregion

        #region Locomotion Animation

        private void UpdateLocomotionAnimation()
        {
            if (!_driveLocomotionAnimation || _animator == null)
            {
                return;
            }

            LocomotionState state;
            Vector2 velocity;
            float movementInput;

            if (_rigidbodyLocomotion != null)
            {
                state = _rigidbodyLocomotion.State;
                velocity = _rigidbodyLocomotion.Velocity;
                movementInput = _rigidbodyLocomotion.MovementInput;
            }
            else if (_simulatedSplineLocomotion != null)
            {
                state = _simulatedSplineLocomotion.State;
                velocity = _simulatedSplineLocomotion.Velocity;
                movementInput = _simulatedSplineLocomotion.MovementInput;
            }
            else
            {
                return;
            }

            if (state == null)
            {
                return;
            }

            bool isMovingOnGround = state.Grounded &&
                Mathf.Abs(movementInput) > _movementInputThreshold &&
                Mathf.Abs(velocity.x) > _movementSpeedThreshold;

            bool isAirborne = !state.Grounded &&
                (state.IsJumping || state.Rising || state.Falling);

            SetRunning(isMovingOnGround);
            SetJumping(isAirborne);
        }

        #endregion

        #region Head Control

        public void SetAutomaticHeadLookEnabled(bool enabled)
        {
            EnsureInitialized();
            _automaticHeadLook = enabled;

            if (!enabled && !_manualHeadControl)
            {
                _currentInterestTarget = null;
                ClearAimControl(_head);
            }
        }

        public void SetFacingDirection(float direction)
        {
            if (Mathf.Abs(direction) <= 0.001f)
            {
                return;
            }

            _facingDirectionOverride = Mathf.Sign(direction);
            _hasFacingDirectionOverride = true;
        }

        public void ClearFacingDirectionOverride()
        {
            _hasFacingDirectionOverride = false;
        }

        public void SetHeadLookTarget(Transform target)
        {
            EnsureInitialized();
            _manualHeadControl = target != null;
            _currentInterestTarget = null;
            SetTarget(_head, target);
        }

        public void SetHeadLookPosition(Vector3 worldPosition)
        {
            EnsureInitialized();
            _manualHeadControl = true;
            _currentInterestTarget = null;
            SetTargetPosition(_head, worldPosition);
        }

        public void SetHeadLocalAngle(float angleFromBase)
        {
            EnsureInitialized();
            _manualHeadControl = true;
            _currentInterestTarget = null;
            SetManualAngle(_head, angleFromBase);
        }

        public void ClearHeadLookTarget()
        {
            _manualHeadControl = false;
            _currentInterestTarget = null;
            ClearAimControl(_head);
        }

        #endregion

        #region Arm Control

        // These methods rotate the arm segments independently; they are not a two-bone IK solver.
        public void SetArmTarget(CharacterArm2D arm, Transform target)
        {
            EnsureInitialized();
            SetTarget(GetUpperArm(arm), target);
            SetTarget(GetForearm(arm), target);
        }

        public void SetArmTargetPosition(CharacterArm2D arm, Vector3 worldPosition)
        {
            EnsureInitialized();
            SetTargetPosition(GetUpperArm(arm), worldPosition);
            SetTargetPosition(GetForearm(arm), worldPosition);
        }

        public void SetArmPose(CharacterArm2D arm, float upperArmAngleFromBase, float forearmAngleFromBase)
        {
            EnsureInitialized();
            SetManualAngle(GetUpperArm(arm), upperArmAngleFromBase);
            SetManualAngle(GetForearm(arm), forearmAngleFromBase);
        }

        public void ClearArmControl(CharacterArm2D arm)
        {
            ClearAimControl(GetUpperArm(arm));
            ClearAimControl(GetForearm(arm));
        }

        public void ClearArmControls()
        {
            ClearArmControl(CharacterArm2D.Front);
            ClearArmControl(CharacterArm2D.Back);
        }

        #endregion

        //------------------------------- Helpers -------------------------------//

        #region Animator Helpers

        private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType parameterType)
        {
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                return false;
            }

            EnsureInitialized();
            RefreshAnimatorParameters();

            int parameterHash = Animator.StringToHash(parameterName);
            return _animator != null &&
                _animatorParameters.TryGetValue(parameterHash, out AnimatorControllerParameterType actualType) &&
                actualType == parameterType;
        }

        private void EnsureInitialized()
        {
            if (!_initialized)
            {
                Initialize();
            }
        }

        private AimBoneSettings GetUpperArm(CharacterArm2D arm)
        {
            switch (arm)
            {
                case CharacterArm2D.Front:
                    return _frontUpperArm;

                case CharacterArm2D.Back:
                    return _backUpperArm;

                default:
                    return null;
            }
        }

        private AimBoneSettings GetForearm(CharacterArm2D arm)
        {
            switch (arm)
            {
                case CharacterArm2D.Front:
                    return _frontForearm;

                case CharacterArm2D.Back:
                    return _backForearm;

                default:
                    return null;
            }
        }

        #endregion

        #region Automatic Head Look

        private void UpdateAutomaticHeadLook()
        {
            if (_manualHeadControl)
            {
                bool manualTargetIsValid = _head._hasManualAngle ||
                    _head._usesPosition ||
                    _head._target != null;

                if (manualTargetIsValid)
                {
                    return;
                }

                _manualHeadControl = false;
            }

            if (!_automaticHeadLook)
            {
                ClearAutomaticInterestTarget();
                return;
            }

            CharacterLookTarget2D nextTarget = FindBestInterestTarget();

            if (nextTarget == null)
            {
                ClearAutomaticInterestTarget();
                return;
            }

            _currentInterestTarget = nextTarget;
            SetTargetPosition(_head, nextTarget.GetLookPosition(this));
        }

        private CharacterLookTarget2D FindBestInterestTarget()
        {
            if (_head == null || _head._bone == null)
            {
                return null;
            }

            Vector2 origin = _head._bone.position;
            Vector2 forward = Vector2.right * GetFacingDirection();
            float maximumDistanceSqr = _lookAtRadius * _lookAtRadius;
            float bestDistanceSqr = float.PositiveInfinity;
            int bestPriority = int.MinValue;
            CharacterLookTarget2D bestTarget = null;

            foreach (CharacterLookTarget2D target in CharacterLookTarget2D.ActiveTargets)
            {
                if (target == null || !target.IsAvailableTo(this))
                {
                    continue;
                }

                Vector2 directionToTarget = (Vector2)target.GetLookPosition(this) - origin;
                float distanceSqr = directionToTarget.sqrMagnitude;

                if (distanceSqr <= 0.0001f || distanceSqr > maximumDistanceSqr)
                {
                    continue;
                }

                if (Vector2.Angle(forward, directionToTarget) > _maximumInterestAngle)
                {
                    continue;
                }

                if (target.Priority < bestPriority ||
                    (target.Priority == bestPriority && distanceSqr >= bestDistanceSqr))
                {
                    continue;
                }

                bestTarget = target;
                bestPriority = target.Priority;
                bestDistanceSqr = distanceSqr;
            }

            if (_currentInterestTarget != null &&
                _currentInterestTarget.IsAvailableTo(this) &&
                IsInsideInterestRange(_currentInterestTarget, origin, forward, maximumDistanceSqr, out float currentDistanceSqr))
            {
                if (_currentInterestTarget.Priority > bestPriority ||
                    (_currentInterestTarget.Priority == bestPriority &&
                     Mathf.Sqrt(currentDistanceSqr) <= Mathf.Sqrt(bestDistanceSqr) + 0.25f))
                {
                    return _currentInterestTarget;
                }
            }

            return bestTarget;
        }

        private bool IsInsideInterestRange(
            CharacterLookTarget2D target,
            Vector2 origin,
            Vector2 forward,
            float maximumDistanceSqr,
            out float distanceSqr)
        {
            Vector2 directionToTarget = (Vector2)target.GetLookPosition(this) - origin;
            distanceSqr = directionToTarget.sqrMagnitude;

            if (distanceSqr <= 0.0001f ||
                distanceSqr > maximumDistanceSqr ||
                Vector2.Angle(forward, directionToTarget) > _maximumInterestAngle)
            {
                return false;
            }

            return true;
        }

        private float GetFacingDirection()
        {
            if (_hasFacingDirectionOverride)
            {
                return _facingDirectionOverride;
            }

            Transform facingRoot = _facingRoot != null ? _facingRoot : transform;
            Vector3 worldRight = facingRoot.localToWorldMatrix.MultiplyVector(Vector3.right);

            if (Mathf.Abs(worldRight.x) > 0.001f)
            {
                return Mathf.Sign(worldRight.x);
            }

            return facingRoot.lossyScale.x < 0f ? -1f : 1f;
        }

        private void ClearAutomaticInterestTarget()
        {
            bool hasAutomaticAim = _head != null && _head._usesPosition;

            if (_currentInterestTarget == null && !hasAutomaticAim)
            {
                return;
            }

            _currentInterestTarget = null;
            ClearAimControl(_head);
        }

        #endregion

        #region Bone Aim Helpers

        private void SetTarget(AimBoneSettings settings, Transform target)
        {
            if (settings == null)
            {
                return;
            }

            settings._target = target;
            settings._targetPosition = Vector3.zero;
            settings._hasTarget = target != null;
            settings._usesPosition = false;
            settings._hasManualAngle = false;
        }

        private void SetTargetPosition(AimBoneSettings settings, Vector3 worldPosition)
        {
            if (settings == null)
            {
                return;
            }

            settings._target = null;
            settings._targetPosition = worldPosition;
            settings._hasTarget = true;
            settings._usesPosition = true;
            settings._hasManualAngle = false;
        }

        private void SetManualAngle(AimBoneSettings settings, float angleFromBase)
        {
            if (settings == null)
            {
                return;
            }

            settings._target = null;
            settings._hasTarget = false;
            settings._usesPosition = false;
            settings._manualAngleFromBase = angleFromBase;
            settings._hasManualAngle = true;
        }

        private void ClearAimControl(AimBoneSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            settings._target = null;
            settings._targetPosition = Vector3.zero;
            settings._hasTarget = false;
            settings._usesPosition = false;
            settings._hasManualAngle = false;
        }

        private void UpdateHeadAim()
        {
            AimBoneSettings settings = _head;

            if (settings == null || settings._bone == null)
            {
                return;
            }

            Quaternion animatedRotation = settings._bone.localRotation;

            if (settings._hasLastAppliedAimPose &&
                Quaternion.Angle(animatedRotation, settings._lastAppliedLocalRotation) <= 0.05f)
            {
                animatedRotation *= Quaternion.Euler(0f, 0f, -settings._lastAppliedAimOffset);
            }

            float animatedAngle = animatedRotation.eulerAngles.z;
            bool hasAim = TryGetDesiredHeadAimOffset(settings, animatedAngle, out float desiredAimOffset);

            if (hasAim)
            {
                settings._currentAimOffset = Mathf.MoveTowardsAngle(
                    settings._currentAimOffset,
                    desiredAimOffset,
                    settings._rotationSpeed * Time.deltaTime);
                settings._isOverriding = true;
            }
            else if (settings._isOverriding)
            {
                settings._currentAimOffset = Mathf.MoveTowardsAngle(
                    settings._currentAimOffset,
                    0f,
                    settings._rotationSpeed * Time.deltaTime);

                if (Mathf.Abs(Mathf.DeltaAngle(settings._currentAimOffset, 0f)) <= 0.1f)
                {
                    settings._currentAimOffset = 0f;
                    settings._isOverriding = false;
                }
            }
            else
            {
                settings._hasLastAppliedAimPose = false;
                return;
            }

            Quaternion aimedRotation = animatedRotation *
                Quaternion.Euler(0f, 0f, settings._currentAimOffset);

            settings._bone.localRotation = aimedRotation;
            settings._lastAppliedLocalRotation = aimedRotation;
            settings._lastAppliedAimOffset = settings._currentAimOffset;
            settings._hasLastAppliedAimPose = true;
        }

        private bool TryGetDesiredHeadAimOffset(
            AimBoneSettings settings,
            float animatedAngle,
            out float aimOffset)
        {
            aimOffset = 0f;

            if (settings._hasManualAngle)
            {
                aimOffset = Mathf.Clamp(
                    settings._manualAngleFromBase,
                    settings._minimumAimAngle,
                    settings._maximumAimAngle);
                return true;
            }

            if (!TryGetTargetPosition(settings, out Vector3 worldTargetPosition))
            {
                return false;
            }

            Transform parent = settings._bone.parent;
            Vector3 targetInParentSpace = parent != null
                ? parent.InverseTransformPoint(worldTargetPosition)
                : worldTargetPosition;
            Vector2 localDirection = targetInParentSpace - settings._bone.localPosition;

            if (localDirection.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            float targetAngle = Mathf.Atan2(localDirection.y, localDirection.x) * Mathf.Rad2Deg;
            targetAngle -= settings._forwardAngleOffset;

            aimOffset = Mathf.Clamp(
                Mathf.DeltaAngle(animatedAngle, targetAngle),
                settings._minimumAimAngle,
                settings._maximumAimAngle);

            return true;
        }

        private void UpdateAim(AimBoneSettings settings)
        {
            if (settings == null || settings._bone == null)
            {
                return;
            }

            if (TryGetDesiredLocalRotation(settings, out Quaternion desiredRotation))
            {
                if (!settings._isOverriding)
                {
                    settings._currentLocalRotation = settings._bone.localRotation;
                }

                float rotationStep = settings._rotationSpeed * Time.deltaTime;
                settings._currentLocalRotation = Quaternion.RotateTowards(
                    settings._currentLocalRotation, desiredRotation, rotationStep);
                settings._bone.localRotation = settings._currentLocalRotation;
                settings._isOverriding = true;
                return;
            }

            if (!settings._isOverriding)
            {
                return;
            }

            Quaternion returnRotation = _animator != null
                ? settings._bone.localRotation
                : settings._baseLocalRotation;
            settings._currentLocalRotation = Quaternion.RotateTowards(
                settings._currentLocalRotation,
                returnRotation,
                settings._rotationSpeed * Time.deltaTime);
            settings._bone.localRotation = settings._currentLocalRotation;

            if (Quaternion.Angle(settings._currentLocalRotation, returnRotation) <= 0.1f)
            {
                settings._currentLocalRotation = returnRotation;
                settings._bone.localRotation = returnRotation;
                settings._isOverriding = false;
            }
        }

        private bool TryGetDesiredLocalRotation(AimBoneSettings settings, out Quaternion desiredRotation)
        {
            desiredRotation = Quaternion.identity;

            if (settings._bone == null || !settings._hasBaseRotation)
            {
                return false;
            }

            float baseAngle = settings._baseLocalRotation.eulerAngles.z;

            if (settings._hasManualAngle)
            {
                float angleOffset = Mathf.Clamp(
                    settings._manualAngleFromBase,
                    settings._minimumAimAngle,
                    settings._maximumAimAngle);
                desiredRotation = Quaternion.Euler(0f, 0f, baseAngle + angleOffset);
                return true;
            }

            if (!TryGetTargetPosition(settings, out Vector3 worldTargetPosition))
            {
                settings._hasTarget = false;
                return false;
            }

            Transform parent = settings._bone.parent;
            Vector3 targetInParentSpace = parent != null
                ? parent.InverseTransformPoint(worldTargetPosition)
                : worldTargetPosition;
            Vector2 localDirection = targetInParentSpace - settings._bone.localPosition;

            if (localDirection.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            float targetAngle = Mathf.Atan2(localDirection.y, localDirection.x) * Mathf.Rad2Deg;
            targetAngle -= settings._forwardAngleOffset;

            float relativeAngle = Mathf.DeltaAngle(baseAngle, targetAngle);
            relativeAngle = Mathf.Clamp(
                relativeAngle,
                settings._minimumAimAngle,
                settings._maximumAimAngle);

            desiredRotation = Quaternion.Euler(0f, 0f, baseAngle + relativeAngle);
            return true;
        }

        private bool TryGetTargetPosition(AimBoneSettings settings, out Vector3 targetPosition)
        {
            targetPosition = Vector3.zero;

            if (settings == null || !settings._hasTarget)
            {
                return false;
            }

            if (settings._usesPosition)
            {
                targetPosition = settings._targetPosition;
                return true;
            }

            if (settings._target == null)
            {
                return false;
            }

            targetPosition = settings._target.position;
            return true;
        }

        private void ResetAimOnDisable(AimBoneSettings settings)
        {
            ClearAimControl(settings);
            RestoreBaseRotation(settings);
        }

        private void RestoreBaseRotation(AimBoneSettings settings)
        {
            if (settings == null || settings._bone == null || !settings._hasBaseRotation)
            {
                return;
            }

            settings._bone.localRotation = settings._baseLocalRotation;
            settings._currentLocalRotation = settings._baseLocalRotation;
            settings._currentAimOffset = 0f;
            settings._lastAppliedAimOffset = 0f;
            settings._hasLastAppliedAimPose = false;
            settings._isOverriding = false;
        }

        #endregion

        #if UNITY_EDITOR
        //------------------------------- Editor -------------------------------//

        private void OnValidate()
        {
            ValidateAim(_head);
            ValidateAim(_frontUpperArm);
            ValidateAim(_frontForearm);
            ValidateAim(_backUpperArm);
            ValidateAim(_backForearm);
            _lookAtRadius = Mathf.Max(0f, _lookAtRadius);
            _maximumInterestAngle = Mathf.Clamp(_maximumInterestAngle, 0f, 90f);
            _movementInputThreshold = Mathf.Max(0f, _movementInputThreshold);
            _movementSpeedThreshold = Mathf.Max(0f, _movementSpeedThreshold);

            if (!Application.isPlaying)
            {
                InitializeComponents();
                InitializeAim(_head);
                InitializeAim(_frontUpperArm);
                InitializeAim(_frontForearm);
                InitializeAim(_backUpperArm);
                InitializeAim(_backForearm);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!_drawAimGizmos)
            {
                return;
            }

            DrawAimGizmo(_head);
            DrawAimGizmo(_frontUpperArm);
            DrawAimGizmo(_frontForearm);
            DrawAimGizmo(_backUpperArm);
            DrawAimGizmo(_backForearm);

            if (_automaticHeadLook)
            {
                DrawInterestRangeGizmos();
            }
        }

        private void DrawInterestRangeGizmos()
        {
            Vector3 origin = _head != null && _head._bone != null
                ? _head._bone.position
                : transform.position;
            float direction = GetFacingDirection();
            Vector3 forward = Vector3.right * direction;

            Color previousColor = Gizmos.color;
            Gizmos.color = new Color(1f, 0.8f, 0.1f, 1f);
            Gizmos.DrawWireSphere(origin, _lookAtRadius);
            Gizmos.DrawLine(
                origin,
                origin + Quaternion.Euler(0f, 0f, _maximumInterestAngle) * forward * _lookAtRadius);
            Gizmos.DrawLine(
                origin,
                origin + Quaternion.Euler(0f, 0f, -_maximumInterestAngle) * forward * _lookAtRadius);
            Gizmos.color = previousColor;
        }

        private void DrawAimGizmo(AimBoneSettings settings)
        {
            if (settings == null || settings._bone == null ||
                !TryGetTargetPosition(settings, out Vector3 targetPosition))
            {
                return;
            }

            Color previousColor = Gizmos.color;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(settings._bone.position, targetPosition);
            Gizmos.DrawSphere(targetPosition, 0.04f);
            Gizmos.color = previousColor;
        }

        private void ValidateAim(AimBoneSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            settings._minimumAimAngle = Mathf.Clamp(settings._minimumAimAngle, -180f, 180f);
            settings._maximumAimAngle = Mathf.Clamp(settings._maximumAimAngle, -180f, 180f);

            if (settings._maximumAimAngle < settings._minimumAimAngle)
            {
                float minimum = settings._minimumAimAngle;
                settings._minimumAimAngle = settings._maximumAimAngle;
                settings._maximumAimAngle = minimum;
            }

            settings._rotationSpeed = Mathf.Max(0f, settings._rotationSpeed);
            settings._forwardAngleOffset = Mathf.Clamp(settings._forwardAngleOffset, -180f, 180f);
        }

        #endif

        //------------------------------- Nested Types -------------------------------//

        [Serializable]
        private class AimBoneSettings
        {
            [Header("BONE")]
            [SerializeField] internal string _bonePath;
            [SerializeField] internal Transform _bone;

            [Header("AIM LIMITS")]
            [SerializeField] internal float _minimumAimAngle = -75f;
            [SerializeField] internal float _maximumAimAngle = 75f;
            [SerializeField] internal float _forwardAngleOffset;
            [SerializeField] internal float _rotationSpeed = 540f;

            internal Quaternion _baseLocalRotation;
            internal Quaternion _currentLocalRotation;
            internal Quaternion _lastAppliedLocalRotation;
            internal float _currentAimOffset;
            internal float _lastAppliedAimOffset;
            internal bool _hasLastAppliedAimPose;
            internal Transform _target;
            internal Vector3 _targetPosition;
            internal float _manualAngleFromBase;
            internal bool _hasBaseRotation;
            internal bool _hasTarget;
            internal bool _usesPosition;
            internal bool _hasManualAngle;
            internal bool _isOverriding;

            public AimBoneSettings()
            {
            }

            public AimBoneSettings(
                string bonePath,
                float minimumAimAngle,
                float maximumAimAngle,
                float rotationSpeed = 540f)
            {
                _bonePath = bonePath;
                _minimumAimAngle = minimumAimAngle;
                _maximumAimAngle = maximumAimAngle;
                _rotationSpeed = rotationSpeed;
            }
        }
    }
}
