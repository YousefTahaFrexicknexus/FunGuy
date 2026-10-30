
using DG.Tweening;
using UnityEngine;

public class SlingshotController : MonoBehaviour
{
    private enum LauncherState
    {
        Ready,
        Pulling,
        Launched
    }

    [Header("References")]
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private Transform pullPoint;
    [SerializeField] private Transform launchPoint;
    [SerializeField] private Rigidbody playerRigidbody;
    [SerializeField] private ProceduralHammock hammock;

    [Header("Input")]
    [SerializeField] private float maxPullDistance = 2f;
    [SerializeField] private float inputPlaneOffset;
    [SerializeField] private LayerMask inputMask = ~0;

    [Header("Launch")]
    [SerializeField] private float launchSpeed = 10f;
    [SerializeField] private float upwardBoost = 4f;

    private LauncherState state = LauncherState.Ready;
    private Plane inputPlane;
    private Vector3 initialPullPosition;
    private Vector3 currentPullPosition;

    private void Awake()
    {
        if (gameplayCamera == null)
            gameplayCamera = Camera.main;

        if (pullPoint != null)
        {
            initialPullPosition = pullPoint.position;
            currentPullPosition = initialPullPosition;
        }

        // Keep the player stationary while aiming.
        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.isKinematic = true;
        }

        Vector3 planeNormal = gameplayCamera != null
            ? gameplayCamera.transform.forward
            : Vector3.forward;

        inputPlane = new Plane(
            planeNormal,
            initialPullPosition +
            planeNormal * inputPlaneOffset
        );
    }

    private void Update()
    {
        if (state == LauncherState.Launched)
            return;

        if (Input.GetMouseButtonDown(0))
            BeginPull(Input.mousePosition);

        if (state == LauncherState.Pulling &&
            Input.GetMouseButton(0))
        {
            UpdatePull(Input.mousePosition);
        }

        if (state == LauncherState.Pulling &&
            Input.GetMouseButtonUp(0))
        {
            Release();
        }

        // Touch input for mobile.
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
                BeginPull(touch.position);

            if (state == LauncherState.Pulling &&
                (touch.phase == TouchPhase.Moved ||
                 touch.phase == TouchPhase.Stationary))
            {
                UpdatePull(touch.position);
            }

            if (state == LauncherState.Pulling &&
                (touch.phase == TouchPhase.Ended ||
                 touch.phase == TouchPhase.Canceled))
            {
                Release();
            }
        }
    }

    private void BeginPull(Vector2 screenPosition)
    {
        if (state != LauncherState.Ready)
            return;

        state = LauncherState.Pulling;
        UpdatePull(screenPosition);
    }

    private void UpdatePull(Vector2 screenPosition)
    {
        if (gameplayCamera == null || pullPoint == null)
            return;

        Ray ray = gameplayCamera.ScreenPointToRay(screenPosition);

        if (!inputPlane.Raycast(ray, out float distance))
            return;

        Vector3 pointerPosition = ray.GetPoint(distance);

        // Drag relative to the initial resting position.
        Vector3 offset = pointerPosition - initialPullPosition;

        if (offset.magnitude > maxPullDistance)
            offset = offset.normalized * maxPullDistance;

        currentPullPosition = initialPullPosition + offset;
        pullPoint.position = currentPullPosition;
    }

    private void Release()
    {
        if (state != LauncherState.Pulling)
            return;

        state = LauncherState.Launched;

        Vector3 pullOffset =
            currentPullPosition - initialPullPosition;

        // Launch opposite to the pull direction.
        Vector3 launchDirection = -pullOffset.normalized;

        if (pullOffset.sqrMagnitude < 0.001f)
            launchDirection = launchPoint != null
                ? launchPoint.forward
                : transform.forward;

        launchDirection =
            (launchDirection + Vector3.up * upwardBoost / launchSpeed)
            .normalized;

        if (playerRigidbody != null)
        {
            playerRigidbody.isKinematic = false;
            playerRigidbody.linearVelocity =
                launchDirection * launchSpeed;
        }

        if (hammock != null)
            hammock.SnapBack();
    }

    public void ResetLauncher()
    {
        state = LauncherState.Ready;

        if (pullPoint != null)
        {
            pullPoint.position = initialPullPosition;
            currentPullPosition = initialPullPosition;
        }

        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.isKinematic = true;
        }

        if (hammock != null)
            hammock.ResetHammock();
    }
}
