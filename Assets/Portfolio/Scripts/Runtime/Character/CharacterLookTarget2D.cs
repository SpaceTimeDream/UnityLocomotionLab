using System.Collections.Generic;
using UnityEngine;

namespace Portfolio.Character
{
    public abstract class CharacterLookTarget2D : MonoBehaviour
    {
        //------------------------------- Parameters -------------------------------//

        #region Components

        [Header("LOOK TARGET")]
        [SerializeField] private Transform _lookPoint;
        [SerializeField] private int _priority;
        [SerializeField] private bool _availableForLookAt = true;

        public int Priority => _priority;
        public Transform LookPoint => _lookPoint != null ? _lookPoint : transform;
        public bool AvailableForLookAt
        {
            get => _availableForLookAt;
            set => _availableForLookAt = value;
        }

        #endregion

        #region Flags

        private static readonly HashSet<CharacterLookTarget2D> _activeTargets =
            new HashSet<CharacterLookTarget2D>();

        internal static IEnumerable<CharacterLookTarget2D> ActiveTargets => _activeTargets;

        #endregion

        //------------------------------- Unity Methods -------------------------------//

        #region Unity

        protected virtual void OnEnable()
        {
            _activeTargets.Add(this);
        }

        protected virtual void OnDisable()
        {
            _activeTargets.Remove(this);
        }

        #endregion

        //------------------------------- Public Methods -------------------------------//

        public bool IsAvailableTo(CharacterAnimationController2D character)
        {
            return _availableForLookAt &&
                isActiveAndEnabled &&
                character != null &&
                CanBeLookedAtBy(character);
        }

        public virtual Vector3 GetLookPosition(CharacterAnimationController2D character)
        {
            return LookPoint.position;
        }

        //------------------------------- Private Methods -------------------------------//

        protected abstract bool CanBeLookedAtBy(CharacterAnimationController2D character);

#if UNITY_EDITOR
        //------------------------------- Editor -------------------------------//

        private void OnValidate()
        {
            _priority = Mathf.Clamp(_priority, -100, 100);
        }
#endif
    }
}
