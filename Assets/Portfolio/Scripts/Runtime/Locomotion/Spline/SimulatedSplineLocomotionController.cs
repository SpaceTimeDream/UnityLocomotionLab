using UnityEngine;

namespace Portfolio.Locomotion.Spline
{
    public class SimulatedSplineLocomotionController : MonoBehaviour
    {
        //------------------------------- Game Pause -------------------------------//

        //------------------------------- Debugging Methods -------------------------------//

        //------------------------------- Parameters -------------------------------//

        #region Components
        [SerializeField] private Transform _visualRoot;
        [SerializeField] private SimulatedSplineMovement _movement;
        [SerializeField] private SimulatedSplineJump _jump;
        [SerializeField] private SplineGroundConstraint2D _groundConstraint;
        [SerializeField] private SimulatedCollisionResolver2D _collisionResolver;
        [SerializeField] private SplineLocomotionState _state = new SplineLocomotionState();
        [SerializeField] private float _demoResetY = -8f;
        [SerializeField] private float _groundedAlignmentSpeed = 18f;
        [SerializeField] private bool _constrainZRotation;

        private Transform _cachedTransform;
        private Vector2 _movementInput;
        private Vector2 _spawnPosition;
        private bool _initialized;
        private float _startingVisualZRotation;

        // Properties
        public SplineLocomotionState State => _state;

        #endregion

        #region Flags

        #endregion

        //------------------------------- Unity Methods -------------------------------//

        #region Unity
        private void Start()
        {
            Initialize();
        }

        private void FixedUpdate()
        {
            SimulateFixedStep();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        #endregion

        //------------------------------- Initialization Methods -------------------------------//

        #region Initialization

        private void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            InitializeModules();
            InitializeComponents();
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

            if (_visualRoot != null)
            {
                _startingVisualZRotation = _visualRoot.eulerAngles.z;
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

            if (_collisionResolver == null)
            {
                _collisionResolver = GetComponent<SimulatedCollisionResolver2D>();
            }
        }

        private void InitializeEvents()
        {
        }

        #endregion

        //------------------------------- Core / Routines Methods -------------------------------//

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

        private void SimulateFixedStep()
        {
            if (!_initialized || _movement == null || _jump == null || _groundConstraint == null || _collisionResolver == null || !_groundConstraint.IsValid())
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
            _groundConstraint.ConstrainAtDistance(_state, _cachedTransform, _movement.WalkableSlopeAngle, _collisionResolver);
            AlignVisual(false);
        }

        private void SimulateAirborne(float horizontalInput)
        {
            Vector2 previousPosition = _cachedTransform.position;
            _movement.SimulateAirborne(_state, horizontalInput, Time.fixedDeltaTime);
            _jump.ApplyEarlyRelease(_state);
            Vector2 motion = _state.AirborneVelocity * Time.fixedDeltaTime;
            Vector2 impactVelocity = _state.AirborneVelocity;
            Vector2 resolvedMotion = _collisionResolver.Resolve(previousPosition, motion, ref _state.AirborneVelocity,
                _movement.WalkableSlopeAngle, false, out RaycastHit2D blockingHit);
            _state.Rising = _state.AirborneVelocity.y > 0f;
            _state.Falling = _state.AirborneVelocity.y < 0f;

            if (blockingHit.collider != null && blockingHit.normal.y > 0.01f &&
                _groundConstraint.TryLand(_state, blockingHit, impactVelocity, _movement.WalkableSlopeAngle))
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

            _cachedTransform.Translate(resolvedMotion, Space.World);
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

            if (_constrainZRotation)
            {
                _visualRoot.rotation = Quaternion.Euler(0f, 0f, _startingVisualZRotation);
                return;
            }

            Quaternion targetRotation = GetTargetVisualRotation(upright);
            _visualRoot.rotation = Quaternion.Lerp(_visualRoot.rotation, targetRotation, _groundedAlignmentSpeed * Time.fixedDeltaTime);
        }

        private Quaternion GetTargetVisualRotation(bool upright)
        {
            return upright ? Quaternion.identity : Quaternion.FromToRotation(Vector3.up, _state.SurfaceNormal);
        }

        private void TranslateTo(Vector2 position)
        {
            Vector2 delta = position - (Vector2)_cachedTransform.position;
            _cachedTransform.Translate(delta, Space.World);
        }

        private void UnsubscribeEvents()
        {
        }

#if UNITY_EDITOR
        //------------------------------- Editor -------------------------------//
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

            if (_collisionResolver == null)
            {
                _collisionResolver = GetComponent<SimulatedCollisionResolver2D>();
            }
        }
#endif
    }
}
