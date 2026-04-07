using UnityEngine;

namespace Funguy.IdkPlatformer
{
    [CreateAssetMenu(fileName = "MovementTuningProfile", menuName = "Funguy/IdkPlatformer/Movement Tuning Profile")]
    public sealed class MovementTuningProfile : ScriptableObject
    {
        [Header("Air Control")]
        [SerializeField] private float moveAcceleration = 24f;
        [SerializeField] private float airControlStrength = 1f;
        [SerializeField] private float forwardAirControlMultiplier = 0.6f;
        [SerializeField] private float airBrakeAcceleration = 18f;
        [SerializeField] private float maxControllableSpeed = 12f;
        [SerializeField] private float maxSpeed = 18f;
        [SerializeField] private float overSpeedDrag = 8f;
        [SerializeField] private float airDrag = 0.5f;

        [Header("Gravity")]
        [SerializeField] private float gravityScale = 1f;
        [SerializeField] private float jumpGravityMultiplier = 0.85f;
        [SerializeField] private float fallGravityMultiplier = 1.35f;

        [Header("Bounce And Dash")]
        [SerializeField] private float baseJumpForce = 9f;
        [SerializeField] private float dashForce = 8f;
        [SerializeField] private float dashCooldown = 0.2f;
        [SerializeField] private int dashChargesPerBounce = 1;
        [SerializeField] private float postBounceLowControlTime = 0.1f;
        [SerializeField] private float postBounceAirControlMultiplier = 0.35f;
        [SerializeField] private float postDashBonusControlTime = 0.18f;
        [SerializeField] private float postDashAirControlMultiplier = 1.35f;

        [Header("Forgiveness")]
        [SerializeField] private float bounceGraceTime = 0.1f;
        [SerializeField] private float dashBufferTime = 0.1f;
        [SerializeField, Range(0f, 1f)] private float minGroundDot = 0.65f;

        public float MoveAcceleration => moveAcceleration;

        public float AirControlStrength => airControlStrength;

        public float ForwardAirControlMultiplier => forwardAirControlMultiplier;

        public float AirBrakeAcceleration => airBrakeAcceleration;

        public float MaxControllableSpeed => maxControllableSpeed;

        public float MaxSpeed => Mathf.Max(maxControllableSpeed, maxSpeed);

        public float OverSpeedDrag => overSpeedDrag;

        public float AirDrag => airDrag;

        public float GravityScale => gravityScale;

        public float JumpGravityMultiplier => jumpGravityMultiplier;

        public float FallGravityMultiplier => fallGravityMultiplier;

        public float BaseJumpForce => baseJumpForce;

        public float DashForce => dashForce;

        public float DashCooldown => dashCooldown;

        public int DashChargesPerBounce => Mathf.Max(1, dashChargesPerBounce);

        public float PostBounceLowControlTime => postBounceLowControlTime;

        public float PostBounceAirControlMultiplier => postBounceAirControlMultiplier;

        public float PostDashBonusControlTime => postDashBonusControlTime;

        public float PostDashAirControlMultiplier => postDashAirControlMultiplier;

        public float BounceGraceTime => bounceGraceTime;

        public float DashBufferTime => dashBufferTime;

        public float MinGroundDot => minGroundDot;

        private void OnValidate()
        {
            moveAcceleration = Mathf.Max(0f, moveAcceleration);
            airControlStrength = Mathf.Max(0f, airControlStrength);
            forwardAirControlMultiplier = Mathf.Max(0f, forwardAirControlMultiplier);
            airBrakeAcceleration = Mathf.Max(0f, airBrakeAcceleration);
            maxControllableSpeed = Mathf.Max(0f, maxControllableSpeed);
            maxSpeed = Mathf.Max(maxControllableSpeed, maxSpeed);
            overSpeedDrag = Mathf.Max(0f, overSpeedDrag);
            airDrag = Mathf.Max(0f, airDrag);
            gravityScale = Mathf.Max(0f, gravityScale);
            jumpGravityMultiplier = Mathf.Max(0f, jumpGravityMultiplier);
            fallGravityMultiplier = Mathf.Max(0f, fallGravityMultiplier);
            baseJumpForce = Mathf.Max(0f, baseJumpForce);
            dashForce = Mathf.Max(0f, dashForce);
            dashCooldown = Mathf.Max(0f, dashCooldown);
            dashChargesPerBounce = Mathf.Max(1, dashChargesPerBounce);
            postBounceLowControlTime = Mathf.Max(0f, postBounceLowControlTime);
            postBounceAirControlMultiplier = Mathf.Max(0f, postBounceAirControlMultiplier);
            postDashBonusControlTime = Mathf.Max(0f, postDashBonusControlTime);
            postDashAirControlMultiplier = Mathf.Max(0f, postDashAirControlMultiplier);
            bounceGraceTime = Mathf.Max(0f, bounceGraceTime);
            dashBufferTime = Mathf.Max(0f, dashBufferTime);
            minGroundDot = Mathf.Clamp01(minGroundDot);
        }
    }
}
