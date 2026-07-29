using UnityEngine;

namespace Portfolio.Locomotion.Core
{
    public class BasicCameraFollow2D : MonoBehaviour
    {
        //------------------------------- Game Pause -------------------------------//

        //------------------------------- Debugging Methods -------------------------------//

        //------------------------------- Parameters -------------------------------//

        #region Components

        [Header("COMPONENTS")]
        [SerializeField] private Transform _target;

        [Header("FOLLOW PARAMETERS")]
        [SerializeField] private Vector3 _offset = new Vector3(0f, 1.5f, -10f);
        [SerializeField] private bool _followHorizontal = true;
        [SerializeField] private bool _followVertical = true;

        private Transform _cachedTransform;

        public Transform Target => _target;

        #endregion

        #region Flags

        #endregion

        //------------------------------- Unity Methods -------------------------------//

        #region Unity

        private void Start()
        {
            Initialize();
        }

        private void LateUpdate()
        {
            FollowTarget();
        }

        #endregion

        //------------------------------- Initialization Methods -------------------------------//

        #region Initialization

        private void Initialize()
        {
            InitializeModules();
            InitializeComponents();
            InitializeEvents();
            SnapToTarget();
        }

        private void InitializeModules()
        {
        }

        private void InitializeComponents()
        {
            _cachedTransform = transform;
        }

        private void InitializeEvents()
        {
        }

        #endregion

        //------------------------------- Core / Routines Methods -------------------------------//

        public void SetTarget(Transform target, bool snapToTarget = true)
        {
            _target = target;

            if (snapToTarget)
            {
                SnapToTarget();
            }
        }

        public void SnapToTarget()
        {
            if (_cachedTransform == null)
            {
                _cachedTransform = transform;
            }

            if (_target == null)
            {
                return;
            }

            _cachedTransform.position = GetTargetPosition();
        }

        private void FollowTarget()
        {
            if (_target == null)
            {
                return;
            }

            _cachedTransform.position = GetTargetPosition();
        }

        private Vector3 GetTargetPosition()
        {
            Vector3 cameraPosition = _cachedTransform.position;

            if (_followHorizontal)
            {
                cameraPosition.x = _target.position.x + _offset.x;
            }

            if (_followVertical)
            {
                cameraPosition.y = _target.position.y + _offset.y;
            }

            cameraPosition.z = _offset.z;
            return cameraPosition;
        }

#if UNITY_EDITOR
        //------------------------------- Editor -------------------------------//

        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                _cachedTransform = transform;
            }
        }
#endif
    }
}
