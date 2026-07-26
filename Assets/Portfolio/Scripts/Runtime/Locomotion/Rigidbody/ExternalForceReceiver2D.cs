using UnityEngine;

namespace Portfolio.Locomotion.Rigidbody
{
    [System.Serializable]
    public class ExternalForceReceiver2D
    {
        // Fields
        [SerializeField] private float _horizontalProtectionTime = 0.12f;

        private float _protectedUntilTime;

        // Properties
        public bool IsHorizontalProtected => Time.time < _protectedUntilTime;

        // Public Methods
        public void AddImpulse(UnityEngine.Rigidbody2D body, Vector2 impulse, bool protectHorizontalMomentum = true)
        {
            body.AddForce(impulse, ForceMode2D.Impulse);
            ProtectHorizontalMomentum(protectHorizontalMomentum);
        }

        public void AddVelocity(UnityEngine.Rigidbody2D body, Vector2 velocity, bool protectHorizontalMomentum = true)
        {
            body.linearVelocity += velocity;
            ProtectHorizontalMomentum(protectHorizontalMomentum);
        }

        public void SetVelocity(UnityEngine.Rigidbody2D body, Vector2 velocity, bool protectHorizontalMomentum = true)
        {
            body.linearVelocity = velocity;
            ProtectHorizontalMomentum(protectHorizontalMomentum);
        }

        public void ClearRuntimeState()
        {
            _protectedUntilTime = 0f;
        }

        public void Validate()
        {
            _horizontalProtectionTime = Mathf.Max(0f, _horizontalProtectionTime);
        }

        // Private Methods
        private void ProtectHorizontalMomentum(bool protectHorizontalMomentum)
        {
            if (!protectHorizontalMomentum)
            {
                return;
            }

            _protectedUntilTime = Time.time + _horizontalProtectionTime;
        }
    }
}
