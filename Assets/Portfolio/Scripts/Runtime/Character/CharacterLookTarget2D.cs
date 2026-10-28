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
        [SerializeField] private bool _showRuntimeDebugLabel = true;
        [SerializeField] private Vector2 _debugLabelOffset = new Vector2(0f, 0.35f);
        [SerializeField, Min(60f)] private float _debugLabelWidth = 150f;

        private static GUIStyle _debugLabelStyle;

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

        private void OnGUI()
        {
            if (!_showRuntimeDebugLabel || !Application.isPlaying || !isActiveAndEnabled)
            {
                return;
            }

            Camera targetCamera = Camera.main;

            if (targetCamera == null)
            {
                return;
            }

            Vector3 worldPosition = LookPoint.position + (Vector3)_debugLabelOffset;
            Vector3 screenPosition = targetCamera.WorldToScreenPoint(worldPosition);

            if (screenPosition.z <= 0f)
            {
                return;
            }

            if (_debugLabelStyle == null)
            {
                _debugLabelStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    wordWrap = true,
                    normal = { textColor = Color.white }
                };
            }

            const float LABEL_HEIGHT = 36f;
            Rect labelRect = new Rect(
                screenPosition.x - _debugLabelWidth * 0.5f,
                Screen.height - screenPosition.y - LABEL_HEIGHT,
                _debugLabelWidth,
                LABEL_HEIGHT);
            GUI.Label(labelRect, $"LOOK TARGET\nPRIORITY {_priority}", _debugLabelStyle);
        }

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
