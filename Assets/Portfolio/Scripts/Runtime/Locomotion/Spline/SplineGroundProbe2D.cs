using UnityEngine;

namespace Portfolio.Locomotion.Spline
{
    public class SplineGroundProbe2D : MonoBehaviour
    {
        // Fields
        [SerializeField] private Collider2D _groundCollider;
        [SerializeField] private LayerMask _groundLayers = Physics2D.DefaultRaycastLayers;
        [SerializeField] private float _probeStartOffset = 0.18f;
        [SerializeField] private float _groundedProbeDistance = 0.32f;
        [SerializeField] private float _landingSkin = 0.08f;
        [SerializeField] private bool _drawDebugRays = true;

        // Properties
        public Collider2D GroundCollider => _groundCollider;

        // Unity Methods
#if UNITY_EDITOR
        private void OnValidate()
        {
            _probeStartOffset = Mathf.Max(0.01f, _probeStartOffset);
            _groundedProbeDistance = Mathf.Max(0.01f, _groundedProbeDistance);
            _landingSkin = Mathf.Max(0.001f, _landingSkin);
        }
#endif

        // Public Methods
        public bool TryGetGroundedHit(Vector2 anchorPosition, out RaycastHit2D hit)
        {
            Vector2 origin = anchorPosition + Vector2.up * _probeStartOffset;
            float distance = _probeStartOffset + _groundedProbeDistance;
            hit = Physics2D.Raycast(origin, Vector2.down, distance, _groundLayers);
            DrawRay(origin, Vector2.down * distance, hit.collider != null);
            return IsValidHit(hit);
        }

        public bool TryGetLandingHit(Vector2 previousPosition, Vector2 proposedPosition, out RaycastHit2D hit)
        {
            Vector2 motion = proposedPosition - previousPosition;

            if (motion.y >= 0f || motion.sqrMagnitude <= 0.000001f)
            {
                hit = default;
                return false;
            }

            Vector2 origin = previousPosition + Vector2.up * _landingSkin;
            Vector2 direction = motion.normalized;
            float distance = motion.magnitude + _landingSkin * 2f;
            hit = Physics2D.Raycast(origin, direction, distance, _groundLayers);
            DrawRay(origin, direction * distance, hit.collider != null);

            if (!IsValidHit(hit))
            {
                return false;
            }

            return hit.normal.y > 0.01f && Vector2.Dot(direction, hit.normal) < 0f;
        }

        // Private Methods
        private bool IsValidHit(RaycastHit2D hit)
        {
            if (hit.collider == null)
            {
                return false;
            }

            return _groundCollider == null || hit.collider == _groundCollider;
        }

        private void DrawRay(Vector2 origin, Vector2 direction, bool hit)
        {
            if (!_drawDebugRays)
            {
                return;
            }

            Debug.DrawRay(origin, direction, hit ? Color.green : Color.red);
        }
    }
}
