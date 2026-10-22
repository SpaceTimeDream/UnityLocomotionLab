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
        private bool _movementFrozen;
        private bool _scriptedControlActive;
        private Vector2 _scriptedMoveInput;
        private Vector2 _cachedPausedVelocity;
        private float _cachedPausedGravityScale;

        public bool IsMovementFrozen => _movementFrozen;
        public bool IsScriptedControlActive => _scriptedControlActive;
        public Vector2 ScriptedMoveInput => _scriptedMoveInput;

        public void BeginScriptedControl()
        {
            _scriptedMoveInput = Vector2.zero;
            _scriptedControlActive = true;
        }

        public void SetScriptedMoveInput(Vector2 input)
        {
            if (_scriptedControlActive)
                _scriptedMoveInput = Vector2.ClampMagnitude(input, 1f);
        }

        public void EndScriptedControl()
        {
            _scriptedMoveInput = Vector2.zero;
            _scriptedControlActive = false;
        }

        public float ResolveMovementInput(
            bool controlEnabled,
            GameInput gameInput)
        {
            if (!controlEnabled || _movementFrozen)
                return 0f;
            if (_scriptedControlActive)
                return Mathf.Clamp(_scriptedMoveInput.x, -1f, 1f);
            if (gameInput == null)
                return 0f;

            return Mathf.Clamp(gameInput.MovementValue.x, -1f, 1f);
        }

        public void Freeze(UnityEngine.Rigidbody2D body)
        {
            if (_movementFrozen || body == null)
                return;

            _movementFrozen = true;
            _cachedPausedVelocity = body.linearVelocity;
            _cachedPausedGravityScale = body.gravityScale;
            body.linearVelocity = Vector2.zero;
            body.gravityScale = 0f;
        }

        public void Unfreeze(UnityEngine.Rigidbody2D body)
        {
            if (!_movementFrozen || body == null)
                return;

            _movementFrozen = false;
            body.linearVelocity = _cachedPausedVelocity;
            body.gravityScale = _cachedPausedGravityScale;
        }

        public void ResetRuntimeState()
        {
            _scriptedMoveInput = Vector2.zero;
            _scriptedControlActive = false;
        }
    }
}
