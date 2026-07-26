using UnityEngine;

namespace Portfolio.Locomotion.Spline
{
    public class SimulatedSplineLocomotionController : MonoBehaviour
    {
        // Fields
        [SerializeField] private Transform _visualRoot;
        [SerializeField] private SimulatedSplineMovement _movement;
        [SerializeField] private SimulatedSplineJump _jump;
        [SerializeField] private SplineGroundConstraint2D _groundConstraint;
        [SerializeField] private SplineLocomotionState _state = new SplineLocomotionState();
        [SerializeField] private float _demoResetY = -8f;
        [SerializeField] private float _groundedAlignmentSpeed = 18f;

        private Transform _cachedTransform;
        private Vector2 _movementInput;
        private Vector2 _spawnPosition;
        private bool _initialized;

        // Properties
        public SplineLocomotionState State => _state;

        // Unity Methods
        private void Start()
        {
            Initialize();
        }

        private void FixedUpdate()
        {
            SimulateFixedStep();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _groundedAlignmentSpeed = Mathf.Max(0f, _groundedAlignmentSpeed);

            if (_movement == null)
            {
                _movement = GetComponent<SimulatedSplineMovement>();
            }

            if (_jump == null)
            {
                _jump = GetComponent<SimulatedSplineJump>();
            }

            if (_groundConstraint == null)
            {
                _groundConstraint = GetComponent<SplineGroundConstraint2D>();
            }
        }
#endif

        // Public Methods
        public void SetMovementInput(Vector2 input)
        {
            _movementInput = Vector2.ClampMagnitude(input, 1f);
        }

        public void RequestJump()
        {
            _jump?.RequestJump();
        }

        public void ReleaseJump()
        {
            _jump?.ReleaseJump();
        }

        public void SetControlEnabled(bool enabled)
        {
            _state.ControlEnabled = enabled;
            if (!enabled)
            {
                _movementInput = Vector2.zero;
            }
        }

        public void ResetRuntimeState()
        {
            _state.Reset(_cachedTransform.position);
            _jump?.ResetRuntime();
            SnapToNearestPathPoint();
        }

        public void SnapToNearestPathPoint()
        {
            if (_groundConstraint == null || !_groundConstraint.IsValid())
            {
                return;
            }

            _groundConstraint.SnapNearest(_state, _cachedTransform);
            AlignVisual(true);
        }

        public void ResetToSpawn()
        {
            _cachedTransform.position = _spawnPosition;
            ResetRuntimeState();
        }

        // Private Methods
        private void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            InitializeComponents();
            InitializeModules();
            InitializeEvents();
            _spawnPosition = _cachedTransform.position;
            _state.Reset(_spawnPosition);
            SnapToNearestPathPoint();
            _initialized = true;
        }

        private void InitializeComponents()
        {
            _cachedTransform = transform;

            if (_visualRoot == null && transform.childCount > 0)
            {
                _visualRoot = transform.GetChild(0);
            }
        }

        private void InitializeModules()
        {
            if (_movement == null)
            {
                _movement = GetComponent<SimulatedSplineMovement>();
            }

            if (_jump == null)
            {
                _jump = GetComponent<SimulatedSplineJump>();
            }

            if (_groundConstraint == null)
            {
                _groundConstraint = GetComponent<SplineGroundConstraint2D>();
            }
        }

        private void InitializeEvents()
        {
        }

        private void SimulateFixedStep()
        {
            if (!_initialized || _movement == null || _jump == null || _groundConstraint == null || !_groundConstraint.IsValid())
            {
                return;
            }

            if (_cachedTransform.position.y < _demoResetY)
            {
                ResetToSpawn();
                return;
            }

            float horizontalInput = _state.ControlEnabled ? _movementInput.x : 0f;
            UpdateGroundContact();
            _jump.NotifyGrounded(_state.Grounded);

            if (_state.ControlEnabled && _jump.CanJump())
            {
                _jump.ConsumeJump(_state);
            }

            if (_state.Grounded)
            {
                SimulateGrounded(horizontalInput);
                return;
            }

            SimulateAirborne(horizontalInput);
        }

        private void SimulateGrounded(float horizontalInput)
        {
            _movement.SimulateGrounded(_state, horizontalInput, Time.fixedDeltaTime);
            _groundConstraint.ConstrainAtDistance(_state, _cachedTransform, _movement.WalkableSlopeAngle);
            AlignVisual(false);
        }

        private void SimulateAirborne(float horizontalInput)
        {
            Vector2 previousPosition = _cachedTransform.position;
            _movement.SimulateAirborne(_state, horizontalInput, Time.fixedDeltaTime);
            _jump.ApplyEarlyRelease(_state);
            Vector2 motion = _state.AirborneVelocity * Time.fixedDeltaTime;
            Vector2 proposedPosition = previousPosition + motion;
            _state.Rising = _state.AirborneVelocity.y > 0f;
            _state.Falling = _state.AirborneVelocity.y < 0f;

            if (_groundConstraint.TryLand(_state, previousPosition, proposedPosition, _movement.WalkableSlopeAngle))
            {
                TranslateTo(_state.AnchorPosition);
                AlignVisual(false);

                _jump.NotifyGrounded(true);

                if (_jump.HasBufferedJump())
                {
                    _jump.ConsumeJump(_state);
                }
                return;
            }

            _cachedTransform.Translate(motion, Space.World);
            _state.AnchorPosition = _cachedTransform.position;
            AlignVisual(true);
        }

        private void UpdateGroundContact()
        {
            if (!_state.Grounded || _groundConstraint.HasGroundContact(_cachedTransform.position))
            {
                return;
            }

            _groundConstraint.Detach(_state);
        }

        private void AlignVisual(bool upright)
        {
            if (_visualRoot == null)
            {
                return;
            }

            Quaternion targetRotation = upright ? Quaternion.identity : Quaternion.FromToRotation(Vector3.up, _state.SurfaceNormal);
            _visualRoot.rotation = Quaternion.Lerp(_visualRoot.rotation, targetRotation, _groundedAlignmentSpeed * Time.fixedDeltaTime);
        }

        private void TranslateTo(Vector2 position)
        {
            Vector2 delta = position - (Vector2)_cachedTransform.position;
            _cachedTransform.Translate(delta, Space.World);
        }
    }
}
