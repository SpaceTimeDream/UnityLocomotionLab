using Portfolio.Locomotion.Core;
using UnityEngine;
using UnityEngine.Serialization;

namespace Portfolio.Locomotion.Rigidbody
{
    /// <summary>
    /// Owns horizontal movement, slope traversal, floating movement, and movement speed limits.
    /// PlayerRigidbodyController supplies input and calls UpdateMovement during its fixed-step sequence.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerRigidbodyMovement : MonoBehaviour
    {
        //------------------------------- Game Pause -------------------------------//

        //------------------------------- Debugging Methods -------------------------------//

        //------------------------------- Parameters -------------------------------//

        #region Components

        [Header("MOVE PARAMETERS")]
        [FormerlySerializedAs("_maximumGroundSpeed")]
        [InspectorName("movementSpeed")]
        [SerializeField] private float _movementSpeed = 8f;
        [SerializeField] private float _moveSpeedClampModifier;
        [FormerlySerializedAs("_groundAcceleration")]
        [InspectorName("moveAcceleration")]
        [SerializeField] private float _moveAcceleration = 70f;
        [FormerlySerializedAs("_groundDeceleration")]
        [InspectorName("moveDeceleration")]
        [SerializeField] private float _moveDeceleration = 90f;
        [FormerlySerializedAs("_slopeAcceleration")]
        [SerializeField] private float _slopeMoveAcceleration = 70f;
        [FormerlySerializedAs("_slopeDeceleration")]
        [SerializeField] private float _slopeMoveDeceleration = 90f;
        [FormerlySerializedAs("_airAcceleration")]
        [SerializeField] private float _airMoveAcceleration = 38f;
        [FormerlySerializedAs("_airDeceleration")]
        [SerializeField] private float _airMoveDeceleration = 40f;
        [SerializeField] private float _steepSlopeSlideSpeed = 4f;
        [SerializeField] private float _slopeAdherenceSpeed = 1.5f;
        [SerializeField] private float _minimumInput = 0.01f;

        [Header("FLOATING MOVE PARAMETERS")]
        [SerializeField] private float _floatingMoveAcceleration = 55f;
        [SerializeField] private float _floatingMoveDeceleration = 30f;
        [SerializeField] private float _floatingMoveSpeedClamp = 18f;
        [SerializeField] private float _floatingSpeedClampModifier;
        [SerializeField] private Vector2 _floatingExternalVelocity;

        [Header("FACING PARAMETERS")]
        [SerializeField] private bool _flipVisualWithFacing = true;

        public float movementSpeed
        {
            get => _movementSpeed;
            set => _movementSpeed = Mathf.Max(0f, value);
        }

        public float moveAcceleration
        {
            get => _moveAcceleration;
            set => _moveAcceleration = Mathf.Max(0f, value);
        }

        public float moveDeceleration
        {
            get => _moveDeceleration;
            set => _moveDeceleration = Mathf.Max(0f, value);
        }

        public float moveSpeedClampModifier
        {
            get => _moveSpeedClampModifier;
            set => _moveSpeedClampModifier = Mathf.Max(0f, value);
        }

        public float slopeMoveAcceleration
        {
            get => _slopeMoveAcceleration;
            set => _slopeMoveAcceleration = Mathf.Max(0f, value);
        }

        public float slopeMoveDeceleration
        {
            get => _slopeMoveDeceleration;
            set => _slopeMoveDeceleration = Mathf.Max(0f, value);
        }

        public float airMoveAcceleration
        {
            get => _airMoveAcceleration;
            set => _airMoveAcceleration = Mathf.Max(0f, value);
        }

        public float airMoveDeceleration
        {
            get => _airMoveDeceleration;
            set => _airMoveDeceleration = Mathf.Max(0f, value);
        }

        public float steepSlopeSlideSpeed
        {
            get => _steepSlopeSlideSpeed;
            set => _steepSlopeSlideSpeed = Mathf.Max(0f, value);
        }

        public float slopeAdherenceSpeed
        {
            get => _slopeAdherenceSpeed;
            set => _slopeAdherenceSpeed = Mathf.Max(0f, value);
        }

        public float minimumInput
        {
            get => _minimumInput;
            set => _minimumInput = Mathf.Max(0.0001f, value);
        }

        public float floatingMoveAcceleration
        {
            get => _floatingMoveAcceleration;
            set => _floatingMoveAcceleration = Mathf.Max(0f, value);
        }

        public float floatingMoveDeceleration
        {
            get => _floatingMoveDeceleration;
            set => _floatingMoveDeceleration = Mathf.Max(0f, value);
        }

        public float floatingMoveSpeedClamp
        {
            get => _floatingMoveSpeedClamp;
            set => _floatingMoveSpeedClamp = Mathf.Max(0f, value);
        }

        public float floatingSpeedClampModifier
        {
            get => _floatingSpeedClampModifier;
            set => _floatingSpeedClampModifier = Mathf.Max(0f, value);
        }

        public Vector2 FloatingExternalVelocity => _floatingExternalVelocity;

        public bool FlipVisualWithFacing
        {
            get => _flipVisualWithFacing;
            set => _flipVisualWithFacing = value;
        }

        #endregion

        #region Flags

        private PlayerRigidbodyController _controller;
        private bool _isInitialized;
        #endregion

        //------------------------------- Initialization Methods -------------------------------//

        #region Initialization

        /// <summary>
        /// Binds the movement module to its coordinating PlayerRigidbodyController.
        /// </summary>
        public void Initialize(PlayerRigidbodyController controller)
        {
            if (_isInitialized && _controller == controller)
            {
                return;
            }

            _controller = controller;
            Validate();
            _isInitialized = _controller != null;
        }

        #endregion

        //------------------------------- Core / Routines Methods -------------------------------//

        #region Update

        /// <summary>
        /// Applies one movement step using the input prepared by PlayerRigidbodyController.
        /// </summary>
        public void UpdateMovement()
        {
            if (!_isInitialized || _controller == null || _controller.Body == null)
            {
                return;
            }

            if (PlayerRigidbodyStatus.HasMode(Mode.FloatingArea))
            {
                FloatingMove();
                return;
            }

            ApplyMovement(
                _controller.Body,
                _controller.State,
                _controller.SurfaceProbe,
                _controller.ExternalForces,
                _controller.MovementInput,
                _controller.SteepSlopeBlockDirection,
                _controller.PlayerRigidbodyJump.IsInJumpAscentState);
        }

        /// <summary>
        /// Applies regular ground, slope, steep-slope, or airborne movement.
        /// </summary>
        public void ApplyMovement(
            UnityEngine.Rigidbody2D body,
            LocomotionState state,
            PlayerRigidbodySurfaceProbe surfaceProbe,
            PlayerRigidbodyExternalForceReceiver externalForces,
            float movementInput,
            float steepSlopeBlockDirection,
            bool jumpAscentActive)
        {
            if (body == null || state == null || surfaceProbe == null || externalForces == null)
            {
                return;
            }

            if (!Mathf.Approximately(steepSlopeBlockDirection, 0f))
            {
                movementInput = RemoveInputIntoSlope(movementInput, steepSlopeBlockDirection);
                RemoveVelocityIntoSlope(body, steepSlopeBlockDirection);
            }

            if (externalForces.IsControlProtected)
            {
                return;
            }

            if (!state.ControlEnabled)
            {
                ApplyDeceleration(body, state.Grounded ? _moveDeceleration : _airMoveDeceleration);
                return;
            }

            float resistance = externalForces.Resistance;
            if (state.OnSteepSlope)
            {
                ApplySteepSlopeMovement(body, state, surfaceProbe, movementInput, resistance);
                return;
            }

            if (state.Grounded && state.OnWalkableSlope)
            {
                ApplySlopeMovement(body, state, surfaceProbe, movementInput, resistance);
                return;
            }

            if (state.Grounded)
            {
                ApplyHorizontalMovement(body, movementInput, _moveAcceleration, _moveDeceleration, resistance, true);
                return;
            }

            if (jumpAscentActive || !externalForces.HasExternalForce)
            {
                ApplyHorizontalMovement(body, movementInput, _airMoveAcceleration, _airMoveDeceleration, resistance, false);
            }
        }

        #endregion

        #region Horizontal Movement

        private void ApplyHorizontalMovement(
            UnityEngine.Rigidbody2D body,
            float movementInput,
            float acceleration,
            float deceleration,
            float resistance,
            bool grounded)
        {
            Vector2 velocity = body.linearVelocity;

            if (Mathf.Abs(movementInput) > _minimumInput)
            {
                float accelerationStep = movementInput * (acceleration / resistance) * Time.fixedDeltaTime;
                velocity.x = ClampHorizontalSpeed(velocity.x + accelerationStep);
            }
            else
            {
                velocity.x = Mathf.MoveTowards(velocity.x, 0f, deceleration * Time.fixedDeltaTime);
            }

            if (grounded && velocity.y > 0f)
            {
                velocity.y = 0f;
            }

            body.linearVelocity = velocity;
        }

        private void ApplyDeceleration(UnityEngine.Rigidbody2D body, float deceleration)
        {
            Vector2 velocity = body.linearVelocity;
            velocity.x = Mathf.MoveTowards(velocity.x, 0f, deceleration * Time.fixedDeltaTime);
            body.linearVelocity = velocity;
        }

        #endregion

        #region Slope Movement

        private void ApplySlopeMovement(
            UnityEngine.Rigidbody2D body,
            LocomotionState state,
            PlayerRigidbodySurfaceProbe surfaceProbe,
            float movementInput,
            float resistance)
        {
            Vector2 tangent = GetSlopeTangent(surfaceProbe, state.SurfaceNormal, movementInput);
            float acceleration = Mathf.Abs(movementInput) > _minimumInput
                ? _slopeMoveAcceleration
                : _slopeMoveDeceleration;
            float targetSpeed = Mathf.Abs(movementInput) > _minimumInput
                ? Mathf.Abs(ClampHorizontalSpeed(movementInput * _movementSpeed))
                : 0f;
            Vector2 targetVelocity = tangent * targetSpeed;

            if (Mathf.Abs(movementInput) <= _minimumInput)
            {
                targetVelocity = -state.SurfaceNormal * _slopeAdherenceSpeed;
            }
            else if (tangent.y < 0f && targetVelocity.y > 0f)
            {
                targetVelocity.y = 0f;
            }

            body.linearVelocity = Vector2.MoveTowards(
                body.linearVelocity,
                targetVelocity,
                (acceleration / resistance) * Time.fixedDeltaTime);
        }

        private void ApplySteepSlopeMovement(
            UnityEngine.Rigidbody2D body,
            LocomotionState state,
            PlayerRigidbodySurfaceProbe surfaceProbe,
            float movementInput,
            float resistance)
        {
            Vector2 downhillTangent = surfaceProbe.GetSlopeTangent(state.SurfaceNormal);
            if (downhillTangent.y > 0f)
            {
                downhillTangent *= -1f;
            }

            bool movingDownhill = Mathf.Abs(movementInput) > _minimumInput &&
                Mathf.Sign(movementInput) == Mathf.Sign(downhillTangent.x);
            float requestedSpeed = movingDownhill ? Mathf.Abs(movementInput) * _movementSpeed : 0f;
            float targetSpeed = Mathf.Max(requestedSpeed, _steepSlopeSlideSpeed);
            Vector2 targetVelocity = downhillTangent * targetSpeed;

            body.linearVelocity = Vector2.MoveTowards(
                body.linearVelocity,
                targetVelocity,
                (_slopeMoveAcceleration / resistance) * Time.fixedDeltaTime);
            state.SetSliding(targetSpeed > _minimumInput);
        }

        private Vector2 GetSlopeTangent(
            PlayerRigidbodySurfaceProbe surfaceProbe,
            Vector2 surfaceNormal,
            float movementInput)
        {
            Vector2 tangent = surfaceProbe.GetSlopeTangent(surfaceNormal);
            Vector2 desiredDirection = new Vector2(Mathf.Sign(movementInput), 0f);

            if (desiredDirection != Vector2.zero && Vector2.Dot(tangent, desiredDirection) < 0f)
            {
                tangent *= -1f;
            }

            return tangent;
        }

        #endregion

        #region Floating Movement

        private void FloatingMove()
        {
            if (_controller.ExternalForces.IsControlProtected)
            {
                return;
            }

            Vector2 moveInput = new Vector2(_controller.MovementInput, 0f);
            float acceleration = _floatingMoveAcceleration / _controller.ExternalForces.Resistance;
            Vector2 playerVelocity = _controller.Body.linearVelocity - _floatingExternalVelocity;
            playerVelocity += moveInput * acceleration * Time.fixedDeltaTime;

            if (Mathf.Abs(moveInput.x) <= _minimumInput)
            {
                playerVelocity = Vector2.MoveTowards(
                    playerVelocity,
                    Vector2.zero,
                    _floatingMoveDeceleration * Time.fixedDeltaTime);
            }

            _controller.Body.linearVelocity = ClampFloatingSpeed(playerVelocity + _floatingExternalVelocity);
        }

        private Vector2 ClampFloatingSpeed(Vector2 velocity)
        {
            float clampValue = Mathf.Abs(_floatingMoveSpeedClamp) + Mathf.Max(0f, _floatingSpeedClampModifier);
            velocity.x = Mathf.Clamp(velocity.x, -clampValue, clampValue);
            velocity.y = Mathf.Clamp(velocity.y, -clampValue, clampValue);
            return velocity;
        }

        /// <summary>
        /// Sets a velocity contribution preserved while floating movement is active.
        /// </summary>
        public void SetFloatingExternalVelocity(Vector2 velocity)
        {
            _floatingExternalVelocity = velocity;
        }

        /// <summary>
        /// Adds a temporary clamp to the floating movement speed.
        /// </summary>
        public void AddFloatingSpeedClamp(float value)
        {
            _floatingSpeedClampModifier = Mathf.Max(0f, _floatingSpeedClampModifier + value);
        }

        public void ResetFloatingRuntimeState()
        {
            _floatingExternalVelocity = Vector2.zero;
            _floatingSpeedClampModifier = 0f;
        }

        #endregion

        #region Speed Clamp

        /// <summary>
        /// Sets the active horizontal speed clamp for authored impulses.
        /// </summary>
        public void SetMoveSpeedClamp(float value)
        {
            _moveSpeedClampModifier = Mathf.Max(0f, value);
        }

        /// <summary>
        /// Adds an authored horizontal speed clamp without reducing an existing clamp.
        /// </summary>
        public void AddMoveSpeedClamp(float value)
        {
            _moveSpeedClampModifier = Mathf.Max(_moveSpeedClampModifier, Mathf.Abs(value));
        }

        public void ResetRuntimeState()
        {
            _moveSpeedClampModifier = 0f;
            ResetFloatingRuntimeState();
        }

        private float ClampHorizontalSpeed(float speed)
        {
            float maximumSpeed = Mathf.Max(_movementSpeed, _moveSpeedClampModifier);
            if (_controller != null && _controller.IsScriptedControlActive)
            {
                maximumSpeed *= Mathf.Clamp01(Mathf.Abs(_controller.MovementInput));
            }

            return Mathf.Clamp(speed, -maximumSpeed, maximumSpeed);
        }

        private float RemoveInputIntoSlope(float movementInput, float steepSlopeBlockDirection)
        {
            return movementInput * steepSlopeBlockDirection > _minimumInput ? 0f : movementInput;
        }

        private void RemoveVelocityIntoSlope(UnityEngine.Rigidbody2D body, float steepSlopeBlockDirection)
        {
            // Remove all horizontal momentum directed into the blocking slope.
            if (body.linearVelocity.x * steepSlopeBlockDirection <= 0f)
            {
                return;
            }

            Vector2 velocity = body.linearVelocity;
            velocity.x = 0f;
            body.linearVelocity = velocity;
        }

        #endregion

        #region Direction Helpers

        /// <summary>
        /// Resolves a nearby steep-slope direction that blocks movement.
        /// </summary>
        public float ResolveSteepSlopeBlockDirection(
            float movementInput,
            float facingDirection,
            UnityEngine.Rigidbody2D body,
            CapsuleCollider2D capsule,
            PlayerRigidbodySurfaceProbe surfaceProbe,
            float steepSlopeCheckDistance)
        {
            if (body == null || capsule == null || surfaceProbe == null)
            {
                return 0f;
            }

            float inputDirection = GetDirection(movementInput);
            if (HasNearbySteepSlope(surfaceProbe, capsule, inputDirection, steepSlopeCheckDistance))
            {
                return inputDirection;
            }

            float velocityDirection = GetDirection(body.linearVelocity.x);
            if (!Mathf.Approximately(velocityDirection, inputDirection) &&
                HasNearbySteepSlope(surfaceProbe, capsule, velocityDirection, steepSlopeCheckDistance))
            {
                return velocityDirection;
            }

            if (Mathf.Approximately(inputDirection, 0f) &&
                Mathf.Approximately(velocityDirection, 0f) &&
                HasNearbySteepSlope(surfaceProbe, capsule, facingDirection, steepSlopeCheckDistance))
            {
                return facingDirection;
            }

            return 0f;
        }

        /// <summary>
        /// Resolves facing direction from input and preserved external motion.
        /// </summary>
        public float ResolveFacingDirection(
            float movementInput,
            UnityEngine.Rigidbody2D body,
            PlayerRigidbodyExternalForceReceiver externalForces,
            float currentFacingDirection)
        {
            if (body == null)
            {
                return currentFacingDirection;
            }

            float direction = movementInput;
            if (externalForces != null && externalForces.IsControlProtected &&
                Mathf.Abs(body.linearVelocity.x) > 0.01f)
            {
                direction = body.linearVelocity.x;
            }

            return Mathf.Abs(direction) > 0.01f ? Mathf.Sign(direction) : currentFacingDirection;
        }

        private static bool HasNearbySteepSlope(
            PlayerRigidbodySurfaceProbe surfaceProbe,
            CapsuleCollider2D capsule,
            float direction,
            float distance)
        {
            return !Mathf.Approximately(direction, 0f) &&
                surfaceProbe.HasNearbySteepSlope(capsule, direction, distance);
        }

        private static float GetDirection(float value)
        {
            return Mathf.Abs(value) > 0.01f ? Mathf.Sign(value) : 0f;
        }

        #endregion

        //------------------------------- Editor -------------------------------//

        /// <summary>
        /// Clamps serialized movement tuning to safe runtime values.
        /// </summary>
        public void Validate()
        {
            _movementSpeed = Mathf.Max(0f, _movementSpeed);
            _moveAcceleration = Mathf.Max(0f, _moveAcceleration);
            _moveDeceleration = Mathf.Max(0f, _moveDeceleration);
            _slopeMoveAcceleration = Mathf.Max(0f, _slopeMoveAcceleration);
            _slopeMoveDeceleration = Mathf.Max(0f, _slopeMoveDeceleration);
            _airMoveAcceleration = Mathf.Max(0f, _airMoveAcceleration);
            _airMoveDeceleration = Mathf.Max(0f, _airMoveDeceleration);
            _steepSlopeSlideSpeed = Mathf.Max(0f, _steepSlopeSlideSpeed);
            _slopeAdherenceSpeed = Mathf.Max(0f, _slopeAdherenceSpeed);
            _minimumInput = Mathf.Max(0.0001f, _minimumInput);
            _floatingMoveAcceleration = Mathf.Max(0f, _floatingMoveAcceleration);
            _floatingMoveDeceleration = Mathf.Max(0f, _floatingMoveDeceleration);
            _floatingMoveSpeedClamp = Mathf.Max(0f, _floatingMoveSpeedClamp);
            _floatingSpeedClampModifier = Mathf.Max(0f, _floatingSpeedClampModifier);
            _moveSpeedClampModifier = Mathf.Max(0f, _moveSpeedClampModifier);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            Validate();
        }
#endif
    }
}
