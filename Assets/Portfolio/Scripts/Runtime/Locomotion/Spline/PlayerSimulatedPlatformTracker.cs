using Portfolio.Locomotion.Core;
using UnityEngine;

namespace Portfolio.Locomotion.Spline
{
    [System.Serializable]
    public sealed class PlayerSimulatedPlatformTracker
    {
        //------------------------------- Parameters -------------------------------//

        #region Components

        [SerializeField] private bool _enabled = true;

        #endregion

        #region Flags

        private Transform _platform;
        private Vector2 _lastPlatformPosition;
        private Vector2 _currentPlatformDelta;

        #endregion

        #region Properties

        public Transform CurrentPlatform => _platform;
        public Vector2 CurrentPlatformDelta => _currentPlatformDelta;

        #endregion

        //------------------------------- Core / Routines Methods -------------------------------//

        #region Core

        public void ApplyPlatformMotion(
            Transform character,
            bool grounded,
            bool jumpAscentActive)
        {
            _currentPlatformDelta = Vector2.zero;

            if (!_enabled ||
                _platform == null ||
                !grounded ||
                jumpAscentActive)
            {
                return;
            }

            Vector2 platformPosition = _platform.position;
            _currentPlatformDelta = platformPosition - _lastPlatformPosition;

            if (_currentPlatformDelta.sqrMagnitude > Mathf.Epsilon)
            {
                character.position += (Vector3)_currentPlatformDelta;
                Physics2D.SyncTransforms();
            }

            _lastPlatformPosition = platformPosition;
        }

        public void Track(
            LocomotionState state,
            Collider2D surfaceCollider)
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
        }

        public void FinalizePlatformSync()
        {
            if (!_enabled || _platform == null)
            {
                _currentPlatformDelta = Vector2.zero;
                return;
            }

            _lastPlatformPosition = _platform.position;
        }

        public void ClearRuntimeState()
        {
            _platform = null;
            _lastPlatformPosition = Vector2.zero;
            _currentPlatformDelta = Vector2.zero;
        }

        #endregion
    }
}
