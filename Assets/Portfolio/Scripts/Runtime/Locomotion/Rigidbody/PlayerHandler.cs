using Portfolio.Input;
using UnityEngine;

namespace Portfolio.Locomotion.Rigidbody
{
    /// <summary>
    /// Handles player-control input selection and pause/freeze mechanics. Public control
    /// methods on RigidbodyLocomotionController remain as compatibility entry points.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerHandler : MonoBehaviour
    {
        public float ResolveMovementInput(
            bool controlEnabled,
            bool movementFrozen,
            bool scriptedControlActive,
            Vector2 scriptedMoveInput,
            GameInput gameInput)
        {
            if (!controlEnabled || movementFrozen)
                return 0f;
            if (scriptedControlActive)
                return Mathf.Clamp(scriptedMoveInput.x, -1f, 1f);
            if (gameInput == null)
                return 0f;

            return Mathf.Clamp(gameInput.MovementValue.x, -1f, 1f);
        }

        public void Freeze(UnityEngine.Rigidbody2D body, float gravityScale, out Vector2 cachedVelocity, out float cachedGravityScale)
        {
            cachedVelocity = body != null ? body.linearVelocity : Vector2.zero;
            cachedGravityScale = gravityScale;

            if (body == null)
                return;

            body.linearVelocity = Vector2.zero;
            body.gravityScale = 0f;
        }

        public void Unfreeze(UnityEngine.Rigidbody2D body, Vector2 cachedVelocity, float cachedGravityScale)
        {
            if (body == null)
                return;

            body.linearVelocity = cachedVelocity;
            body.gravityScale = cachedGravityScale;
        }
    }
}
