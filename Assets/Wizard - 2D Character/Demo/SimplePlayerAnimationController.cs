using System;
using System.Collections.Generic;
using UnityEngine;

namespace ClearSky
{
    public enum WizardArm
    {
        Front,
        Back
    }

    [DisallowMultipleComponent]
    public class SimplePlayerAnimationController : MonoBehaviour
    {
        //------------------------------- Game Pause -------------------------------//
        // This component only drives animation and bone poses. Gameplay systems own input and movement.

        //------------------------------- Parameters -------------------------------//

        #region Components

        [Header("COMPONENTS")]
        [SerializeField] private Animator _animator;

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

        [Header("DEBUG PARAMETERS")]
        [SerializeField] private bool _drawAimGizmos = true;

        public Animator Animator => _animator;
        public Transform HeadBone => _head != null ? _head._bone : null;
        public Transform FrontUpperArmBone => _frontUpperArm != null ? _frontUpperArm._bone : null;
        public Transform FrontForearmBone => _frontForearm != null ? _frontForearm._bone : null;
        public Transform BackUpperArmBone => _backUpperArm != null ? _backUpperArm._bone : null;
        public Transform BackForearmBone => _backForearm != null ? _backForearm._bone : null;

        #endregion

        #region Flags

        private bool _initialized;
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

        private void LateUpdate()
        {
            if (!_initialized)
            {
                return;
            }

            UpdateAim(_head);
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

        #region Wizard Animation Methods

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

        #region Head Control

        public void SetHeadLookTarget(Transform target)
        {
            EnsureInitialized();
            SetTarget(_head, target);
        }

        public void SetHeadLookPosition(Vector3 worldPosition)
        {
            EnsureInitialized();
            SetTargetPosition(_head, worldPosition);
        }

        public void SetHeadLocalAngle(float angleFromBase)
        {
            EnsureInitialized();
            SetManualAngle(_head, angleFromBase);
        }

        public void ClearHeadLookTarget()
        {
            ClearAimControl(_head);
        }

        #endregion

        #region Arm Control

        // These methods rotate the arm segments independently; they are not a two-bone IK solver.
        public void SetArmTarget(WizardArm arm, Transform target)
        {
            EnsureInitialized();
            SetTarget(GetUpperArm(arm), target);
            SetTarget(GetForearm(arm), target);
        }

        public void SetArmTargetPosition(WizardArm arm, Vector3 worldPosition)
        {
            EnsureInitialized();
            SetTargetPosition(GetUpperArm(arm), worldPosition);
            SetTargetPosition(GetForearm(arm), worldPosition);
        }

        public void SetArmPose(WizardArm arm, float upperArmAngleFromBase, float forearmAngleFromBase)
        {
            EnsureInitialized();
            SetManualAngle(GetUpperArm(arm), upperArmAngleFromBase);
            SetManualAngle(GetForearm(arm), forearmAngleFromBase);
        }

        public void ClearArmControl(WizardArm arm)
        {
            ClearAimControl(GetUpperArm(arm));
            ClearAimControl(GetForearm(arm));
        }

        public void ClearArmControls()
        {
            ClearArmControl(WizardArm.Front);
            ClearArmControl(WizardArm.Back);
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

        private AimBoneSettings GetUpperArm(WizardArm arm)
        {
            switch (arm)
            {
                case WizardArm.Front:
                    return _frontUpperArm;

                case WizardArm.Back:
                    return _backUpperArm;

                default:
                    return null;
            }
        }

        private AimBoneSettings GetForearm(WizardArm arm)
        {
            switch (arm)
            {
                case WizardArm.Front:
                    return _frontForearm;

                case WizardArm.Back:
                    return _backForearm;

                default:
                    return null;
            }
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
            [SerializeField] private string _bonePath;
            [SerializeField] private Transform _bone;

            [Header("AIM LIMITS")]
            [SerializeField] private float _minimumAimAngle = -75f;
            [SerializeField] private float _maximumAimAngle = 75f;
            [SerializeField] private float _forwardAngleOffset;
            [SerializeField] private float _rotationSpeed = 540f;

            private Quaternion _baseLocalRotation;
            private Quaternion _currentLocalRotation;
            private Transform _target;
            private Vector3 _targetPosition;
            private float _manualAngleFromBase;
            private bool _hasBaseRotation;
            private bool _hasTarget;
            private bool _usesPosition;
            private bool _hasManualAngle;
            private bool _isOverriding;

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
