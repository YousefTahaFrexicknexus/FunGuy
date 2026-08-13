using UnityEngine;
using UnityEngine.InputSystem;

public class RunnerInputSource : MonoBehaviour
{
    [SerializeField, Tooltip("Primary movement joystick used on touch devices.")]
    FloatingJoystick movementJoystick;
    [SerializeField, Tooltip("Touch button that triggers a dash press for one frame when consumed.")]
    TouchDashButton dashButton;
    [SerializeField, Tooltip("Camera used to convert 2D input into camera-relative world movement.")]
    Camera movementCamera;
    [SerializeField, Tooltip("If enabled, WASD / arrows and Space / Shift also drive the runner in editor and desktop builds.")]
    bool enableKeyboardFallback = true;
    [SerializeField, Tooltip("Minimum stick magnitude before movement input is treated as intentional.")]
    float deadZone = 0.1f;

    [Tooltip("Ignore tiny joystick movement around the center.")]
    [Range(0f, 0.5f)] [SerializeField] float horizontalDeadZone = 0.1f;

    [Tooltip("Ignore tiny joystick movement around the center.")]
    [Range(0f, 0.5f)] [SerializeField] float verticalDeadZone = 0.1f;

    public MovementInputFrame CurrentFrame { get; set; } = MovementInputFrame.Empty;
    public Camera MovementCamera => movementCamera;

    public void Configure(FloatingJoystick joystick, TouchDashButton dash, Camera referenceCamera)
    {
        movementJoystick = joystick;
        dashButton = dash;
        movementCamera = referenceCamera;
    }

    public void SetMovementCamera(Camera referenceCamera)
    {
        movementCamera = referenceCamera;
    }

    void Update()
    {
        Vector2 move = new Vector2(ReadHorizontalInput(), ReadVerticalInput());

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

    float ReadHorizontalInput()
    {
        if (movementJoystick == null)
        {
            return 0f;
        }

        float inputX = Mathf.Clamp(movementJoystick.Horizontal, -1f, 1f);
        return Mathf.Abs(inputX) < horizontalDeadZone ? 0f : inputX;
    }

    float ReadVerticalInput()
    {
        if (movementJoystick == null)
        {
            return 0f;
        }

        float inputY = Mathf.Clamp(movementJoystick.Vertical, -1f, 1f);
        return Mathf.Abs(inputY) < verticalDeadZone ? 0f : inputY;
    }

    Vector2 ResolveKeyboardMove(Vector2 currentMove)
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

    bool ResolveKeyboardDash()
    {
        if (!enableKeyboardFallback || Keyboard.current == null)
        {
            return false;
        }

        return Keyboard.current.spaceKey.wasPressedThisFrame
            || Keyboard.current.leftShiftKey.wasPressedThisFrame
            || Keyboard.current.rightShiftKey.wasPressedThisFrame;
    }

    Vector3 ResolveWishDirection(Vector2 move, Vector3 referenceForward, Vector3 referenceRight)
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

    Vector3 ResolveReferenceForward()
    {
        Vector3 forward = movementCamera != null ? movementCamera.transform.forward : Vector3.forward;
        Vector3 flattened = Vector3.ProjectOnPlane(forward, Vector3.up);
        
        if (flattened.sqrMagnitude <= 0.0001f)
        {
            return Vector3.forward;
        }

        return flattened.normalized;
    }

    Vector3 ResolveReferenceRight(Vector3 referenceForward)
    {
        Vector3 right = Vector3.Cross(Vector3.up, referenceForward);
        if (right.sqrMagnitude <= 0.0001f)
        {
            return Vector3.right;
        }

        return right.normalized;
    }
}