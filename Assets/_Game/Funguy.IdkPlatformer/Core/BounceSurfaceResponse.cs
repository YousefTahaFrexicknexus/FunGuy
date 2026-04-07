using UnityEngine;

namespace Funguy.IdkPlatformer
{
    public readonly struct BounceSurfaceResponse
    {
        public BounceSurfaceResponse(
            float velocityScale,
            float directionalInfluence,
            float planarBoost,
            float upwardImpulse,
            float impactRecoveryFactor,
            Vector3 launchDirection,
            float upBlend,
            bool hasPlanarDragOverride = false,
            float planarDragOverride = 0f)
        {
            VelocityScale = Mathf.Max(0f, velocityScale);
            DirectionalInfluence = Mathf.Clamp01(directionalInfluence);
            PlanarBoost = planarBoost;
            UpwardImpulse = Mathf.Max(0f, upwardImpulse);
            ImpactRecoveryFactor = Mathf.Max(0f, impactRecoveryFactor);
            LaunchDirection = launchDirection;
            UpBlend = Mathf.Clamp01(upBlend);
            HasPlanarDragOverride = hasPlanarDragOverride;
            PlanarDragOverride = Mathf.Max(0f, planarDragOverride);
        }

        public float VelocityScale { get; }

        public float DirectionalInfluence { get; }

        public float PlanarBoost { get; }

        public float UpwardImpulse { get; }

        public float ImpactRecoveryFactor { get; }

        public Vector3 LaunchDirection { get; }

        public float UpBlend { get; }

        public bool HasPlanarDragOverride { get; }

        public float PlanarDragOverride { get; }
    }
}
