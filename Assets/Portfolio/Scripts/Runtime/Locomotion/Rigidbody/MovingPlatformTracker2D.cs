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
        [SerializeField] private bool _enabled = true;

        private Transform _platform;
        private Vector3 _lastPosition;

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
        public void Track(LocomotionState state, Collider2D surfaceCollider)
        {
            if (!_enabled || !state.Grounded || surfaceCollider == null)
            {
                ClearRuntimeState();
                return;
            }

            if (_platform == surfaceCollider.transform)
            {
                return;
            }

            _platform = surfaceCollider.transform;
            _lastPosition = _platform.position;
        }

        public void ApplyPlatformMotion(UnityEngine.Rigidbody2D body)
        {
            if (!_enabled || _platform == null)
            {
                return;
            }

            Vector3 delta = _platform.position - _lastPosition;
            body.position += (Vector2)delta;
            _lastPosition = _platform.position;
        }

        public void ClearRuntimeState()
        {
            _platform = null;
        }
#if UNITY_EDITOR
        //------------------------------- Editor -------------------------------//
#endif
    }
}
