using System;
using UnityEngine;

namespace Funguy.IdkPlatformer
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class MovementMotor : MonoBehaviour
    {
        private const float DashInputThreshold = 0.1f;
        private const float DashVelocityThreshold = 0.25f;
        private const float LandingVerticalTolerance = 0.25f;
        private const float GroundedContactRetention = 0.05f;
        private const float MinDirectionSqrMagnitude = 0.0001f;

        [SerializeField] private Rigidbody body;
        [SerializeField] private MovementTuningProfile tuningProfile;
        [SerializeField] private bool motorEnabled = true;
        [SerializeField] private Vector3 worldUp = Vector3.up;

        private MovementInputFrame currentInput = MovementInputFrame.Empty;
        private BounceCandidate lastBounceCandidate;
        private bool hasBounceCandidate;
        private float lastSurfaceTouchTime = float.NegativeInfinity;
        private float bufferedDashUntil = float.NegativeInfinity;
        private Collider lastConsumedSurface;
        private Func<bool> tryConsumeDashHandler;
        private bool isGrounded;

        public event Action<BounceEventData> Bounced;
        public event Action Dashed;

        public Vector3 Velocity => body != null ? body.linearVelocity : Vector3.zero;

        public bool IsGrounded => isGrounded;

        private Vector3 Up => worldUp.sqrMagnitude > MinDirectionSqrMagnitude ? worldUp.normalized : Vector3.up;

        private void Reset()
        {
            body = GetComponent<Rigidbody>();
        }

        private void Awake()
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody>();
            }

            ConfigureRigidbody();
        }

        private void OnValidate()
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody>();
            }

            worldUp = worldUp.sqrMagnitude > MinDirectionSqrMagnitude ? worldUp.normalized : Vector3.up;

            if (body != null)
            {
                ConfigureRigidbody();
            }
        }

        private void FixedUpdate()
        {
            isGrounded = ComputeGroundedState();

            if (body == null || tuningProfile == null)
            {
                return;
            }

            if (!motorEnabled)
            {
                currentInput = MovementInputFrame.Empty;
                return;
            }

            float deltaTime = Time.fixedDeltaTime;
            Vector3 velocity = body.linearVelocity;

            ApplyShapedGravity(ref velocity, deltaTime);

            float planarDrag = 0f;
            bool applyPlanarDrag = false;
            bool bouncedThisStep = TryConsumeBounceCandidate(ref velocity, out BounceSurfaceResponse bounceResponse);
            if (bouncedThisStep && bounceResponse.HasPlanarDragOverride)
            {
                planarDrag = bounceResponse.PlanarDragOverride;
                applyPlanarDrag = planarDrag > 0f;
            }
            else if (!isGrounded)
            {
                planarDrag = tuningProfile.AirDrag;
                applyPlanarDrag = planarDrag > 0f;
            }

            if (!bouncedThisStep && !isGrounded)
            {
                ApplyAirAcceleration(ref velocity, currentInput, deltaTime);
            }

            if (applyPlanarDrag)
            {
                ApplyPlanarDrag(ref velocity, planarDrag, deltaTime);
            }

            ApplySoftSpeedLimit(ref velocity, deltaTime);
            TryConsumeBufferedDash(ref velocity, bouncedThisStep);

            body.linearVelocity = velocity;
            isGrounded = !bouncedThisStep && ComputeGroundedState();
        }

        public void SetInput(MovementInputFrame inputFrame)
        {
            currentInput = inputFrame;
        }

        public void RequestDash()
        {
            if (tuningProfile == null)
            {
                return;
            }

            bufferedDashUntil = Mathf.Max(bufferedDashUntil, Time.time + tuningProfile.DashBufferTime);
        }

        public void SetTuningProfile(MovementTuningProfile profile)
        {
            tuningProfile = profile;
        }

        public void SetMotorEnabled(bool enabled)
        {
            motorEnabled = enabled;

            if (!enabled)
            {
                bufferedDashUntil = float.NegativeInfinity;
                currentInput = MovementInputFrame.Empty;
            }
        }

        public void SetDashResourceHandler(Func<bool> dashConsumer)
        {
            tryConsumeDashHandler = dashConsumer;
        }

        private void OnCollisionEnter(Collision collision)
        {
            CacheBounceCandidate(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            CacheBounceCandidate(collision);
        }

        private void OnCollisionExit(Collision collision)
        {
            Collider otherCollider = collision.collider;

            if (otherCollider == lastConsumedSurface)
            {
                lastConsumedSurface = null;
            }

            if (hasBounceCandidate && lastBounceCandidate.Collider == otherCollider)
            {
                hasBounceCandidate = false;
            }
        }

        private void ConfigureRigidbody()
        {
            body.useGravity = false;
            body.constraints |= RigidbodyConstraints.FreezeRotation;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        private void ApplyShapedGravity(ref Vector3 velocity, float deltaTime)
        {
            float verticalSpeed = Vector3.Dot(velocity, Up);
            float gravityMultiplier = verticalSpeed > 0f
                ? tuningProfile.JumpGravityMultiplier
                : tuningProfile.FallGravityMultiplier;

            velocity += Physics.gravity * tuningProfile.GravityScale * gravityMultiplier * deltaTime;
        }

        private void ApplyAirAcceleration(ref Vector3 velocity, MovementInputFrame inputFrame, float deltaTime)
        {
            if (!inputFrame.HasMoveInput)
            {
                return;
            }

            Vector3 planarVelocity = Vector3.ProjectOnPlane(velocity, Up);
            Vector3 wishDirection = inputFrame.WishDirection.normalized;
            float currentAlongWish = Vector3.Dot(planarVelocity, wishDirection);
            float targetAlongWish = tuningProfile.MaxControllableSpeed * inputFrame.Magnitude;
            float speedToAdd = targetAlongWish - currentAlongWish;
            if (speedToAdd <= 0f)
            {
                return;
            }

            float accelerationDelta = tuningProfile.MoveAcceleration
                * tuningProfile.AirControlStrength
                * inputFrame.Magnitude
                * deltaTime;

            planarVelocity += wishDirection * Mathf.Min(speedToAdd, accelerationDelta);
            velocity = planarVelocity + (Up * Vector3.Dot(velocity, Up));
        }

        private void ApplyPlanarDrag(ref Vector3 velocity, float drag, float deltaTime)
        {
            Vector3 planarVelocity = Vector3.ProjectOnPlane(velocity, Up);
            Vector3 verticalVelocity = Up * Vector3.Dot(velocity, Up);
            planarVelocity = Vector3.MoveTowards(planarVelocity, Vector3.zero, drag * deltaTime);
            velocity = planarVelocity + verticalVelocity;
        }

        private void ApplySoftSpeedLimit(ref Vector3 velocity, float deltaTime)
        {
            Vector3 planarVelocity = Vector3.ProjectOnPlane(velocity, Up);
            float planarSpeed = planarVelocity.magnitude;
            float overflow = planarSpeed - tuningProfile.MaxSpeed;

            if (overflow <= 0f || planarSpeed <= MinDirectionSqrMagnitude)
            {
                return;
            }

            float dragAmount = Mathf.Min(overflow, tuningProfile.OverSpeedDrag * overflow * deltaTime);
            planarVelocity -= planarVelocity.normalized * dragAmount;
            velocity = planarVelocity + (Up * Vector3.Dot(velocity, Up));
        }

        private bool TryConsumeBounceCandidate(ref Vector3 velocity, out BounceSurfaceResponse response)
        {
            response = default;

            if (!hasBounceCandidate || tuningProfile == null)
            {
                return false;
            }

            if (lastBounceCandidate.Collider == null || lastBounceCandidate.Surface == null)
            {
                hasBounceCandidate = false;
                return false;
            }

            if (lastBounceCandidate.Collider == lastConsumedSurface)
            {
                return false;
            }

            if (Time.time - lastBounceCandidate.Timestamp > tuningProfile.BounceGraceTime)
            {
                hasBounceCandidate = false;
                return false;
            }

            Vector3 incomingVelocity = velocity;
            BounceContext context = new(
                incomingVelocity,
                lastBounceCandidate.ContactPoint,
                lastBounceCandidate.ContactNormal,
                Up,
                tuningProfile.BaseJumpForce,
                currentInput);

            response = lastBounceCandidate.Surface.GetBounceResponse(in context);
            velocity = ApplyBounceResponse(incomingVelocity, response);

            Bounced?.Invoke(new BounceEventData(
                lastBounceCandidate.Collider,
                lastBounceCandidate.ContactPoint,
                lastBounceCandidate.ContactNormal,
                incomingVelocity,
                velocity,
                response));

            lastConsumedSurface = lastBounceCandidate.Collider;
            hasBounceCandidate = false;
            lastSurfaceTouchTime = float.NegativeInfinity;
            isGrounded = false;
            return true;
        }

        private Vector3 ApplyBounceResponse(Vector3 incomingVelocity, BounceSurfaceResponse response)
        {
            Vector3 planarVelocity = Vector3.ProjectOnPlane(incomingVelocity, Up);
            float planarSpeed = planarVelocity.magnitude;
            Vector3 planarDirection = ResolveSurfacePlanarDirection(planarVelocity, response);

            Vector3 redirectedPlanar = planarVelocity;
            if (planarSpeed > MinDirectionSqrMagnitude && planarDirection.sqrMagnitude > MinDirectionSqrMagnitude)
            {
                redirectedPlanar = Vector3.Lerp(
                    planarVelocity,
                    planarDirection * planarSpeed,
                    response.DirectionalInfluence);
            }

            Vector3 planarOut = redirectedPlanar * response.VelocityScale;
            if (planarDirection.sqrMagnitude > MinDirectionSqrMagnitude && Mathf.Abs(response.PlanarBoost) > 0f)
            {
                planarOut += planarDirection * response.PlanarBoost;
            }

            float verticalSpeed = Vector3.Dot(incomingVelocity, Up);
            float impactBonus = Mathf.Max(0f, -verticalSpeed) * response.ImpactRecoveryFactor;
            Vector3 verticalOut = Up * (response.UpwardImpulse + impactBonus);

            return planarOut + verticalOut;
        }

        private Vector3 ResolveSurfacePlanarDirection(Vector3 planarVelocity, BounceSurfaceResponse response)
        {
            Vector3 launchDirection = response.LaunchDirection.sqrMagnitude > MinDirectionSqrMagnitude
                ? response.LaunchDirection.normalized
                : Up;

            Vector3 blendedDirection = Vector3.Lerp(Up, launchDirection, response.UpBlend);
            Vector3 planarDirection = Vector3.ProjectOnPlane(blendedDirection, Up);

            if (planarDirection.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                planarDirection = Vector3.ProjectOnPlane(launchDirection, Up);
            }

            if (planarDirection.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                planarDirection = planarVelocity;
            }

            if (planarDirection.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                return Vector3.zero;
            }

            return planarDirection.normalized;
        }

        private bool TryConsumeBufferedDash(ref Vector3 velocity, bool bouncedThisStep)
        {
            if (tuningProfile == null || bufferedDashUntil < Time.time)
            {
                if (bufferedDashUntil < Time.time)
                {
                    bufferedDashUntil = float.NegativeInfinity;
                }

                return false;
            }

            if (!bouncedThisStep && isGrounded)
            {
                return false;
            }

            bool canConsumeDash = tryConsumeDashHandler == null || tryConsumeDashHandler.Invoke();
            if (!canConsumeDash)
            {
                return false;
            }

            Vector3 dashDirection = ResolveDashDirection(velocity);
            if (dashDirection.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                bufferedDashUntil = float.NegativeInfinity;
                return false;
            }

            velocity += dashDirection * tuningProfile.DashForce;
            bufferedDashUntil = float.NegativeInfinity;
            Dashed?.Invoke();
            return true;
        }

        private Vector3 ResolveDashDirection(Vector3 velocity)
        {
            Vector3 dashDirection;

            if (currentInput.Magnitude > DashInputThreshold && currentInput.WishDirection.sqrMagnitude > MinDirectionSqrMagnitude)
            {
                dashDirection = currentInput.WishDirection;
            }
            else
            {
                Vector3 planarVelocity = Vector3.ProjectOnPlane(velocity, Up);
                if (planarVelocity.sqrMagnitude > DashVelocityThreshold * DashVelocityThreshold)
                {
                    dashDirection = planarVelocity;
                }
                else
                {
                    dashDirection = currentInput.ReferenceForward;
                }
            }

            dashDirection = Vector3.ProjectOnPlane(dashDirection, Up);
            if (dashDirection.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                dashDirection = Vector3.ProjectOnPlane(Vector3.forward, Up);
            }

            return dashDirection.normalized;
        }

        private void CacheBounceCandidate(Collision collision)
        {
            if (body == null || tuningProfile == null || collision == null)
            {
                return;
            }

            Collider otherCollider = collision.collider;
            if (otherCollider == null || otherCollider == lastConsumedSurface)
            {
                return;
            }

            if (!TryGetBounceSurface(otherCollider, out IBounceSurface surface))
            {
                return;
            }

            if (Vector3.Dot(body.linearVelocity, Up) > LandingVerticalTolerance)
            {
                return;
            }

            ContactPoint bestContact = default;
            float bestGroundDot = float.NegativeInfinity;
            bool hasValidContact = false;

            int contactCount = collision.contactCount;
            for (int index = 0; index < contactCount; index++)
            {
                ContactPoint contact = collision.GetContact(index);
                float groundDot = Vector3.Dot(contact.normal, Up);
                if (groundDot < tuningProfile.MinGroundDot)
                {
                    continue;
                }

                if (!hasValidContact || groundDot > bestGroundDot)
                {
                    bestContact = contact;
                    bestGroundDot = groundDot;
                    hasValidContact = true;
                }
            }

            if (!hasValidContact)
            {
                return;
            }

            float timestamp = Time.time;
            lastSurfaceTouchTime = timestamp;

            if (!hasBounceCandidate || bestGroundDot >= lastBounceCandidate.GroundDot || otherCollider != lastBounceCandidate.Collider)
            {
                lastBounceCandidate = new BounceCandidate(
                    surface,
                    otherCollider,
                    bestContact.point,
                    bestContact.normal,
                    timestamp,
                    bestGroundDot);
                hasBounceCandidate = true;
            }
        }

        private bool ComputeGroundedState()
        {
            return Time.time - lastSurfaceTouchTime <= GroundedContactRetention;
        }

        private static bool TryGetBounceSurface(Collider otherCollider, out IBounceSurface surface)
        {
            MonoBehaviour[] behaviours = otherCollider.GetComponentsInParent<MonoBehaviour>(true);
            for (int index = 0; index < behaviours.Length; index++)
            {
                if (behaviours[index] is IBounceSurface bounceSurface)
                {
                    surface = bounceSurface;
                    return true;
                }
            }

            surface = null;
            return false;
        }

        private readonly struct BounceCandidate
        {
            public BounceCandidate(
                IBounceSurface surface,
                Collider collider,
                Vector3 contactPoint,
                Vector3 contactNormal,
                float timestamp,
                float groundDot)
            {
                Surface = surface;
                Collider = collider;
                ContactPoint = contactPoint;
                ContactNormal = contactNormal;
                Timestamp = timestamp;
                GroundDot = groundDot;
            }

            public IBounceSurface Surface { get; }

            public Collider Collider { get; }

            public Vector3 ContactPoint { get; }

            public Vector3 ContactNormal { get; }

            public float Timestamp { get; }

            public float GroundDot { get; }
        }
    }
}
