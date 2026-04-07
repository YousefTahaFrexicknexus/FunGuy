using UnityEngine;
using UnityEngine.InputSystem;

namespace Funguy.IdkPlatformer
{
    public sealed class InputHandler : MonoBehaviour
    {
        [SerializeField] private FloatingJoystick movementJoystick;
        [SerializeField] private TouchDashButton dashButton;
        [SerializeField] private Camera movementCamera;
        [SerializeField] private bool useMainCameraFallback = true;
        [SerializeField] private bool enableKeyboardFallback = true;
        [SerializeField] private float deadZone = 0.1f;

        public MovementInputFrame CurrentFrame { get; private set; } = MovementInputFrame.Empty;

        private void Update()
        {
            Vector2 move = movementJoystick != null ? movementJoystick.Value : Vector2.zero;

            if (enableKeyboardFallback)
            {
                move = ResolveKeyboardMove(move);
            }

            float magnitude = Mathf.Clamp01(move.magnitude);
            if (magnitude <= deadZone)
            {
                move = Vector2.zero;
                magnitude = 0f;
            }
            else
            {
                move = move.normalized * magnitude;
            }

            Vector3 referenceForward = ResolveReferenceForward();
            Vector3 referenceRight = ResolveReferenceRight(referenceForward);
            Vector3 wishDirection = ResolveWishDirection(move, referenceForward, referenceRight);
            bool dashPressed = (dashButton != null && dashButton.ConsumePress()) || ResolveKeyboardDash();

            CurrentFrame = new MovementInputFrame(move, wishDirection, referenceForward, magnitude, dashPressed);
        }

        private Vector2 ResolveKeyboardMove(Vector2 currentMove)
        {
            if (Keyboard.current == null)
            {
                return currentMove;
            }

            Vector2 keyboardMove = Vector2.zero;

            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            {
                keyboardMove.x -= 1f;
            }

            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            {
                keyboardMove.x += 1f;
            }

            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
            {
                keyboardMove.y -= 1f;
            }

            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
            {
                keyboardMove.y += 1f;
            }

            if (keyboardMove.sqrMagnitude <= 0f)
            {
                return currentMove;
            }

            keyboardMove = Vector2.ClampMagnitude(keyboardMove, 1f);
            return keyboardMove.magnitude > currentMove.magnitude ? keyboardMove : currentMove;
        }

        private bool ResolveKeyboardDash()
        {
            if (!enableKeyboardFallback || Keyboard.current == null)
            {
                return false;
            }

            return Keyboard.current.spaceKey.wasPressedThisFrame
                || Keyboard.current.leftShiftKey.wasPressedThisFrame
                || Keyboard.current.rightShiftKey.wasPressedThisFrame;
        }

        private Vector3 ResolveWishDirection(Vector2 move, Vector3 referenceForward, Vector3 referenceRight)
        {
            if (move.sqrMagnitude <= 0f)
            {
                return Vector3.zero;
            }

            Vector3 wishDirection = (referenceForward * move.y) + (referenceRight * move.x);
            if (wishDirection.sqrMagnitude <= 0.0001f)
            {
                return Vector3.zero;
            }

            return wishDirection.normalized;
        }

        private Vector3 ResolveReferenceForward()
        {
            Camera currentCamera = movementCamera;
            if (currentCamera == null && useMainCameraFallback)
            {
                currentCamera = Camera.main;
            }

            Vector3 forward = currentCamera != null ? currentCamera.transform.forward : Vector3.forward;
            Vector3 flattened = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (flattened.sqrMagnitude <= 0.0001f)
            {
                return Vector3.forward;
            }

            return flattened.normalized;
        }

        private Vector3 ResolveReferenceRight(Vector3 referenceForward)
        {
            Vector3 right = Vector3.Cross(Vector3.up, referenceForward);
            if (right.sqrMagnitude <= 0.0001f)
            {
                return Vector3.right;
            }

            return right.normalized;
        }
    }
}
