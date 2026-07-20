using System;
using UnityEngine;

namespace Portfolio.Locomotion.Spline
{
    [Serializable]
    public class SplineLocomotionState
    {
        // Fields
        [SerializeField] private bool _controlEnabled = true;
        [SerializeField] private bool _grounded;
        [SerializeField] private bool _rising;
        [SerializeField] private bool _falling;
        [SerializeField] private bool _walkableSlope = true;
        [SerializeField] private bool _steepSlope;
        [SerializeField] private float _pathDistance;
        [SerializeField] private float _tangentialGroundSpeed;
        [SerializeField] private Vector2 _airborneVelocity;
        [SerializeField] private Vector2 _anchorPosition;
        [SerializeField] private Vector2 _surfacePoint;
        [SerializeField] private Vector2 _surfaceTangent = Vector2.right;
        [SerializeField] private Vector2 _surfaceNormal = Vector2.up;
        [SerializeField] private float _signedSlopeAngle;

        // Properties
        public bool ControlEnabled { get => _controlEnabled; set => _controlEnabled = value; }
        public bool Grounded { get => _grounded; set => _grounded = value; }
        public bool Rising { get => _rising; set => _rising = value; }
        public bool Falling { get => _falling; set => _falling = value; }
        public bool WalkableSlope { get => _walkableSlope; set => _walkableSlope = value; }
        public bool SteepSlope { get => _steepSlope; set => _steepSlope = value; }
        public float PathDistance { get => _pathDistance; set => _pathDistance = value; }
        public float TangentialGroundSpeed { get => _tangentialGroundSpeed; set => _tangentialGroundSpeed = value; }
        public Vector2 AirborneVelocity { get => _airborneVelocity; set => _airborneVelocity = value; }
        public Vector2 AnchorPosition { get => _anchorPosition; set => _anchorPosition = value; }
        public Vector2 SurfacePoint { get => _surfacePoint; set => _surfacePoint = value; }
        public Vector2 SurfaceTangent { get => _surfaceTangent; set => _surfaceTangent = value; }
        public Vector2 SurfaceNormal { get => _surfaceNormal; set => _surfaceNormal = value; }
        public float SignedSlopeAngle { get => _signedSlopeAngle; set => _signedSlopeAngle = value; }

        // Public Methods
        public void Reset(Vector2 anchorPosition)
        {
            _controlEnabled = true;
            _grounded = false;
            _rising = false;
            _falling = false;
            _walkableSlope = true;
            _steepSlope = false;
            _pathDistance = 0f;
            _tangentialGroundSpeed = 0f;
            _airborneVelocity = Vector2.zero;
            _anchorPosition = anchorPosition;
            _surfacePoint = anchorPosition;
            _surfaceTangent = Vector2.right;
            _surfaceNormal = Vector2.up;
            _signedSlopeAngle = 0f;
        }
    }
}
