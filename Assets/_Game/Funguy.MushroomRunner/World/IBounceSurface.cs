using UnityEngine;

namespace Funguy.MushroomRunner
{
    public interface IBounceSurface
    {
        BounceSurfaceResponse GetBounceResponse(in BounceContext context);
    }

    public interface IBounceContactResolver
    {
        bool TryResolveBounceContact(
            in Collision collision,
            Vector3 worldUp,
            float minGroundDot,
            out Vector3 contactPoint,
            out Vector3 contactNormal,
            out float groundDot);
    }

    public interface IBounceSurfaceBehavior
    {
        bool AllowsBounceWhileMovingUpward { get; }

        float PostBounceCollisionIgnoreDuration { get; }
    }
}

