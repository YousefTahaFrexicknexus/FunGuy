using UnityEngine;

namespace Funguy.MushroomRunner
{
    /// <summary>
    /// Defines how a mushroom converts incoming momentum into a launch response.
    /// </summary>
    [CreateAssetMenu(fileName = "MushroomBounceProfile", menuName = "Funguy/MushroomRunner/Mushroom Bounce Profile")]
    public sealed class MushroomBounceProfile : ScriptableObject
    {
        [Header("Momentum")]
        [SerializeField, Tooltip("Multiplies the incoming velocity carried through the bounce.")]
        private float velocityScale = 1f;
        [SerializeField, Range(0f, 1f), Tooltip("How much the player's input direction can steer the outgoing launch.")]
        private float directionalInfluence = 0.45f;
        [SerializeField, Tooltip("Extra planar speed added along the resolved launch direction.")]
        private float planarBoost;

        [Header("Vertical Launch")]
        [SerializeField, Tooltip("If enabled, Upward Impulse is used directly instead of scaling Base Jump Force.")]
        private bool useAbsoluteUpwardImpulse;
        [SerializeField, Tooltip("Upward launch added by the bounce, either absolute or multiplied by Base Jump Force.")]
        private float upwardImpulse = 1f;
        [SerializeField, Tooltip("How much downward impact is converted into a cleaner outgoing launch.")]
        private float impactRecoveryFactor = 0.25f;

        [Header("Launch Direction")]
        [SerializeField, Tooltip("Local-space launch direction before it is transformed by the mushroom.")]
        private Vector3 localLaunchDirection = Vector3.up;
        [SerializeField, Range(0f, 1f), Tooltip("How strongly the final launch direction is blended back toward up.")]
        private float upBlend = 0.85f;

        [Header("Optional Landing Drag")]
        [SerializeField, Tooltip("If enabled, this bounce can override the post-bounce planar drag value.")]
        private bool overridePlanarDrag;
        [SerializeField, Tooltip("Custom planar drag value used when Override Planar Drag is enabled.")]
        private float planarDragOverride;

        public BounceSurfaceResponse CreateResponse(Transform surfaceTransform, in BounceContext context)
        {
            Transform origin = surfaceTransform != null ? surfaceTransform : null;
            Vector3 worldLaunchDirection = origin != null
                ? origin.TransformDirection(GetSafeLaunchDirection())
                : GetSafeLaunchDirection();

            return CreateResponse(worldLaunchDirection, context);
        }

        public BounceSurfaceResponse CreateDirectedResponse(Transform launchDirectionTransform, in BounceContext context)
        {
            Vector3 worldLaunchDirection = launchDirectionTransform != null
                ? launchDirectionTransform.forward
                : Vector3.forward;

            if (worldLaunchDirection.sqrMagnitude <= 0.0001f)
            {
                worldLaunchDirection = Vector3.forward;
            }

            return CreateResponse(worldLaunchDirection, context);
        }

        private BounceSurfaceResponse CreateResponse(Vector3 worldLaunchDirection, in BounceContext context)
        {
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

