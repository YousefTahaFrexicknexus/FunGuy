using UnityEngine;

namespace Funguy.IdkPlatformer
{
    public readonly struct BounceContext
    {
        public BounceContext(
            Vector3 incomingVelocity,
            Vector3 contactPoint,
            Vector3 contactNormal,
            Vector3 worldUp,
            float baseJumpForce,
            MovementInputFrame inputFrame)
        {
            IncomingVelocity = incomingVelocity;
            ContactPoint = contactPoint;
            ContactNormal = contactNormal;
            WorldUp = worldUp;
            BaseJumpForce = baseJumpForce;
            InputFrame = inputFrame;
        }

        public Vector3 IncomingVelocity { get; }

        public Vector3 ContactPoint { get; }

        public Vector3 ContactNormal { get; }

        public Vector3 WorldUp { get; }

        public float BaseJumpForce { get; }

        public MovementInputFrame InputFrame { get; }
    }
}
