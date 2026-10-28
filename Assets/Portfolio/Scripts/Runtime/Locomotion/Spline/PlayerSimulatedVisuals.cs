using Portfolio.Locomotion.Core;
using UnityEngine;

namespace Portfolio.Locomotion.Spline
{
    /// <summary>
    /// Owns orientation and facing presentation for the simulated player.
    /// </summary>
    [System.Serializable]
    public sealed class PlayerSimulatedVisuals
    {
        //------------------------------- Parameters -------------------------------//

        #region Components

        [Header("VISUAL PARAMETERS")]
        [SerializeField] private float _groundedAlignmentSpeed = 18f;
        [SerializeField] private bool _constrainZRotation = true;
        [SerializeField] private bool _flipVisualWithFacing = true;

        #endregion

        #region Flags

        private Vector3 _startingVisualScale = Vector3.one;
        private float _startingVisualZRotation;

        #endregion

        #region Properties

        public bool FlipVisualWithFacing => _flipVisualWithFacing;

        #endregion

        //------------------------------- Core / Routines Methods -------------------------------//

        #region Visuals

        public void Initialize(Transform visualRoot)
        {
            if (visualRoot == null)
            {
                return;
            }

            _startingVisualZRotation = visualRoot.eulerAngles.z;
            _startingVisualScale = visualRoot.localScale;
        }

        public void ApplyFacing(Transform visualRoot, float facingDirection)
        {
            if (!_flipVisualWithFacing || visualRoot == null)
            {
                return;
            }

            Vector3 scale = _startingVisualScale;
            scale.x = Mathf.Abs(scale.x) * facingDirection;
            visualRoot.localScale = scale;
        }

        public void Align(Transform visualRoot, LocomotionState state, float deltaTime)
        {
            if (visualRoot == null || state == null)
            {
                return;
            }

            Quaternion targetRotation = _constrainZRotation
                ? Quaternion.Euler(0f, 0f, _startingVisualZRotation)
                : GetTargetRotation(state);

            visualRoot.rotation = Quaternion.Lerp(
                visualRoot.rotation,
                targetRotation,
                _groundedAlignmentSpeed * deltaTime);
        }

        public void Validate()
        {
            _groundedAlignmentSpeed = Mathf.Max(0f, _groundedAlignmentSpeed);
        }

        #endregion

        //------------------------------- Helpers -------------------------------//

        #region Helpers

        private static Quaternion GetTargetRotation(LocomotionState state)
        {
            return state.Grounded
                ? Quaternion.FromToRotation(Vector3.up, state.SurfaceNormal)
                : Quaternion.identity;
        }

        #endregion
    }
}
