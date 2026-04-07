using UnityEngine;

namespace Funguy.IdkPlatformer
{
    public readonly struct BounceEventData
    {
        public BounceEventData(
            Collider surfaceCollider,
            Vector3 contactPoint,
            Vector3 contactNormal,
            Vector3 incomingVelocity,
            Vector3 outgoingVelocity,
            BounceSurfaceResponse response)
        {
            SurfaceCollider = surfaceCollider;
            ContactPoint = contactPoint;
            ContactNormal = contactNormal;
            IncomingVelocity = incomingVelocity;
            OutgoingVelocity = outgoingVelocity;
            Response = response;
        }

        public Collider SurfaceCollider { get; }

        public Vector3 ContactPoint { get; }

        public Vector3 ContactNormal { get; }

        public Vector3 IncomingVelocity { get; }

        public Vector3 OutgoingVelocity { get; }

        public BounceSurfaceResponse Response { get; }
    }
}
