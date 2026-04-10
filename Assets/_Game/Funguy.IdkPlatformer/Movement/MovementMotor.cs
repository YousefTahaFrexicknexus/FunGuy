using System;
using UnityEngine;

namespace Funguy.IdkPlatformer
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class MovementMotor : MonoBehaviour
    {
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
        private float lowControlUntil = float.NegativeInfinity;
        private float dashControlBoostUntil = float.NegativeInfinity;
        private Collider lastConsumedSurface;
        private Func<bool> tryConsumeDashHandler;
        private bool isGrounded;

        public event Action<BounceEventData> Bounced;
        public event Action Dashed;

        public Vector3 Velocity => body != null ? body.linearVelocity : Vector3.zero;

        public MovementTuningProfile TuningProfile => tuningProfile;

        public bool IsGrounded => isGrounded;

        public Vector3 UpDirection => Up;

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

        public void ResetMotion(Vector3 worldPosition, Quaternion worldRotation)
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody>();
            }

            if (body == null)
            {
                return;
            }

            hasBounceCandidate = false;
            lastBounceCandidate = default;
            lastSurfaceTouchTime = float.NegativeInfinity;
            bufferedDashUntil = float.NegativeInfinity;
            lowControlUntil = float.NegativeInfinity;
            dashControlBoostUntil = float.NegativeInfinity;
            lastConsumedSurface = null;
            isGrounded = false;
            currentInput = MovementInputFrame.Empty;

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            transform.SetPositionAndRotation(worldPosition, worldRotation);
            body.position = worldPosition;
            body.rotation = worldRotation;
            Physics.SyncTransforms();
            body.WakeUp();
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
            BounceMovementMath.ApplyShapedGravity(ref velocity, tuningProfile, Up, deltaTime);
        }

        private void ApplyAirAcceleration(ref Vector3 velocity, MovementInputFrame inputFrame, float deltaTime)
        {
            BounceMovementMath.ApplyAirAcceleration(
                ref velocity,
                tuningProfile,
                inputFrame,
                Up,
                Time.time < lowControlUntil && Time.time >= dashControlBoostUntil,
                Time.time < dashControlBoostUntil,
                deltaTime);
        }

        private void ApplyPlanarDrag(ref Vector3 velocity, float drag, float deltaTime)
        {
            BounceMovementMath.ApplyPlanarDrag(ref velocity, Up, drag, deltaTime);
        }

        private void ApplySoftSpeedLimit(ref Vector3 velocity, float deltaTime)
        {
            BounceMovementMath.ApplySoftSpeedLimit(ref velocity, tuningProfile, Up, deltaTime);
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
            lowControlUntil = Time.time + tuningProfile.PostBounceLowControlTime;
            isGrounded = false;
            return true;
        }

        private Vector3 ApplyBounceResponse(Vector3 incomingVelocity, BounceSurfaceResponse response)
        {
            return BounceMovementMath.ApplyBounceResponse(incomingVelocity, response, Up);
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

            Vector3 dashDirection = ResolveDashDirection();
            if (dashDirection.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                bufferedDashUntil = float.NegativeInfinity;
                return false;
            }

            velocity += dashDirection * tuningProfile.DashForce;
            bufferedDashUntil = float.NegativeInfinity;
            dashControlBoostUntil = Time.time + tuningProfile.PostDashBonusControlTime;
            Dashed?.Invoke();
            return true;
        }

        private Vector3 ResolveDashDirection()
        {
            return Up;
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
