using UnityEngine;
using UnityEngine.InputSystem;

public class RunnerInputSource : MonoBehaviour
{
    [Header("Touch")]
    [SerializeField, Tooltip("Floating joystick for touch movement. A quick tap on it can also jump.")]
    FloatingJoystick movementJoystick;
    [SerializeField, Tooltip("Optional dedicated on-screen jump button (works while the joystick is held).")]
    TouchDashButton jumpButton;
    [SerializeField, Tooltip("A quick tap on the joystick area triggers a jump.")]
    bool tapJoystickToJump = true;

    [Header("Desktop / Gamepad")]
    [SerializeField, Tooltip("WASD / arrows to move, Space to jump.")]
    bool enableKeyboardFallback = true;
    [SerializeField, Tooltip("Left stick / D-pad to move, South button (Cross on PS4) to jump.")]
    bool enableGamepad = true;

    [Header("General")]
    [SerializeField, Tooltip("Camera used to convert 2D input into camera-relative world movement.")]
    Camera movementCamera;
    [SerializeField, Tooltip("Minimum stick magnitude before movement input is treated as intentional.")]
    float deadZone = 0.1f;
    [Range(0f, 0.5f)] [SerializeField] float horizontalDeadZone = 0.1f;
    [Range(0f, 0.5f)] [SerializeField] float verticalDeadZone = 0.1f;
    [SerializeField, Tooltip("ON if your player controller reads CurrentFrame in FixedUpdate (keeps the press until a physics step has seen it). OFF if it reads in Update.")]
    bool jumpReadInFixedUpdate = true;

    public MovementInputFrame CurrentFrame { get; set; } = MovementInputFrame.Empty;
    public Camera MovementCamera => movementCamera;

    bool jumpQueued;
    bool fixedStepSinceQueued;

    public void Configure(FloatingJoystick joystick, TouchDashButton jump, Camera referenceCamera)
    {
        movementJoystick = joystick;
        jumpButton = jump;
        movementCamera = referenceCamera;
    }

    public void SetMovementCamera(Camera referenceCamera)
    {
        movementCamera = referenceCamera;
    }

    void FixedUpdate()
    {
        fixedStepSinceQueued = true;
    }

    void Update()
    {
        // Drop a jump once it has been visible to a physics step (or one frame, if read in Update).
        if (jumpQueued && (!jumpReadInFixedUpdate || fixedStepSinceQueued))
        {
            jumpQueued = false;
        }

        if (ReadJumpPressed())
        {
            jumpQueued = true;
            fixedStepSinceQueued = false;
        }

        // Movement: take whichever source is pushed hardest
        Vector2 move = new Vector2(ReadHorizontalInput(), ReadVerticalInput());
        move = Strongest(move, ReadKeyboardMove());
        move = Strongest(move, ReadGamepadMove());

        float magnitude = Mathf.Clamp01(move.magnitude);
        if (magnitude < deadZone)
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

        CurrentFrame = new MovementInputFrame(move, wishDirection, referenceForward, magnitude, jumpQueued);
    }

    // ---------- Jump ----------

    bool ReadJumpPressed()
    {
        bool pressed = false;

        // |= (not ||) so every source is consumed each frame
        if (tapJoystickToJump && movementJoystick != null)
            pressed |= movementJoystick.ConsumeTap();

        if (jumpButton != null)
            pressed |= jumpButton.ConsumePress();

        if (enableKeyboardFallback && Keyboard.current != null)
            pressed |= Keyboard.current.spaceKey.wasPressedThisFrame;

        if (enableGamepad && Gamepad.current != null)
            pressed |= Gamepad.current.buttonSouth.wasPressedThisFrame; // Cross on PS4

        return pressed;
    }

    // ---------- Movement sources ----------

    float ReadHorizontalInput()
    {
        if (movementJoystick == null) return 0f;
        float x = Mathf.Clamp(movementJoystick.Horizontal, -1f, 1f);
        return Mathf.Abs(x) < horizontalDeadZone ? 0f : x;
    }

    float ReadVerticalInput()
    {
        if (movementJoystick == null) return 0f;
        float y = Mathf.Clamp(movementJoystick.Vertical, -1f, 1f);
        return Mathf.Abs(y) < verticalDeadZone ? 0f : y;
    }

    Vector2 ReadKeyboardMove()
    {
        if (!enableKeyboardFallback || Keyboard.current == null) return Vector2.zero;

        Vector2 m = Vector2.zero;
        var k = Keyboard.current;

        if (k.aKey.isPressed || k.leftArrowKey.isPressed)  m.x -= 1f;
        if (k.dKey.isPressed || k.rightArrowKey.isPressed) m.x += 1f;
        if (k.sKey.isPressed || k.downArrowKey.isPressed)  m.y -= 1f;
        if (k.wKey.isPressed || k.upArrowKey.isPressed)    m.y += 1f;

        return Vector2.ClampMagnitude(m, 1f);
    }

    Vector2 ReadGamepadMove()
    {
        if (!enableGamepad || Gamepad.current == null) return Vector2.zero;

        // Left stick or D-pad, whichever is pushed harder
        Vector2 stick = Gamepad.current.leftStick.ReadValue();
        Vector2 dpad = Gamepad.current.dpad.ReadValue();
        return Strongest(stick, dpad);
    }

    static Vector2 Strongest(Vector2 a, Vector2 b)
    {
        return b.sqrMagnitude > a.sqrMagnitude ? b : a;
    }

    // ---------- Camera-relative direction ----------

    Vector3 ResolveWishDirection(Vector2 move, Vector3 forward, Vector3 right)
    {
        if (move.sqrMagnitude <= 0f) return Vector3.zero;

        Vector3 wish = (forward * move.y) + (right * move.x);
        return wish.sqrMagnitude <= 0.0001f ? Vector3.zero : wish.normalized;
    }

    Vector3 ResolveReferenceForward()
    {
        Vector3 forward = movementCamera != null ? movementCamera.transform.forward : Vector3.forward;
        Vector3 flattened = Vector3.ProjectOnPlane(forward, Vector3.up);
        return flattened.sqrMagnitude <= 0.0001f ? Vector3.forward : flattened.normalized;
    }

    Vector3 ResolveReferenceRight(Vector3 referenceForward)
    {
        Vector3 right = Vector3.Cross(Vector3.up, referenceForward);
        return right.sqrMagnitude <= 0.0001f ? Vector3.right : right.normalized;
    }
}