using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;

namespace Portfolio.Locomotion.Spline.Path
{
    public class SpriteShapeSplinePath2D : MonoBehaviour
    {
        // Fields
        [SerializeField] private SpriteShapeController _spriteShapeController;
        [SerializeField, Min(2)] private int _samplesPerSegment = 16;
        [SerializeField] private bool _drawSelectedGizmos = true;
        [SerializeField] private float _gizmoNormalLength = 0.35f;

        private readonly List<Vector2> _points = new List<Vector2>(128);
        private readonly List<Vector2> _tangents = new List<Vector2>(128);
        private readonly List<float> _distances = new List<float>(128);
        private readonly List<int> _segments = new List<int>(128);
        private readonly List<float> _segmentTimes = new List<float>(128);
        private Matrix4x4 _cachedMatrix;
        private int _cachedPointCount = -1;
        private int _cachedSamplesPerSegment = -1;
        private int _cachedSplineHash;
        private float _totalLength;
        private bool _cacheValid;

        // Properties
        public float TotalLength
        {
            get
            {
                EnsureCache();
                return _totalLength;
            }
        }

        public int SampleCount
        {
            get
            {
                EnsureCache();
                return _points.Count;
            }
        }

        // Unity Methods
        private void Start()
        {
            Initialize();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _samplesPerSegment = Mathf.Max(2, _samplesPerSegment);
            _gizmoNormalLength = Mathf.Max(0f, _gizmoNormalLength);

            if (_spriteShapeController == null)
            {
                _spriteShapeController = GetComponent<SpriteShapeController>();
            }

            _cacheValid = false;
        }
#endif

        private void OnDrawGizmosSelected()
        {
            DrawSelectedGizmos();
        }

        // Public Methods
        public bool IsValid()
        {
            EnsureCache();
            return _points.Count >= 2 && _totalLength > 0.0001f;
        }

        public float ClampDistance(float distance)
        {
            EnsureCache();
            return Mathf.Clamp(distance, 0f, _totalLength);
        }

        public SplinePathSample2D Evaluate(float distance)
        {
            EnsureCache();

            if (_points.Count == 0)
            {
                SplinePathSample2D empty = default;
                empty.Set(0f, transform.position, Vector2.right, 0, 0f);
                return empty;
            }

            float clampedDistance = ClampDistance(distance);
            int index = FindDistanceIndex(clampedDistance);
            int nextIndex = Mathf.Min(index + 1, _points.Count - 1);
            float span = Mathf.Max(0.0001f, _distances[nextIndex] - _distances[index]);
            float t = Mathf.Clamp01((clampedDistance - _distances[index]) / span);
            Vector2 position = Vector2.Lerp(_points[index], _points[nextIndex], t);
            Vector2 tangent = Vector2.Lerp(_tangents[index], _tangents[nextIndex], t).normalized;
            SplinePathSample2D sample = default;
            sample.Set(clampedDistance, position, tangent, _segments[index], Mathf.Lerp(_segmentTimes[index], _segmentTimes[nextIndex], t));
            return sample;
        }

        public SplinePathSample2D FindNearest(Vector2 worldPosition)
        {
            EnsureCache();
            float bestSqrDistance = float.PositiveInfinity;
            float bestDistance = 0f;

            for (int i = 0; i < _points.Count - 1; i++)
            {
                Vector2 start = _points[i];
                Vector2 end = _points[i + 1];
                Vector2 span = end - start;
                float spanSqr = span.sqrMagnitude;

                if (spanSqr <= 0.000001f)
                {
                    continue;
                }

                float t = Mathf.Clamp01(Vector2.Dot(worldPosition - start, span) / spanSqr);
                Vector2 point = start + span * t;
                float sqrDistance = (worldPosition - point).sqrMagnitude;

                if (sqrDistance >= bestSqrDistance)
                {
                    continue;
                }

                bestSqrDistance = sqrDistance;
                bestDistance = _distances[i] + span.magnitude * t;
            }

            return Evaluate(bestDistance);
        }

        public int CopySamplePositions(Vector3[] results)
        {
            EnsureCache();
            int count = Mathf.Min(results.Length, _points.Count);

            for (int i = 0; i < count; i++)
            {
                results[i] = _points[i];
            }

            return count;
        }

        public int CopyLocalSamplePositions(Vector2[] results)
        {
            EnsureCache();
            int count = Mathf.Min(results.Length, _points.Count);

            for (int i = 0; i < count; i++)
            {
                results[i] = transform.InverseTransformPoint(_points[i]);
            }

            return count;
        }

        public void InvalidateCache()
        {
            _cacheValid = false;
        }

        // Private Methods
        private void Initialize()
        {
            InitializeComponents();
            InitializeModules();
            InitializeEvents();
            RebuildCache();
        }

        private void InitializeComponents()
        {
            if (_spriteShapeController == null)
            {
                _spriteShapeController = GetComponent<SpriteShapeController>();
            }
        }

        private void InitializeModules() { }
        private void InitializeEvents() { }

        private void EnsureCache()
        {
            int splineHash = CalculateSplineHash();

            if (!_cacheValid ||
                _cachedMatrix != transform.localToWorldMatrix ||
                _cachedSamplesPerSegment != _samplesPerSegment ||
                _cachedPointCount != GetSplinePointCount() ||
                _cachedSplineHash != splineHash)
            {
                RebuildCache();
            }
        }

        private void RebuildCache()
        {
            _points.Clear();
            _tangents.Clear();
            _distances.Clear();
            _segments.Clear();
            _segmentTimes.Clear();
            _totalLength = 0f;
            _cachedMatrix = transform.localToWorldMatrix;
            _cachedSamplesPerSegment = _samplesPerSegment;
            _cachedPointCount = GetSplinePointCount();
            _cachedSplineHash = CalculateSplineHash();
            _cacheValid = true;

            if (_spriteShapeController == null || _spriteShapeController.spline.GetPointCount() < 2 || !_spriteShapeController.spline.isOpenEnded)
            {
                return;
            }

            Spline spline = _spriteShapeController.spline;
            for (int segment = 0; segment < spline.GetPointCount() - 1; segment++)
            {
                for (int step = 0; step <= _samplesPerSegment; step++)
                {
                    if (segment > 0 && step == 0)
                    {
                        continue;
                    }

                    float t = step / (float)_samplesPerSegment;
                    Vector2 point = EvaluateBezier(spline, segment, t);
                    Vector2 tangent = EvaluateBezierTangent(spline, segment, t);
                    AddSample(point, tangent, segment, t);
                }
            }
        }

        private int GetSplinePointCount()
        {
            return _spriteShapeController != null ? _spriteShapeController.spline.GetPointCount() : 0;
        }

        private int CalculateSplineHash()
        {
            if (_spriteShapeController == null)
            {
                return 0;
            }

            Spline spline = _spriteShapeController.spline;

            unchecked
            {
                int hash = 17;
                hash = hash * 31 + spline.GetPointCount();
                hash = hash * 31 + spline.isOpenEnded.GetHashCode();

                for (int i = 0; i < spline.GetPointCount(); i++)
                {
                    hash = hash * 31 + spline.GetPosition(i).GetHashCode();
                    hash = hash * 31 + spline.GetLeftTangent(i).GetHashCode();
                    hash = hash * 31 + spline.GetRightTangent(i).GetHashCode();
                }

                return hash;
            }
        }

        private Vector2 EvaluateBezier(Spline spline, int segment, float t)
        {
            Vector3 p0 = spline.GetPosition(segment);
            Vector3 p1 = p0 + spline.GetRightTangent(segment);
            Vector3 p3 = spline.GetPosition(segment + 1);
            Vector3 p2 = p3 + spline.GetLeftTangent(segment + 1);
            float inverse = 1f - t;
            Vector3 local = inverse * inverse * inverse * p0 + 3f * inverse * inverse * t * p1 + 3f * inverse * t * t * p2 + t * t * t * p3;
            return transform.TransformPoint(local);
        }

        private Vector2 EvaluateBezierTangent(Spline spline, int segment, float t)
        {
            Vector3 p0 = spline.GetPosition(segment);
            Vector3 p1 = p0 + spline.GetRightTangent(segment);
            Vector3 p3 = spline.GetPosition(segment + 1);
            Vector3 p2 = p3 + spline.GetLeftTangent(segment + 1);
            float inverse = 1f - t;
            Vector3 local = 3f * inverse * inverse * (p1 - p0) + 6f * inverse * t * (p2 - p1) + 3f * t * t * (p3 - p2);
            return transform.TransformVector(local);
        }

        private void AddSample(Vector2 point, Vector2 tangent, int segment, float segmentTime)
        {
            if (_points.Count > 0)
            {
                float length = Vector2.Distance(_points[_points.Count - 1], point);
                if (length <= 0.0001f)
                {
                    return;
                }
                _totalLength += length;
            }

            _points.Add(point);
            _tangents.Add(tangent.sqrMagnitude > 0.0001f ? tangent.normalized : Vector2.right);
            _distances.Add(_totalLength);
            _segments.Add(segment);
            _segmentTimes.Add(segmentTime);
        }

        private int FindDistanceIndex(float distance)
        {
            for (int i = 0; i < _distances.Count - 1; i++)
            {
                if (_distances[i + 1] >= distance)
                {
                    return i;
                }
            }

            return Mathf.Max(0, _distances.Count - 2);
        }

        private void DrawSelectedGizmos()
        {
            if (!_drawSelectedGizmos || !IsValid())
            {
                return;
            }

            Gizmos.color = Color.yellow;
            for (int i = 0; i < _points.Count - 1; i++)
            {
                Gizmos.DrawLine(_points[i], _points[i + 1]);
            }

            Gizmos.color = Color.cyan;
            for (int i = 0; i < _points.Count; i += Mathf.Max(1, _samplesPerSegment / 2))
            {
                Vector2 normal = new Vector2(-_tangents[i].y, _tangents[i].x);

                if (normal.y < 0f)
                {
                    normal = -normal;
                }

                Gizmos.DrawLine(_points[i], _points[i] + normal * _gizmoNormalLength);
                Gizmos.DrawLine(_points[i], _points[i] + _tangents[i] * _gizmoNormalLength);
            }
        }
    }
}
