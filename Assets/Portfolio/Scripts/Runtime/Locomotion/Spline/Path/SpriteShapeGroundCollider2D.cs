using UnityEngine;

namespace Portfolio.Locomotion.Spline.Path
{
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteShapeSplinePath2D))]
    [RequireComponent(typeof(EdgeCollider2D))]
    public class SpriteShapeGroundCollider2D : MonoBehaviour
    {
        // Fields
        [SerializeField] private SpriteShapeSplinePath2D _path;
        [SerializeField] private EdgeCollider2D _edgeCollider;
        [SerializeField, Min(2)] private int _maximumSamples = 512;

        private Vector2[] _points;

        // Properties
        public EdgeCollider2D EdgeCollider => _edgeCollider;

        // Unity Methods
        private void Start()
        {
            Initialize();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _maximumSamples = Mathf.Max(2, _maximumSamples);
            InitializeComponents();
            RefreshCollider();
        }
#endif

        // Public Methods
        public void RefreshCollider()
        {
            if (_path == null || _edgeCollider == null || !_path.IsValid())
            {
                return;
            }

            int sampleCount = Mathf.Min(_path.SampleCount, _maximumSamples);

            if (_points == null || _points.Length != sampleCount)
            {
                _points = new Vector2[sampleCount];
            }

            int count = _path.CopyLocalSamplePositions(_points);

            if (count < 2)
            {
                return;
            }

            _edgeCollider.points = _points;
        }

        // Private Methods
        private void Initialize()
        {
            InitializeComponents();
            InitializeModules();
            InitializeEvents();
            RefreshCollider();
        }

        private void InitializeComponents()
        {
            if (_path == null)
            {
                _path = GetComponent<SpriteShapeSplinePath2D>();
            }

            if (_edgeCollider == null)
            {
                _edgeCollider = GetComponent<EdgeCollider2D>();
            }
        }

        private void InitializeModules()
        {
        }

        private void InitializeEvents()
        {
        }
    }
}
