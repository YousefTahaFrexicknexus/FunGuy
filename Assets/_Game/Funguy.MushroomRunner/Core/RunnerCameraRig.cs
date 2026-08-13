using UnityEngine;

[DisallowMultipleComponent]
public sealed class RunnerCameraRig : MonoBehaviour
{
    [SerializeField, Tooltip("Follow target, usually the player's CameraFollowTarget child.")]
    Transform target;
    [SerializeField, Tooltip("Optional rigidbody used for FOV speed response. If empty, the rig resolves one from the target hierarchy.")]
    Rigidbody velocitySource;
    [SerializeField, Tooltip("World-space offset from the target to the camera.")]
    Vector3 followOffset = new(0f, 6f, -10.5f);
    [SerializeField, Tooltip("Offset added to the target when computing the look-at point.")]
    Vector3 lookOffset = new(0f, 0.35f, 0f);
    [SerializeField, Tooltip("How quickly the camera catches up to the desired follow position.")]
    float followSharpness = 8f;
    [SerializeField, Tooltip("If enabled, the camera rotates to face the look point every frame.")]
    bool lookAtTarget = true;
    [SerializeField, Tooltip("Field of view used when the player is standing still.")]
    float baseFieldOfView = 50f;
    [SerializeField, Tooltip("Maximum field of view reached at high speed.")]
    float maxFieldOfView = 66f;
    [SerializeField, Tooltip("Planar speed required to reach Max Field Of View.")]
    float speedForMaxFieldOfView = 20f;
    [SerializeField, Tooltip("How quickly the camera eases toward the target field of view.")]
    float fieldOfViewSharpness = 6f;

    Camera attachedCamera;
    Rigidbody cachedTargetBody;

    public void SetTarget(Transform followTarget)
    {
        target = followTarget;

        if (velocitySource == null)
        {
            cachedTargetBody = null;
        }
    }

    public void SetVelocitySource(Rigidbody body)
    {
        velocitySource = body;
        cachedTargetBody = body;
    }

    void Awake()
    {
        attachedCamera = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 desiredPosition = target.position + followOffset;
        float interpolation = 1f - Mathf.Exp(-Mathf.Max(0f, followSharpness) * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desiredPosition, interpolation);
        
        UpdateFieldOfView();

        if (!lookAtTarget)
        {
            return;
        }

        Vector3 lookPoint = target.position + lookOffset;
        Vector3 lookDirection = lookPoint - transform.position;

        if (lookDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
    }

    void UpdateFieldOfView()
    {
        if (attachedCamera == null)
        {
            return;
        }

        Rigidbody targetBody = ResolveTargetBody();
        float speed = 0f;

        if (targetBody != null)
        {
            Vector3 planarVelocity = Vector3.ProjectOnPlane(targetBody.linearVelocity, Vector3.up);
            speed = planarVelocity.magnitude;
        }

        float normalizedSpeed = Mathf.Clamp01(speed / Mathf.Max(0.001f, speedForMaxFieldOfView));
        float targetFieldOfView = Mathf.Lerp(baseFieldOfView, maxFieldOfView, normalizedSpeed);
        float interpolation = 1f - Mathf.Exp(-Mathf.Max(0f, fieldOfViewSharpness) * Time.deltaTime);

        attachedCamera.fieldOfView = Mathf.Lerp(attachedCamera.fieldOfView, targetFieldOfView, interpolation);
    }

    Rigidbody ResolveTargetBody()
    {
        if (velocitySource != null)
        {
            cachedTargetBody = velocitySource;
            return cachedTargetBody;
        }

        if (cachedTargetBody != null)
        {
            return cachedTargetBody;
        }

        cachedTargetBody = target != null ? target.GetComponentInParent<Rigidbody>() : null;

        return cachedTargetBody;
    }
}