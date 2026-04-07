using UnityEngine;

namespace Funguy.IdkPlatformer
{
    public readonly struct MovementInputFrame
    {
        public static MovementInputFrame Empty => new(Vector2.zero, Vector3.zero, Vector3.forward, 0f, false);

        public MovementInputFrame(
            Vector2 move,
            Vector3 wishDirection,
            Vector3 referenceForward,
            float magnitude,
            bool dashPressed)
        {
            Move = move;
            WishDirection = wishDirection;
            ReferenceForward = referenceForward;
            Magnitude = Mathf.Clamp01(magnitude);
            DashPressed = dashPressed;
        }

        public Vector2 Move { get; }

        public Vector3 WishDirection { get; }

        public Vector3 ReferenceForward { get; }

        public float Magnitude { get; }

        public bool DashPressed { get; }

        public bool HasMoveInput => Magnitude > 0f && WishDirection.sqrMagnitude > 0f;
    }
}
