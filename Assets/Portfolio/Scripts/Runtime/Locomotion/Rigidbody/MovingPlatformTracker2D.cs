using Portfolio.Locomotion.Core;
using UnityEngine;

namespace Portfolio.Locomotion.Rigidbody
{
    [System.Serializable]
    public class MovingPlatformTracker2D
    {
        //------------------------------- Game Pause -------------------------------//

        //------------------------------- Debugging Methods -------------------------------//

        //------------------------------- Parameters -------------------------------//

        #region Components

        [Header("MOVING PLATFORM PARAMETERS")]
        [SerializeField] private bool _enabled = true;

        private Transform _platform;
        private Vector2 _lastPlatformPosition;
        private Vector2 _platformLocalOffset;
        private Vector2 _currentPlatformDelta;

        public Transform CurrentPlatform => _platform;
        public Vector2 CurrentPlatformDelta => _currentPlatformDelta;
        public Vector2 PlatformLocalOffset => _platformLocalOffset;

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

        public void ApplyPlatformMotion(UnityEngine.Rigidbody2D body, bool jumpAscentActive)
        {
            _currentPlatformDelta = Vector2.zero;

            if (!_enabled || _platform == null || jumpAscentActive)
            {
                return;
            }

            Vector2 platformPosition = _platform.position;
            _currentPlatformDelta = platformPosition - _lastPlatformPosition;

            if (_currentPlatformDelta.sqrMagnitude > Mathf.Epsilon)
            {
                body.position += _currentPlatformDelta;
            }

            _lastPlatformPosition = platformPosition;
        }

        public void Track(LocomotionState state, Collider2D surfaceCollider, UnityEngine.Rigidbody2D body)
        {
            if (!_enabled || !state.Grounded || surfaceCollider == null)
            {
                ClearRuntimeState();
                return;
            }

            Transform nextPlatform = surfaceCollider.transform;

            if (_platform == nextPlatform)
            {
                return;
            }

            _platform = nextPlatform;
            _lastPlatformPosition = _platform.position;
            _platformLocalOffset = body.position - _lastPlatformPosition;
        }

        public void FinalizePlatformSync(UnityEngine.Rigidbody2D body)
        {
            if (!_enabled || _platform == null)
            {
                _currentPlatformDelta = Vector2.zero;
                return;
            }

            _platformLocalOffset = body.position - (Vector2)_platform.position;
            _lastPlatformPosition = _platform.position;
        }

        public void ClearRuntimeState()
        {
            _platform = null;
            _lastPlatformPosition = Vector2.zero;
            _platformLocalOffset = Vector2.zero;
            _currentPlatformDelta = Vector2.zero;
        }

#if UNITY_EDITOR
        //------------------------------- Editor -------------------------------//
#endif
    }
}
