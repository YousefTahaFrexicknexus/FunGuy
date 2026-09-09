using System;
using UnityEngine;

// Owns surface contact bookkeeping. The motor still decides when a bounce changes velocity.
internal sealed class RunnerBounceContacts
{
    const float LandingVerticalTolerance = 0.25f;
    const float GroundedContactRetention = 0.05f;
    readonly RunnerMovementMotor motor;
    Collider[] rigidBodyColliders = Array.Empty<Collider>();
    BounceCandidate lastBounceCandidate;
    bool hasBounceCandidate;
    float lastSurfaceTouchTime = float.NegativeInfinity;
    Collider lastConsumedSurface;
    Collider ignoredBounceSurface;
    float ignoredBounceSurfaceUntil = float.NegativeInfinity;
    Rigidbody rigidBody => motor.rigidBody;
    MovementTuningProfile tuningProfile => motor.TuningProfile;
    Vector3 Up => motor.UpDirection;

    public RunnerBounceContacts(RunnerMovementMotor motor) { this.motor = motor; }

    public bool TryGetCandidate(out BounceCandidate candidate)
    {
        candidate = lastBounceCandidate;
        if (!hasBounceCandidate || tuningProfile == null) return false;
        if (candidate.Collider == null || candidate.Surface == null ||
            Time.time - candidate.Timestamp > tuningProfile.BounceGraceTime)
        {
            hasBounceCandidate = false;
            return false;
        }
        return candidate.Collider != lastConsumedSurface;
    }

    public void MarkConsumed(Collider surface)
    {
        lastConsumedSurface = surface;
        hasBounceCandidate = false;
        lastSurfaceTouchTime = float.NegativeInfinity;
    }

    public void ResetMotion()
    {
        MarkConsumed(null);
        lastBounceCandidate = default;
        RestoreIgnoredBounceSurface();
    }

    public void OnCollisionExit(Collision collision)
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

    public void CacheBounceCandidate(Collision collision)
    {
        if (rigidBody == null || tuningProfile == null || collision == null)
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

        if (Vector3.Dot(rigidBody.linearVelocity, Up) > LandingVerticalTolerance && !AllowsBounceWhileMovingUpward(surface))
        {
            return;
        }

        Vector3 bestContactPoint;
        Vector3 bestContactNormal;
        float bestGroundDot;
        bool hasValidContact = TryResolveBounceContact(collision, surface, out bestContactPoint, out bestContactNormal, out bestGroundDot);

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
                bestContactPoint,
                bestContactNormal,
                timestamp,
                bestGroundDot);
            hasBounceCandidate = true;
        }
    }

    bool TryResolveBounceContact(
        Collision collision,
        IBounceSurface surface,
        out Vector3 contactPoint,
        out Vector3 contactNormal,
        out float groundDot)
    {
        if (surface is IBounceContactResolver contactResolver &&
            contactResolver.TryResolveBounceContact(collision, Up, tuningProfile.MinGroundDot, out contactPoint, out contactNormal, out groundDot))
        {
            return true;
        }

        contactPoint = default;
        contactNormal = default;
        groundDot = float.NegativeInfinity;

        ContactPoint bestContact = default;
        bool hasValidContact = false;
        int contactCount = collision.contactCount;
        for (int index = 0; index < contactCount; index++)
        {
            ContactPoint contact = collision.GetContact(index);
            float currentGroundDot = Vector3.Dot(contact.normal, Up);
            if (currentGroundDot < tuningProfile.MinGroundDot)
            {
                continue;
            }

            if (!hasValidContact || currentGroundDot > groundDot)
            {
                bestContact = contact;
                groundDot = currentGroundDot;
                hasValidContact = true;
            }
        }

        if (!hasValidContact)
        {
            return false;
        }

        contactPoint = bestContact.point;
        contactNormal = bestContact.normal;
        return true;
    }

    public bool ComputeGroundedState()
    {
        return Time.time - lastSurfaceTouchTime <= GroundedContactRetention;
    }

    bool AllowsBounceWhileMovingUpward(IBounceSurface surface)
    {
        return surface is IBounceSurfaceBehavior behavior && behavior.AllowsBounceWhileMovingUpward;
    }

    public void ApplyPostBounceCollisionIgnore(IBounceSurface surface, Collider surfaceCollider)
    {
        if (surfaceCollider == null || rigidBodyColliders == null || rigidBodyColliders.Length == 0)
        {
            return;
        }

        float ignoreDuration = surface is IBounceSurfaceBehavior behavior
            ? Mathf.Max(0f, behavior.PostBounceCollisionIgnoreDuration)
            : 0f;

        if (ignoreDuration <= 0f)
        {
            return;
        }

        if (ignoredBounceSurface != null && ignoredBounceSurface != surfaceCollider)
        {
            RestoreIgnoredBounceSurface();
        }

        ignoredBounceSurface = surfaceCollider;
        ignoredBounceSurfaceUntil = Mathf.Max(ignoredBounceSurfaceUntil, Time.time + ignoreDuration);

        for (int index = 0; index < rigidBodyColliders.Length; index++)
        {
            Collider rigidBodyCollider = rigidBodyColliders[index];
            if (rigidBodyCollider == null)
            {
                continue;
            }

            Physics.IgnoreCollision(rigidBodyCollider, surfaceCollider, true);
        }
    }

    public void RestoreIgnoredBounceSurfaceIfExpired()
    {
        if (ignoredBounceSurface == null || Time.time < ignoredBounceSurfaceUntil)
        {
            return;
        }

        RestoreIgnoredBounceSurface();
    }

    public void RestoreIgnoredBounceSurface()
    {
        if (ignoredBounceSurface == null || rigidBodyColliders == null)
        {
            ignoredBounceSurface = null;
            ignoredBounceSurfaceUntil = float.NegativeInfinity;
            return;
        }

        for (int index = 0; index < rigidBodyColliders.Length; index++)
        {
            Collider rigidBodyCollider = rigidBodyColliders[index];
            if (rigidBodyCollider == null)
            {
                continue;
            }

            Physics.IgnoreCollision(rigidBodyCollider, ignoredBounceSurface, false);
        }

        ignoredBounceSurface = null;
        ignoredBounceSurfaceUntil = float.NegativeInfinity;
    }

    public void CacheBodyColliders()
    {
        rigidBodyColliders = motor.GetComponentsInChildren<Collider>(true);
    }

    static bool TryGetBounceSurface(Collider otherCollider, out IBounceSurface surface)
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

    public readonly struct BounceCandidate
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
