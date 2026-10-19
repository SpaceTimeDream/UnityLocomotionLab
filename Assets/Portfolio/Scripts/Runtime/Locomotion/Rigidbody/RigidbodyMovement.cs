using Portfolio.Locomotion.Core;
using UnityEngine;

namespace Portfolio.Locomotion.Rigidbody
{
    [System.Serializable]
    public class RigidbodyMovement
    {
        //------------------------------- Game Pause -------------------------------//

        //------------------------------- Debugging Methods -------------------------------//

        //------------------------------- Parameters -------------------------------//

        #region Components

        [Header("MOVE PARAMETERS")]
        [SerializeField] private float _maximumGroundSpeed = 8f;
        [SerializeField] private float _moveSpeedClampModifier;
        [SerializeField] private float _groundAcceleration = 70f;
        [SerializeField] private float _groundDeceleration = 90f;
        [SerializeField] private float _slopeAcceleration = 70f;
        [SerializeField] private float _slopeDeceleration = 90f;
        [SerializeField] private float _airAcceleration = 38f;
        [SerializeField] private float _airDeceleration = 40f;
        [SerializeField] private float _steepSlopeSlideSpeed = 4f;
        [SerializeField] private float _slopeAdherenceSpeed = 1.5f;
        [SerializeField] private float _minimumInput = 0.01f;

        public float MaximumGroundSpeed => _maximumGroundSpeed;
        public float MoveSpeedClampModifier => _moveSpeedClampModifier;
        public float GroundAcceleration => _groundAcceleration;
        public float GroundDeceleration => _groundDeceleration;
        public float SlopeAcceleration => _slopeAcceleration;
        public float SlopeDeceleration => _slopeDeceleration;
        public float AirAcceleration => _airAcceleration;
        public float AirDeceleration => _airDeceleration;

        #endregion

        #region Flags

        #endregion

        //------------------------------- Unity Methods -------------------------------//

        #region Unity

        #endregion

        //------------------------------- Initialization Methods -------------------------------//

        #region Initialization

        #endregion

        //------------------------------- Core / Routines Methods -------------------------------//

        public void ApplyMovement(
            UnityEngine.Rigidbody2D body,
            LocomotionState state,
            SurfaceProbe2D surfaceProbe,
            ExternalForceReceiver2D externalForces,
            float movementInput,
            float steepSlopeBlockDirection,
            bool jumpAscentActive)
        {
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
                ApplyDeceleration(body, state.Grounded ? _groundDeceleration : _airDeceleration);
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
                ApplyHorizontalMovement(body, movementInput, _groundAcceleration, _groundDeceleration, resistance, true);
                return;
            }

            if (jumpAscentActive || !externalForces.HasExternalForce)
            {
                ApplyHorizontalMovement(body, movementInput, _airAcceleration, _airDeceleration, resistance, false);
            }
        }

        public void SetMoveSpeedClamp(float value)
        {
            _moveSpeedClampModifier = Mathf.Max(0f, value);
        }

        public void AddMoveSpeedClamp(float value)
        {
            _moveSpeedClampModifier = Mathf.Max(_moveSpeedClampModifier, Mathf.Abs(value));
        }

        public void ResetRuntimeState()
        {
            _moveSpeedClampModifier = 0f;
        }

        public void Validate()
        {
            _maximumGroundSpeed = Mathf.Max(0f, _maximumGroundSpeed);
            _moveSpeedClampModifier = Mathf.Max(0f, _moveSpeedClampModifier);
            _groundAcceleration = Mathf.Max(0f, _groundAcceleration);
            _groundDeceleration = Mathf.Max(0f, _groundDeceleration);
            _slopeAcceleration = Mathf.Max(0f, _slopeAcceleration);
            _slopeDeceleration = Mathf.Max(0f, _slopeDeceleration);
            _airAcceleration = Mathf.Max(0f, _airAcceleration);
            _airDeceleration = Mathf.Max(0f, _airDeceleration);
            _steepSlopeSlideSpeed = Mathf.Max(0f, _steepSlopeSlideSpeed);
            _slopeAdherenceSpeed = Mathf.Max(0f, _slopeAdherenceSpeed);
            _minimumInput = Mathf.Max(0.0001f, _minimumInput);
        }

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

        private void ApplySlopeMovement(
            UnityEngine.Rigidbody2D body,
            LocomotionState state,
            SurfaceProbe2D surfaceProbe,
            float movementInput,
            float resistance)
        {
            Vector2 tangent = GetSlopeTangent(surfaceProbe, state.SurfaceNormal, movementInput);
            float acceleration = Mathf.Abs(movementInput) > _minimumInput ? _slopeAcceleration : _slopeDeceleration;
            float targetSpeed = Mathf.Abs(movementInput) > _minimumInput
                ? Mathf.Abs(ClampHorizontalSpeed(movementInput * _maximumGroundSpeed))
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
            SurfaceProbe2D surfaceProbe,
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
            float requestedSpeed = movingDownhill ? Mathf.Abs(movementInput) * _maximumGroundSpeed : 0f;
            float targetSpeed = Mathf.Max(requestedSpeed, _steepSlopeSlideSpeed);
            Vector2 targetVelocity = downhillTangent * targetSpeed;

            body.linearVelocity = Vector2.MoveTowards(
                body.linearVelocity,
                targetVelocity,
                (_slopeAcceleration / resistance) * Time.fixedDeltaTime);
            state.SetSliding(targetSpeed > _minimumInput);
        }

        private void ApplyDeceleration(UnityEngine.Rigidbody2D body, float deceleration)
        {
            Vector2 velocity = body.linearVelocity;
            velocity.x = Mathf.MoveTowards(velocity.x, 0f, deceleration * Time.fixedDeltaTime);
            body.linearVelocity = velocity;
        }

        private Vector2 GetSlopeTangent(SurfaceProbe2D surfaceProbe, Vector2 surfaceNormal, float movementInput)
        {
            Vector2 tangent = surfaceProbe.GetSlopeTangent(surfaceNormal);
            Vector2 desiredDirection = new Vector2(Mathf.Sign(movementInput), 0f);

            if (desiredDirection != Vector2.zero && Vector2.Dot(tangent, desiredDirection) < 0f)
            {
                tangent *= -1f;
            }

            return tangent;
        }

        private float ClampHorizontalSpeed(float speed)
        {
            float maximumSpeed = Mathf.Max(_maximumGroundSpeed, _moveSpeedClampModifier);
            return Mathf.Clamp(speed, -maximumSpeed, maximumSpeed);
        }

        private float RemoveInputIntoSlope(float movementInput, float steepSlopeBlockDirection)
        {
            return movementInput * steepSlopeBlockDirection > _minimumInput
                ? 0f
                : movementInput;
        }

        private void RemoveVelocityIntoSlope(
            UnityEngine.Rigidbody2D body,
            float steepSlopeBlockDirection)
        {
            if (body.linearVelocity.x * steepSlopeBlockDirection <= _minimumInput)
            {
                return;
            }

            Vector2 velocity = body.linearVelocity;
            velocity.x = 0f;
            body.linearVelocity = velocity;
        }

#if UNITY_EDITOR
        //------------------------------- Editor -------------------------------//
#endif
    }
}
