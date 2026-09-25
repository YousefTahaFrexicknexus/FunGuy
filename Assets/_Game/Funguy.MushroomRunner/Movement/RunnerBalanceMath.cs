using UnityEngine;

/// <summary>Shared by runtime momentum and the editor balance simulator. No scene state.</summary>
public static class RunnerBalanceMath
{
    public static bool ValidBands(Vector4 caps) => caps.x > 0f && caps.y > caps.x && caps.z > caps.y && caps.w > caps.z
        && !float.IsInfinity(caps.w) && !float.IsNaN(caps.w);

    public static Vector4 Caps(MovementTuningProfile profile) => profile == null ? Vector4.zero : new Vector4(
        profile.GetMaxSpeed(MomentumTier.Low), profile.GetMaxSpeed(MomentumTier.Medium),
        profile.GetMaxSpeed(MomentumTier.High), profile.GetMaxSpeed(MomentumTier.Maximum));

    public static float Decay(float forwardSpeed, MomentumTier tier, Vector4 caps, float strength)
    {
        int index = Mathf.Clamp((int)tier, 0, 3);
        if (index == 0 || !ValidBands(caps)) return 0f;
        float minimum = caps[index - 1];
        float deficit = Mathf.Max(0f, minimum - Mathf.Max(0f, forwardSpeed)) / (caps[index] - minimum);
        return Mathf.Max(0f, strength) * deficit * deficit;
    }

    // Four control points at momentum 0, 33.33, 66.67 and 100.
    // Smoothstep is monotone and has zero slope at each join: no overshoot or tier discontinuities.
    public static float SmoothCurve(float momentum, Vector4 points)
    {
        float x = Mathf.Clamp01(momentum / 100f) * 3f;
        int segment = Mathf.Min(2, Mathf.FloorToInt(x));
        float t = x - segment;
        return Mathf.Lerp(points[segment], points[segment + 1], t * t * (3f - 2f * t));
    }

    public static float SmoothLoss(float momentum, float speed, Vector4 caps, Vector4 upkeep, float strength, float highPressure)
    {
        if (!ValidBands(caps)) return 0f;
        float required = SmoothCurve(momentum, new Vector4(0, caps.x, caps.y, caps.z));
        float width = SmoothCurve(momentum, new Vector4(caps.x, caps.y-caps.x, caps.z-caps.y, caps.w-caps.z));
        float deficit = Mathf.Max(0, required-Mathf.Max(0,speed)) / Mathf.Max(.01f,width);
        float pressure = Mathf.Lerp(1,Mathf.Max(1,highPressure),Mathf.Pow(Mathf.Clamp01(momentum/100f),2));
        return Mathf.Max(0,SmoothCurve(momentum,upkeep)) + Mathf.Max(0,strength)*pressure*deficit*deficit;
    }

    public static MomentumTier ResolveTier(float momentum, MomentumTier previous, Vector3 thresholds,
        float hysteresis, float maximum = 100f)
    {
        int tier = Mathf.Clamp((int)previous, 0, 3);
        while (tier < 3 && momentum >= thresholds[tier]) tier++;
        while (tier > 0)
        {
            int boundary = tier - 1;
            float below = boundary == 0 ? 0f : thresholds[boundary - 1];
            float above = boundary == 2 ? maximum : thresholds[boundary + 1];
            float gap = Mathf.Min(thresholds[boundary] - below, above - thresholds[boundary]);
            float margin = Mathf.Clamp(hysteresis, 0f, Mathf.Max(0f, gap * 0.49f));
            if (momentum >= thresholds[boundary] - margin) break;
            tier--;
        }
        return (MomentumTier)tier;
    }

    public static float BlendCap(float current, float target, float timeConstant, float deltaTime)
    {
        if (deltaTime <= 0f) return current;
        return timeConstant <= 0f ? target : target + (current - target) * Mathf.Exp(-deltaTime / timeConstant);
    }
}
