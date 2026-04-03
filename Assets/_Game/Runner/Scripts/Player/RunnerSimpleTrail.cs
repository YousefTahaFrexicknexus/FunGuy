using UnityEngine;

namespace FunGuy.Runner
{
    [DisallowMultipleComponent]
    public sealed class RunnerSimpleTrail : MonoBehaviour
    {
        [SerializeField] private TrailRenderer trailRenderer;
        [SerializeField] private Vector3 localOffset = new(0f, -0.12f, -0.18f);
        [SerializeField] private float trailTime = 0.22f;
        [SerializeField] private float startWidth = 0.4f;
        [SerializeField] private float endWidth = 0.02f;
        [SerializeField] private float minVertexDistance = 0.08f;
        [SerializeField] private Color startColor = new(0.68f, 1f, 0.94f, 0.72f);
        [SerializeField] private Color endColor = new(0.68f, 1f, 0.94f, 0f);

        private Material generatedMaterial;

        private void Awake()
        {
            ApplyNow();
        }

        private void OnDestroy()
        {
            if (generatedMaterial != null)
            {
                Destroy(generatedMaterial);
            }
        }

        [ContextMenu("Apply Trail Setup")]
        public void ApplyNow()
        {
            trailRenderer = ResolveTrailRenderer();
            trailRenderer.transform.localPosition = localOffset;
            trailRenderer.alignment = LineAlignment.View;
            trailRenderer.textureMode = LineTextureMode.Stretch;
            trailRenderer.emitting = true;
            trailRenderer.autodestruct = false;
            trailRenderer.generateLightingData = false;
            trailRenderer.numCapVertices = 0;
            trailRenderer.numCornerVertices = 1;
            trailRenderer.minVertexDistance = minVertexDistance;
            trailRenderer.time = trailTime;
            trailRenderer.startWidth = startWidth;
            trailRenderer.endWidth = endWidth;
            trailRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trailRenderer.receiveShadows = false;
            trailRenderer.sharedMaterial = GetTrailMaterial();
            trailRenderer.startColor = startColor;
            trailRenderer.endColor = endColor;
        }

        public void ClearTrail()
        {
            if (trailRenderer == null)
            {
                trailRenderer = ResolveTrailRenderer();
            }

            trailRenderer.Clear();
        }

        private TrailRenderer ResolveTrailRenderer()
        {
            if (trailRenderer != null)
            {
                return trailRenderer;
            }

            Transform existing = transform.Find("TrailPivot");
            GameObject pivotObject;

            if (existing != null)
            {
                pivotObject = existing.gameObject;
            }
            else
            {
                pivotObject = new GameObject("TrailPivot");
                pivotObject.transform.SetParent(transform, false);
            }

            TrailRenderer trail = pivotObject.GetComponent<TrailRenderer>();

            if (trail == null)
            {
                trail = pivotObject.AddComponent<TrailRenderer>();
            }

            return trail;
        }

        private Material GetTrailMaterial()
        {
            if (trailRenderer != null && trailRenderer.sharedMaterial != null)
            {
                return trailRenderer.sharedMaterial;
            }

            if (generatedMaterial != null)
            {
                return generatedMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");

            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            generatedMaterial = new Material(shader)
            {
                name = "RunnerTrail_Runtime"
            };

            if (generatedMaterial.HasProperty("_BaseColor"))
            {
                generatedMaterial.SetColor("_BaseColor", startColor);
            }

            if (generatedMaterial.HasProperty("_Color"))
            {
                generatedMaterial.SetColor("_Color", startColor);
            }

            return generatedMaterial;
        }
    }
}
