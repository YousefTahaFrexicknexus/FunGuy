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
        Vector2 weights = new(InputWeight(error.x, input.x, neutralStrength), InputWeight(error.y, input.y, neutralStrength));
        return CanReachAxis(error.x, timeToLand, accelerationLimits.x * weights.x, velocityLimits.x, injectedVelocity.x)
            && CanReachAxis(error.y, timeToLand, accelerationLimits.y * weights.y, velocityLimits.y, injectedVelocity.y);
    }

    // The prediction already contains previously injected velocity. Only additional
    // displacement is needed: accelerate until the remaining speed budget, then coast.
    public static bool CanReachAxis(float error, float time, float acceleration, float velocityLimit, float injectedVelocity)
    {
        if (Mathf.Abs(error) < 0.0001f) return true;
        if (time <= 0f || acceleration <= 0f) return false;
        float remainingSpeed = Mathf.Max(0f, velocityLimit - Mathf.Sign(error) * injectedVelocity);
        float acceleratingTime = Mathf.Min(time, remainingSpeed / acceleration);
        float distance = acceleration * acceleratingTime * (time - 0.5f * acceleratingTime);
        return Mathf.Abs(error) <= distance + 0.0001f;
    }

    static float RequiredCappedAcceleration(float error, float timeToLand, float remainingSpeed)
    {
        float time = Mathf.Max(0.02f, timeToLand);
        float distance = Mathf.Abs(error);
        if (distance < 0.0001f || remainingSpeed <= 0f) return 0f;
        // Constant acceleration suffices until it would exceed the speed budget.
        // Otherwise accelerate sooner, hit the budget, then coast to the landing.
        float acceleration = 2f * distance / (time * time);
        if (acceleration * time > remainingSpeed)
            acceleration = remainingSpeed * remainingSpeed / (2f * Mathf.Max(0.0001f, remainingSpeed * time - distance));
        return Mathf.Sign(error) * acceleration;
    }

    public static Vector2 CalculateVelocityStep(Vector2 error, float timeToLand, Vector2 input,
        Vector2 accelerationLimits, Vector2 velocityLimits, Vector2 injectedVelocity,
        float neutralStrength, float response, float deltaTime, ref Vector2 smoothedAcceleration)
    {
        Vector2 weights = new(InputWeight(error.x, input.x, neutralStrength), InputWeight(error.y, input.y, neutralStrength));
        Vector2 limits = Vector2.Scale(accelerationLimits, weights);
        Vector2 required = new(
            RequiredCappedAcceleration(error.x, timeToLand, Mathf.Max(0f, velocityLimits.x - Mathf.Sign(error.x) * injectedVelocity.x)),
            RequiredCappedAcceleration(error.y, timeToLand, Mathf.Max(0f, velocityLimits.y - Mathf.Sign(error.y) * injectedVelocity.y)));
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
        return drop > 0f && CanReachAxis(drop, timeToLand, accelerationLimit, velocityLimit, injectedDownwardVelocity);
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
        float desired = Mathf.Min(RequiredCappedAcceleration(drop, timeToLand,
            Mathf.Max(0f, velocityLimit - injectedDownwardVelocity)), accelerationLimit);
        smoothedAcceleration = Mathf.MoveTowards(smoothedAcceleration, desired,
            accelerationLimit * response * deltaTime);
        smoothedAcceleration = Mathf.Clamp(smoothedAcceleration, 0f, accelerationLimit);
        return Mathf.Min(smoothedAcceleration * deltaTime, Mathf.Max(0f, velocityLimit - injectedDownwardVelocity));
    }
}
