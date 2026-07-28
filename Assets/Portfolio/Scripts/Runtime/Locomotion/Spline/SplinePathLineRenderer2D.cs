using Portfolio.Locomotion.Spline.Path;
using UnityEngine;

namespace Portfolio.Locomotion.Spline
{
    [ExecuteAlways]
    public class SplinePathLineRenderer2D : MonoBehaviour
    {
        //------------------------------- Game Pause -------------------------------//

        //------------------------------- Debugging Methods -------------------------------//

        //------------------------------- Parameters -------------------------------//

        #region Components
        [SerializeField] private SpriteShapeSplinePath2D _path;
        [SerializeField] private LineRenderer _lineRenderer;
        [SerializeField] private int _maximumSamples = 512;

        private Vector3[] _positions;

        #endregion

        #region Flags

        #endregion

        //------------------------------- Unity Methods -------------------------------//

        #region Unity
        private void Start()
        {
            Initialize();
        }

        #endregion

        //------------------------------- Initialization Methods -------------------------------//

        #region Initialization

        private void Initialize()
        {
            InitializeComponents();
            InitializeModules();
            InitializeEvents();
            RefreshLine();
        }

        private void InitializeComponents()
        {
            if (_path == null)
            {
                _path = GetComponent<SpriteShapeSplinePath2D>();
            }

            if (_lineRenderer == null)
            {
                _lineRenderer = GetComponent<LineRenderer>();
            }
        }

        private void InitializeModules()
        {
            EnsurePositionBuffer();
        }

        private void InitializeEvents()
        {
        }

        #endregion

        //------------------------------- Core / Routines Methods -------------------------------//
        public void RefreshLine()
        {
            if (_path == null || _lineRenderer == null || !_path.IsValid())
            {
                return;
            }

            EnsurePositionBuffer();
            int count = _path.CopySamplePositions(_positions);
            _lineRenderer.positionCount = count;

            for (int i = 0; i < count; i++)
            {
                _lineRenderer.SetPosition(i, _positions[i]);
            }
        }

        private void EnsurePositionBuffer()
        {
            if (_positions == null || _positions.Length != _maximumSamples)
            {
                _positions = new Vector3[_maximumSamples];
            }
        }
#if UNITY_EDITOR
        //------------------------------- Editor -------------------------------//

        private void OnValidate()
        {
            _maximumSamples = Mathf.Max(2, _maximumSamples);
            InitializeComponents();

            if (!Application.isPlaying)
            {
                RefreshLine();
            }
        }
#endif
    }
}
