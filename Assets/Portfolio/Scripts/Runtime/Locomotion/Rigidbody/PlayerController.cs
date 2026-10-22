using UnityEngine;

namespace Portfolio.Locomotion.Rigidbody
{
    /// <summary>
    /// Lightweight player-facing facade for systems that want a single controller reference.
    /// RigidbodyLocomotionController remains on existing scene objects to preserve its script
    /// GUID and serialized configuration while coordinating the specialized components.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RigidbodyLocomotionController))]
    public sealed class PlayerController : MonoBehaviour
    {
        private RigidbodyLocomotionController _controller;

        public RigidbodyLocomotionController Locomotion => _controller;
        public PlayerHandler Handler { get; private set; }
        public PlayerJump Jump { get; private set; }
        public PlayerMovement Movement { get; private set; }
        public PlayerPhysics Physics { get; private set; }
        public PlayerStatus Status { get; private set; }

        private void Awake()
        {
            _controller = GetComponent<RigidbodyLocomotionController>();
            Handler = GetComponent<PlayerHandler>();
            Jump = GetComponent<PlayerJump>();
            Movement = GetComponent<PlayerMovement>();
            Physics = GetComponent<PlayerPhysics>();
            Status = GetComponent<PlayerStatus>();
        }
    }
}
