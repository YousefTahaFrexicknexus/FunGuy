using UnityEngine;

public static class LandingAssistMath
{
    public static float InputWeight(float error, float input, float neutralStrength)
    {
        if (Mathf.Abs(error) < 0.0001f) return 0f;
        float alignment = Mathf.Sign(error) * input;
        return alignment < 0f
            ? neutralStrength * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(-alignment / 0.25f)))
            : Mathf.Lerp(neutralStrength, 1f, Mathf.Clamp01(alignment));
    }

    public static Vector2 RequiredAcceleration(Vector2 error, float timeToLand)
    {
        float time = Mathf.Max(0.02f, timeToLand);
        return error * (2f / (time * time));
    }

    public static bool CanReach(Vector2 error, float timeToLand, Vector2 input,
        Vector2 accelerationLimits, Vector2 velocityLimits, Vector2 injectedVelocity, float neutralStrength)
    {
        Vector2 required = RequiredAcceleration(error, timeToLand);
        Vector2 weights = new(InputWeight(error.x, input.x, neutralStrength), InputWeight(error.y, input.y, neutralStrength));
        if (Mathf.Abs(required.x) > accelerationLimits.x * weights.x + 0.0001f
            || Mathf.Abs(required.y) > accelerationLimits.y * weights.y + 0.0001f) return false;
        Vector2 neededVelocity = required * timeToLand + injectedVelocity;
        return Mathf.Abs(neededVelocity.x) <= velocityLimits.x && Mathf.Abs(neededVelocity.y) <= velocityLimits.y;
    }

    public static Vector2 CalculateVelocityStep(Vector2 error, float timeToLand, Vector2 input,
        Vector2 accelerationLimits, Vector2 velocityLimits, Vector2 injectedVelocity,
        float neutralStrength, float response, float deltaTime, ref Vector2 smoothedAcceleration)
    {
        Vector2 weights = new(InputWeight(error.x, input.x, neutralStrength), InputWeight(error.y, input.y, neutralStrength));
        Vector2 limits = Vector2.Scale(accelerationLimits, weights);
        Vector2 required = RequiredAcceleration(error, timeToLand);
        Vector2 desired = new(Mathf.Clamp(required.x, -limits.x, limits.x), Mathf.Clamp(required.y, -limits.y, limits.y));
        smoothedAcceleration = Vector2.MoveTowards(smoothedAcceleration, desired,
            accelerationLimits.magnitude * response * deltaTime);
        // An opposing input cuts the affected axis immediately, including smoothing residue.
        smoothedAcceleration.x = Mathf.Clamp(smoothedAcceleration.x, -limits.x, limits.x);
        smoothedAcceleration.y = Mathf.Clamp(smoothedAcceleration.y, -limits.y, limits.y);
        Vector2 next = injectedVelocity + smoothedAcceleration * deltaTime;
        next.x = Mathf.Clamp(next.x, -velocityLimits.x, velocityLimits.x);
        next.y = Mathf.Clamp(next.y, -velocityLimits.y, velocityLimits.y);
        return next - injectedVelocity;
    }

    public static bool CanReachDownward(float drop, float timeToLand, float accelerationLimit,
        float velocityLimit, float injectedDownwardVelocity)
    {
        float time = Mathf.Max(0.02f, timeToLand);
        float required = 2f * drop / (time * time);
        return drop > 0f && required <= accelerationLimit + 0.0001f
            && required * time + injectedDownwardVelocity <= velocityLimit;
    }

    public static float CalculateDownwardVelocityStep(float drop, float timeToLand, float accelerationLimit,
        float velocityLimit, float injectedDownwardVelocity, float response, float deltaTime,
        ref float smoothedAcceleration)
    {
        if (drop <= 0f || accelerationLimit <= 0f || deltaTime <= 0f)
        {
            smoothedAcceleration = 0f;
            return 0f;
        }
        float time = Mathf.Max(0.02f, timeToLand);
        float desired = Mathf.Min(2f * drop / (time * time), accelerationLimit);
        smoothedAcceleration = Mathf.MoveTowards(smoothedAcceleration, desired,
            accelerationLimit * response * deltaTime);
        smoothedAcceleration = Mathf.Clamp(smoothedAcceleration, 0f, accelerationLimit);
        return Mathf.Min(smoothedAcceleration * deltaTime, Mathf.Max(0f, velocityLimit - injectedDownwardVelocity));
    }
}
