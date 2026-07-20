using System;
using UnityEngine;

namespace Portfolio.Locomotion.Spline.Path
{
    [Serializable]
    public struct SplinePathSample2D
    {
        // Fields
        [SerializeField] private float _distance;
        [SerializeField] private Vector2 _position;
        [SerializeField] private Vector2 _tangent;
        [SerializeField] private Vector2 _normal;
        [SerializeField] private float _slopeAngle;
        [SerializeField] private int _segmentIndex;
        [SerializeField] private float _segmentT;

        // Properties
        public float Distance => _distance;
        public Vector2 Position => _position;
        public Vector2 Tangent => _tangent;
        public Vector2 Normal => _normal;
        public float SlopeAngle => _slopeAngle;
        public int SegmentIndex => _segmentIndex;
        public float SegmentT => _segmentT;

        // Public Methods
        public void Set(float distance, Vector2 position, Vector2 tangent, int segmentIndex, float segmentT)
        {
            _distance = distance;
            _position = position;
            _tangent = tangent.sqrMagnitude > 0.0001f ? tangent.normalized : Vector2.right;
            _normal = new Vector2(-_tangent.y, _tangent.x);

            if (_normal.y < 0f)
            {
                _normal = -_normal;
            }

            _slopeAngle = Vector2.SignedAngle(Vector2.up, _normal);
            _segmentIndex = segmentIndex;
            _segmentT = segmentT;
        }
    }
}
