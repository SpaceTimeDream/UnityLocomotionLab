using Portfolio.Locomotion.Spline.Path;
using UnityEngine;

namespace Portfolio.Locomotion.Spline
{
    public class SplinePathLineRenderer2D : MonoBehaviour
    {
        // Fields
        [SerializeField] private SpriteShapeSplinePath2D _path;
        [SerializeField] private LineRenderer _lineRenderer;
        [SerializeField] private int _maximumSamples = 512;

        private Vector3[] _positions;

        // Unity Methods
        private void Start()
        {
            Initialize();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _maximumSamples = Mathf.Max(2, _maximumSamples);
            if (_path == null) _path = GetComponent<SpriteShapeSplinePath2D>();
            if (_lineRenderer == null) _lineRenderer = GetComponent<LineRenderer>();
        }
#endif

        // Private Methods
        private void Initialize()
        {
            InitializeComponents();
            InitializeModules();
            InitializeEvents();
            RefreshLine();
        }

        private void InitializeComponents()
        {
            if (_path == null) _path = GetComponent<SpriteShapeSplinePath2D>();
            if (_lineRenderer == null) _lineRenderer = GetComponent<LineRenderer>();
        }

        private void InitializeModules()
        {
            _positions = new Vector3[_maximumSamples];
        }

        private void InitializeEvents() { }

        private void RefreshLine()
        {
            if (_path == null || _lineRenderer == null || !_path.IsValid())
            {
                return;
            }

            int count = _path.CopySamplePositions(_positions);
            _lineRenderer.positionCount = count;

            for (int i = 0; i < count; i++)
            {
                _lineRenderer.SetPosition(i, _positions[i]);
            }
        }
    }
}
