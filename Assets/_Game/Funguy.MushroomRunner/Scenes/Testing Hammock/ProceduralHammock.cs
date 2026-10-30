using DG.Tweening;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ProceduralHammock : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform leftAnchor;
    [SerializeField] private Transform rightAnchor;
    [SerializeField] private Transform pullPoint;

    [Header("Mesh")]
    [SerializeField, Range(4, 32)] private int segments = 16;
    [SerializeField, Min(0.01f)] private float width = 1f;

    [Header("Rest Shape")]
    [SerializeField] private float restSag = 0.15f;

    [Header("Deformation")]
    [SerializeField] private float maxPullDistance = 2f;
    [SerializeField] private float pullMultiplier = 1f;
    [SerializeField] private float widthCompression = 0.25f;

    [Header("Release")]
    [SerializeField] private float resetDuration = 0.2f;
    [SerializeField] private Ease resetEase = Ease.OutQuad;

    private MeshFilter meshFilter;
    private Mesh mesh;

    private Vector3[] vertices;
    private Vector2[] uvs;
    private int[] triangles;

    private float currentPull;

    private Tween resetTween;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();

        CreateMesh();
        UpdateMesh(0f);
    }

    private void LateUpdate()
    {
        if (leftAnchor == null ||
            rightAnchor == null ||
            pullPoint == null)
        {
            return;
        }

        Vector3 left = leftAnchor.position;
        Vector3 right = rightAnchor.position;

        Vector3 center = Vector3.Lerp(left, right, 0.5f);

        float pullDistance = Vector3.Distance(center, pullPoint.position);

        currentPull = Mathf.Min(
            pullDistance,
            maxPullDistance
        );

        UpdateMesh(currentPull);
    }

    private void CreateMesh()
    {
        mesh = new Mesh
        {
            name = "Procedural Hammock"
        };

        mesh.MarkDynamic();

        meshFilter.sharedMesh = mesh;

        int vertexCount = (segments + 1) * 2;

        vertices = new Vector3[vertexCount];
        uvs = new Vector2[vertexCount];
        triangles = new int[segments * 6];

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;

            int topIndex = i * 2;
            int bottomIndex = topIndex + 1;

            uvs[topIndex] = new Vector2(t, 1f);
            uvs[bottomIndex] = new Vector2(t, 0f);

            if (i >= segments)
                continue;

            int triangleIndex = i * 6;

            triangles[triangleIndex] = topIndex;
            triangles[triangleIndex + 1] = topIndex + 2;
            triangles[triangleIndex + 2] = bottomIndex;

            triangles[triangleIndex + 3] = bottomIndex;
            triangles[triangleIndex + 4] = topIndex + 2;
            triangles[triangleIndex + 5] = bottomIndex + 2;
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    private void UpdateMesh(float pullDistance)
    {
        Vector3 left = transform.InverseTransformPoint(
            leftAnchor.position
        );

        Vector3 right = transform.InverseTransformPoint(
            rightAnchor.position
        );

        Vector3 pull = transform.InverseTransformPoint(
            pullPoint.position
        );

        Vector3 center = Vector3.Lerp(left, right, 0.5f);

        Vector3 horizontalDirection = (
            right - left
        ).normalized;

        Vector3 widthDirection = Vector3.Cross(
            horizontalDirection,
            Vector3.up
        ).normalized;

        if (widthDirection.sqrMagnitude < 0.001f)
        {
            widthDirection = transform.forward;
        }

        float normalizedPull = Mathf.Clamp01(
            pullDistance / maxPullDistance
        );

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;

            int topIndex = i * 2;
            int bottomIndex = topIndex + 1;

            Vector3 position = GetSurfacePosition(
                left,
                right,
                center,
                pull,
                t,
                normalizedPull
            );

            float centerWeight = Mathf.Sin(
                t * Mathf.PI
            );

            // Compress the hammock width as it stretches.
            float compression = Mathf.Lerp(
                1f,
                1f - widthCompression,
                normalizedPull * centerWeight
            );

            Vector3 widthOffset =
                widthDirection *
                (width * 0.5f) *
                compression;

            vertices[topIndex] = position + widthOffset;
            vertices[bottomIndex] = position - widthOffset;
        }

        mesh.vertices = vertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    private Vector3 GetSurfacePosition(
        Vector3 left,
        Vector3 right,
        Vector3 center,
        Vector3 pull,
        float t,
        float normalizedPull)
    {
        Vector3 basePosition = Vector3.Lerp(
            left,
            right,
            t
        );

        // Natural hammock sag.
        float sagWeight = Mathf.Sin(
            t * Mathf.PI
        );

        Vector3 sagOffset =
            Vector3.down *
            restSag *
            sagWeight;

        // Pull deformation.
        float pullWeight = Mathf.Sin(
            t * Mathf.PI
        );

        Vector3 pullOffset =
            (pull - center) *
            pullWeight *
            pullMultiplier *
            normalizedPull;

        return basePosition +
               sagOffset +
               pullOffset;
    }

    public void ResetHammock()
    {
        resetTween?.Kill();

        resetTween = DOTween.To(
            () => currentPull,
            value =>
            {
                currentPull = value;
                UpdateMesh(value);
            },
            0f,
            resetDuration
        )
        .SetEase(resetEase);
    }

    public void SnapBack()
    {
        resetTween?.Kill();

        resetTween = DOTween.To(
            () => currentPull,
            value =>
            {
                currentPull = value;
                UpdateMesh(value);
            },
            0f,
            resetDuration
        )
        .SetEase(Ease.OutBack);
    }

    private void OnDestroy()
    {
        resetTween?.Kill();
    }

    private void OnDrawGizmos()
    {
        if (leftAnchor == null ||
            rightAnchor == null ||
            pullPoint == null)
        {
            return;
        }

        Gizmos.color = Color.green;
        Gizmos.DrawSphere(leftAnchor.position, 0.08f);
        Gizmos.DrawSphere(rightAnchor.position, 0.08f);

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(pullPoint.position, 0.12f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(
            leftAnchor.position,
            pullPoint.position
        );

        Gizmos.DrawLine(
            rightAnchor.position,
            pullPoint.position
        );
    }
}