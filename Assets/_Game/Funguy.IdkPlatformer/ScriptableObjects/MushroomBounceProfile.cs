using UnityEngine;

namespace Funguy.IdkPlatformer
{
    [CreateAssetMenu(fileName = "MushroomBounceProfile", menuName = "Funguy/IdkPlatformer/Mushroom Bounce Profile")]
    public sealed class MushroomBounceProfile : ScriptableObject
    {
        [Header("Momentum")]
        [SerializeField] private float velocityScale = 1f;
        [SerializeField, Range(0f, 1f)] private float directionalInfluence = 0.45f;
        [SerializeField] private float planarBoost;

        [Header("Vertical Launch")]
        [SerializeField] private bool useAbsoluteUpwardImpulse;
        [SerializeField] private float upwardImpulse = 1f;
        [SerializeField] private float impactRecoveryFactor = 0.25f;

        [Header("Launch Direction")]
        [SerializeField] private Vector3 localLaunchDirection = Vector3.up;
        [SerializeField, Range(0f, 1f)] private float upBlend = 0.85f;

        [Header("Optional Landing Drag")]
        [SerializeField] private bool overridePlanarDrag;
        [SerializeField] private float planarDragOverride;

        public BounceSurfaceResponse CreateResponse(Transform surfaceTransform, in BounceContext context)
        {
            Transform origin = surfaceTransform != null ? surfaceTransform : null;
            Vector3 worldLaunchDirection = origin != null
                ? origin.TransformDirection(GetSafeLaunchDirection())
                : GetSafeLaunchDirection();

            float resolvedUpwardImpulse = useAbsoluteUpwardImpulse
                ? upwardImpulse
                : context.BaseJumpForce * upwardImpulse;

            return new BounceSurfaceResponse(
                velocityScale,
                directionalInfluence,
                planarBoost,
                resolvedUpwardImpulse,
                impactRecoveryFactor,
                worldLaunchDirection,
                upBlend,
                overridePlanarDrag,
                planarDragOverride);
        }

        private Vector3 GetSafeLaunchDirection()
        {
            if (localLaunchDirection.sqrMagnitude <= 0.0001f)
            {
                return Vector3.up;
            }

            return localLaunchDirection.normalized;
        }

        private void OnValidate()
        {
            velocityScale = Mathf.Max(0f, velocityScale);
            directionalInfluence = Mathf.Clamp01(directionalInfluence);
            upwardImpulse = Mathf.Max(0f, upwardImpulse);
            impactRecoveryFactor = Mathf.Max(0f, impactRecoveryFactor);
            planarDragOverride = Mathf.Max(0f, planarDragOverride);
        }
    }
}
