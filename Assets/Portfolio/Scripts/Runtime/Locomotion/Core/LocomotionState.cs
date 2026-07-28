using UnityEngine;

namespace Portfolio.Locomotion.Core
{
    [System.Serializable]
    public class LocomotionState
    {
        //------------------------------- Game Pause -------------------------------//

        //------------------------------- Debugging Methods -------------------------------//

        //------------------------------- Parameters -------------------------------//

        #region Components
        [SerializeField] private bool _controlEnabled = true;
        [SerializeField] private bool _grounded;
        [SerializeField] private bool _rising;
        [SerializeField] private bool _falling;
        [SerializeField] private bool _onWalkableSlope;
        [SerializeField] private bool _onSteepSlope;
        [SerializeField] private bool _sliding;
        [SerializeField] private Vector2 _surfaceNormal = Vector2.up;
        [SerializeField] private float _slopeAngle;

        // Properties
        public bool ControlEnabled => _controlEnabled;
        public bool Grounded => _grounded;
        public bool Rising => _rising;
        public bool Falling => _falling;
        public bool OnWalkableSlope => _onWalkableSlope;
        public bool OnSteepSlope => _onSteepSlope;
        public bool Sliding => _sliding;
        public Vector2 SurfaceNormal => _surfaceNormal;
        public float SlopeAngle => _slopeAngle;

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
        public void SetControlEnabled(bool controlEnabled)
        {
            _controlEnabled = controlEnabled;
        }

        public void SetSurface(bool grounded, bool onWalkableSlope, bool onSteepSlope, Vector2 surfaceNormal, float slopeAngle)
        {
            _grounded = grounded;
            _onWalkableSlope = onWalkableSlope;
            _onSteepSlope = onSteepSlope;
            _surfaceNormal = surfaceNormal.sqrMagnitude > 0f ? surfaceNormal.normalized : Vector2.up;
            _slopeAngle = slopeAngle;
        }

        public void SetVerticalMotion(float velocityY)
        {
            _rising = velocityY > 0.01f;
            _falling = velocityY < -0.01f;
        }

        public void SetSliding(bool sliding)
        {
            _sliding = sliding;
        }

        public void ResetRuntimeState()
        {
            _grounded = false;
            _rising = false;
            _falling = false;
            _onWalkableSlope = false;
            _onSteepSlope = false;
            _sliding = false;
            _surfaceNormal = Vector2.up;
            _slopeAngle = 0f;
        }
#if UNITY_EDITOR
        //------------------------------- Editor -------------------------------//
#endif
    }
}
