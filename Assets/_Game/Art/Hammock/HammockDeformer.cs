using UnityEngine;

/// <summary>
/// Bends the hammock mesh so its middle follows the pulled-back character, with the ends staying
/// tied to the trees. After the launch the hammock springs forward and wobbles to rest.
/// Put this on the hammock object (MeshFilter + MeshRenderer, NOT a SkinnedMeshRenderer).
/// </summary>
[RequireComponent(typeof(MeshFilter))]
public class HammockDeformer : MonoBehaviour
{
    public enum LengthAxis { X, Z }

    [SerializeField] private HammockSlingshot slingshot;

    [Header("Mesh")]
    [Tooltip("The mesh's local axis that runs from one tree to the other.")]
    [SerializeField] private LengthAxis lengthAxis = LengthAxis.X;
    [Tooltip("How much each part follows the character, from one tree (left of the curve) to the other (right). " +
             "Keep both ends at 0 so the knots stay on the trees. Peak = how much the middle follows.")]
    [SerializeField] private AnimationCurve followCurve = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0f));
    [Tooltip("The cloth narrows across its width as it stretches. 0 = off.")]
    [SerializeField, Range(0f, 0.6f)] private float widthGather = 0.25f;

    [Header("Wobble after release")]
    [Tooltip("Oscillations per second.")]
    [SerializeField] private float wobbleFrequency = 3f;
    [Tooltip("Lower = bouncier, higher = settles faster.")]
    [SerializeField, Range(0.02f, 1f)] private float damping = 0.12f;
    [Tooltip("How hard the hammock whips forward after release, as a fraction of the launch speed.")]
    [SerializeField, Range(0f, 0.5f)] private float wobbleStrength = 0.15f;

    private Mesh mesh;
    private Vector3[] original;
    private Vector3[] deformed;
    private float[] weights;
    private float widthCenter;

    private Vector3 offset;       // how far the middle of the hammock is from rest (world space)
    private Vector3 velocity;
    private bool wobbling;

    private int LengthIndex => lengthAxis == LengthAxis.X ? 0 : 2;
    private int WidthIndex  => lengthAxis == LengthAxis.X ? 2 : 0;

    private void Awake()
    {
        if (slingshot == null)
        {
            Debug.LogError("HammockDeformer: assign the HammockSlingshot (on your character).", this);
            enabled = false;
            return;
        }

        mesh = GetComponent<MeshFilter>().mesh;   // per-object copy, the original asset is untouched
        mesh.MarkDynamic();

        original = mesh.vertices;
        deformed = new Vector3[original.Length];
        weights  = new float[original.Length];

        int li = LengthIndex, wi = WidthIndex;
        float lMin = float.MaxValue, lMax = float.MinValue, wMin = float.MaxValue, wMax = float.MinValue;
        foreach (var v in original)
        {
            lMin = Mathf.Min(lMin, v[li]); lMax = Mathf.Max(lMax, v[li]);
            wMin = Mathf.Min(wMin, v[wi]); wMax = Mathf.Max(wMax, v[wi]);
        }
        widthCenter = (wMin + wMax) * 0.5f;

        for (int i = 0; i < original.Length; i++)
        {
            float t = Mathf.InverseLerp(lMin, lMax, original[i][li]);
            weights[i] = Mathf.Clamp01(followCurve.Evaluate(t));
        }
    }

    private void OnEnable()  { if (slingshot != null) slingshot.Launched += OnLaunched; }
    private void OnDisable() { if (slingshot != null) slingshot.Launched -= OnLaunched; }
    private void OnDestroy() { if (mesh != null) Destroy(mesh); }

    private void OnLaunched(Vector3 dir, float speed)
    {
        // The character just left: the hammock snaps forward past its rest position and wobbles.
        velocity = dir * (speed * wobbleStrength);
        wobbling = true;
    }

    // LateUpdate so it runs after the slingshot has moved the character this frame.
    private void LateUpdate()
    {
        Vector3 previous = offset;

        if (slingshot.IsHolding)
        {
            offset = slingshot.Displacement;
            velocity = Vector3.zero;
            wobbling = false;
        }
        else if (wobbling)
        {
            Simulate();
        }
        else
        {
            return;
        }

        if ((offset - previous).sqrMagnitude > 1e-10f)
            Deform();
    }

    private void Simulate()
    {
        float omega = 2f * Mathf.PI * wobbleFrequency;
        float k = omega * omega;
        float c = 2f * damping * omega;

        float remaining = Time.deltaTime;
        while (remaining > 0f)
        {
            float h = Mathf.Min(remaining, 1f / 120f);
            velocity += (-k * offset - c * velocity) * h;
            offset += velocity * h;
            remaining -= h;
        }

        if (offset.sqrMagnitude < 1e-6f && velocity.sqrMagnitude < 1e-4f)
        {
            offset = Vector3.zero;
            velocity = Vector3.zero;
            wobbling = false;
        }
    }

    private void Deform()
    {
        Vector3 local = transform.InverseTransformVector(offset);
        float strain = Mathf.Clamp01(offset.magnitude / slingshot.MaxPullDistance);
        int wi = WidthIndex;

        for (int i = 0; i < original.Length; i++)
        {
            Vector3 v = original[i];
            float w = weights[i];

            if (widthGather > 0f)
                v[wi] = Mathf.Lerp(v[wi], widthCenter, widthGather * strain * w);

            deformed[i] = v + local * w;
        }

        mesh.vertices = deformed;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }
}
