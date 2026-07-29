using Portfolio.Locomotion.Core;
using UnityEngine;

namespace Portfolio.Locomotion.Rigidbody
{
    [System.Serializable]
    public class ExternalForceReceiver2D
    {
        //------------------------------- Game Pause -------------------------------//

        //------------------------------- Debugging Methods -------------------------------//

        //------------------------------- Parameters -------------------------------//

        #region Components

        [Header("EXTERNAL FORCE PARAMETERS")]
        [SerializeField] private float _horizontalProtectionTime = 0.12f;
        [SerializeField] private float _externalForceResistance = 3f;
        [SerializeField] private float _settledSpeedThreshold = 0.25f;

        private float _protectedUntilTime;
        private bool _hasExternalForce;
        private bool _clearOnGrounded;

        public bool HasExternalForce => _hasExternalForce;
        public bool IsHorizontalProtected => Time.time < _protectedUntilTime;
        public bool IsControlProtected => _hasExternalForce && IsHorizontalProtected;
        public float Resistance => _hasExternalForce ? Mathf.Max(1f, _externalForceResistance) : 1f;

        #endregion

        #region Flags

        #endregion

        //------------------------------- Unity Methods -------------------------------//

        #region Unity

        #endregion

        //------------------------------- Initialization Methods -------------------------------//

        #region Initialization

        #endregion

        //------------------------------- Core / Routines Methods -------------------------------//

        public void AddImpulse(
            UnityEngine.Rigidbody2D body,
            Vector2 impulse,
            bool protectHorizontalMomentum = true,
            bool clearOnGrounded = false)
        {
            body.AddForce(impulse, ForceMode2D.Impulse);
            BeginExternalForce(protectHorizontalMomentum, clearOnGrounded);
        }

        public void AddVelocity(
            UnityEngine.Rigidbody2D body,
            Vector2 velocity,
            bool protectHorizontalMomentum = true,
            bool clearOnGrounded = false)
        {
            body.linearVelocity += velocity;
            BeginExternalForce(protectHorizontalMomentum, clearOnGrounded);
        }

        public void SetVelocity(
            UnityEngine.Rigidbody2D body,
            Vector2 velocity,
            bool protectHorizontalMomentum = true,
            bool clearOnGrounded = false)
        {
            body.linearVelocity = velocity;
            BeginExternalForce(protectHorizontalMomentum, clearOnGrounded);
        }

        public void UpdateRuntimeState(UnityEngine.Rigidbody2D body, LocomotionState state)
        {
            if (!_hasExternalForce || IsHorizontalProtected)
            {
                return;
            }

            if (_clearOnGrounded && state.Grounded)
            {
                ClearRuntimeState();
                return;
            }

            if (state.Grounded && body.linearVelocity.sqrMagnitude <= _settledSpeedThreshold * _settledSpeedThreshold)
            {
                ClearRuntimeState();
            }
        }

        public void ClearRuntimeState()
        {
            _protectedUntilTime = 0f;
            _hasExternalForce = false;
            _clearOnGrounded = false;
        }

        public void Validate()
        {
            _horizontalProtectionTime = Mathf.Max(0f, _horizontalProtectionTime);
            _externalForceResistance = Mathf.Max(1f, _externalForceResistance);
            _settledSpeedThreshold = Mathf.Max(0f, _settledSpeedThreshold);
        }

        private void BeginExternalForce(bool protectHorizontalMomentum, bool clearOnGrounded)
        {
            _hasExternalForce = true;
            _clearOnGrounded = clearOnGrounded;

            if (protectHorizontalMomentum)
            {
                _protectedUntilTime = Mathf.Max(_protectedUntilTime, Time.time + _horizontalProtectionTime);
            }
            else
            {
                _protectedUntilTime = 0f;
            }
        }

#if UNITY_EDITOR
        //------------------------------- Editor -------------------------------//
#endif
    }
}
