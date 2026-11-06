using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Drag down on screen (or with the mouse) to pull the character back,
/// release to fling him forward. Attach to the character that has the Rigidbody.
/// The rubber bands are regular mesh objects that get stretched between the posts and the character.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class SlingshotLauncher : MonoBehaviour
{
    public enum LengthAxis { X, Y, Z }

    [Header("Direction")]
    [SerializeField] private Vector3 launchDirection = Vector3.forward;

    [Header("Pull")]
    [Tooltip("How far back (world units) the character moves at full pull.")]
    [SerializeField] private float maxPullDistance = 3f;
    [Tooltip("How many screen pixels of dragging equal a full pull.")]
    [SerializeField] private float maxDragPixels = 300f;
    [Tooltip("Pull below this (0-1) cancels the launch and snaps back.")]
    [SerializeField, Range(0f, 0.5f)] private float minPullToLaunch = 0.1f;
    [SerializeField] private float returnSpeed = 12f;

    [Header("Launch")]
    [SerializeField] private float minLaunchSpeed = 15f;
    [SerializeField] private float maxLaunchSpeed = 40f;
    [SerializeField] private AnimationCurve powerCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Band meshes (optional)")]
    [Tooltip("The two posts the bands are attached to.")]
    [SerializeField] private Transform leftAnchor;
    [SerializeField] private Transform rightAnchor;
    [Tooltip("Mesh objects (with a MeshFilter) that get stretched between each post and the pouch. " +
             "Don't parent them under a scaled object.")]
    [SerializeField] private Transform leftBand;
    [SerializeField] private Transform rightBand;
    [Tooltip("Where the bands attach on the character (e.g. an empty child at his back). " +
             "Falls back to the character's pivot.")]
    [SerializeField] private Transform pouchPoint;
    [Tooltip("The axis your band mesh is long on. Unity's Cylinder/Capsule = Y, a Cube or custom model modeled along Z = Z.")]
    [SerializeField] private LengthAxis lengthAxis = LengthAxis.Y;

    [Header("Runner hookup")]
    [Tooltip("Your normal run/steer scripts. Disabled until the launch happens.")]
    [SerializeField] private Behaviour[] disableUntilLaunch;
    [Tooltip("Invoked on release with the launch speed (use for sound, camera shake, etc.).")]
    public UnityEvent<float> onLaunched;

    private enum State { Idle, Aiming, Returning, Launched }

    private Rigidbody rb;
    private Vector3 startPosition;
    private Vector3 dir;
    private State state = State.Idle;
    private float dragStartY;
    private float pull;

    private Vector3 leftBaseScale, rightBaseScale;
    private float leftMeshLength = 1f, rightMeshLength = 1f;

    private Vector3 PouchPosition => pouchPoint != null ? pouchPoint.position : transform.position;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        startPosition = transform.position;
        dir = launchDirection.sqrMagnitude > 0f ? launchDirection.normalized : transform.forward;

        rb.isKinematic = true;               // hold him still while aiming
        SetRunnerEnabled(false);

        CacheBand(leftBand, out leftBaseScale, out leftMeshLength);
        CacheBand(rightBand, out rightBaseScale, out rightMeshLength);
    }

    private void Start()
    {
        UpdateBands();   // show the bands at rest before the first drag
    }

    private void Update()
    {
        if (state == State.Launched) return;

        ReadPointer(out Vector2 pos, out bool down, out bool up);

        if (down)
        {
            dragStartY = pos.y;
            state = State.Aiming;
        }

        if (state == State.Aiming)
        {
            // Dragging downward = pulling back
            pull = Mathf.Clamp01((dragStartY - pos.y) / maxDragPixels);
            transform.position = startPosition - dir * (pull * maxPullDistance);
            if (up) Release();
        }
        else if (state == State.Returning)
        {
            transform.position = Vector3.MoveTowards(transform.position, startPosition, returnSpeed * Time.deltaTime);
            if ((transform.position - startPosition).sqrMagnitude < 0.0001f) state = State.Idle;
        }

        if (state != State.Launched) UpdateBands();
    }

    private void Release()
    {
        if (pull < minPullToLaunch)
        {
            state = State.Returning;
            return;
        }

        float speed = Mathf.Lerp(minLaunchSpeed, maxLaunchSpeed, powerCurve.Evaluate(pull));

        rb.isKinematic = false;
        rb.AddForce(dir * speed, ForceMode.VelocityChange);   // instant speed, ignores mass

        SetRunnerEnabled(true);
        SetBandsActive(false);
        state = State.Launched;
        onLaunched?.Invoke(speed);
    }

    // ---------- Band mesh handling ----------

    private void CacheBand(Transform band, out Vector3 baseScale, out float meshLength)
    {
        baseScale = Vector3.one;
        meshLength = 1f;
        if (band == null) return;

        baseScale = band.localScale;
        var mf = band.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
            meshLength = Mathf.Max(0.0001f, mf.sharedMesh.bounds.size[(int)lengthAxis]);
    }

    private void UpdateBands()
    {
        Vector3 pouch = PouchPosition;
        if (leftBand != null && leftAnchor != null)
            StretchBand(leftBand, leftAnchor.position, pouch, leftBaseScale, leftMeshLength);
        if (rightBand != null && rightAnchor != null)
            StretchBand(rightBand, rightAnchor.position, pouch, rightBaseScale, rightMeshLength);
    }

    private void StretchBand(Transform band, Vector3 from, Vector3 to, Vector3 baseScale, float meshLength)
    {
        Vector3 delta = to - from;
        float length = delta.magnitude;
        if (length < 0.0001f) return;

        band.position = from + delta * 0.5f;   // centre of the band (mesh pivot should be centred)
        band.rotation = Quaternion.FromToRotation(AxisVector(lengthAxis), delta / length);

        Vector3 s = baseScale;
        s[(int)lengthAxis] = length / meshLength;   // stretch only along its length
        band.localScale = s;
    }

    private static Vector3 AxisVector(LengthAxis a)
    {
        switch (a)
        {
            case LengthAxis.X: return Vector3.right;
            case LengthAxis.Y: return Vector3.up;
            default:           return Vector3.forward;
        }
    }

    private void SetBandsActive(bool value)
    {
        if (leftBand != null) leftBand.gameObject.SetActive(value);
        if (rightBand != null) rightBand.gameObject.SetActive(value);
    }

    // ---------- Misc ----------

    private void SetRunnerEnabled(bool value)
    {
        foreach (var b in disableUntilLaunch)
            if (b != null) b.enabled = value;
    }

    // Works with both the old Input Manager and the new Input System (mouse + touch).
    private static void ReadPointer(out Vector2 pos, out bool down, out bool up)
    {
#if ENABLE_INPUT_SYSTEM
        var p = Pointer.current;
        if (p == null) { pos = default; down = up = false; return; }
        pos  = p.position.ReadValue();
        down = p.press.wasPressedThisFrame;
        up   = p.press.wasReleasedThisFrame;
#else
        pos  = Input.mousePosition;
        down = Input.GetMouseButtonDown(0);
        up   = Input.GetMouseButtonUp(0);
#endif
    }
}