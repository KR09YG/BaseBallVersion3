using UnityEngine;

public static class BattingPhysics
{
    private const float BALL_MASS_KG = 0.145f;
    private const float EFFECTIVE_BAT_MASS_FACTOR = 0.7f;
    private const float KMH_TO_MS = 1f / 3.6f;
    private const float BALL_RADIUS_M = 0.0366f;
    private const float RPM_TO_RAD_PER_SEC = 2f * Mathf.PI / 60f;

    /// <summary>
    /// 打球初速を計算
    /// </summary>
    public static float CalculateExitVelocity(
        float pitchSpeedMs,
        float batSpeedKmh,
        float batMass,
        float cor,
        float efficiency)
    {
        float batSpeedMs = batSpeedKmh * KMH_TO_MS;
        float effectiveBatMass = batMass * EFFECTIVE_BAT_MASS_FACTOR;

        float e = Mathf.Clamp(cor, 0.4f, 0.6f);

        float numerator =
            (BALL_MASS_KG - e * effectiveBatMass) * pitchSpeedMs +
            effectiveBatMass * (1f + e) * batSpeedMs;

        float denominator = BALL_MASS_KG + effectiveBatMass;

        float baseVelocity = numerator / denominator;
        return baseVelocity * efficiency;
    }

    /// <summary>
    /// インパクト効率を計算
    /// </summary>
    public static float CalculateImpactEfficiency(
        float impactDistance,
        float sweetSpotRadius,
        float maxImpactDistance,
        AnimationCurve efficiencyCurve)
    {
        if (impactDistance <= sweetSpotRadius)
            return 1.0f;

        if (impactDistance >= maxImpactDistance)
            return efficiencyCurve.Evaluate(1.0f);

        float t = Mathf.InverseLerp(sweetSpotRadius, maxImpactDistance, impactDistance);
        return efficiencyCurve.Evaluate(t);
    }

    /// <summary>
    /// 打ち上げ角度を計算
    /// </summary>
    public static float CalculateLaunchAngle(
        float verticalOffset,
        BattingParameters param)
    {
        float normalizedOffset = verticalOffset * 10f;
        float angleOffset = Mathf.Sign(normalizedOffset) *
                           Mathf.Pow(Mathf.Abs(normalizedOffset), param.launchAnglePower) *
                           param.launchAngleScale;

        float launchAngle = param.idealLaunchAngle - angleOffset;
        return Mathf.Clamp(launchAngle, param.minLaunchAngle, param.maxLaunchAngle);
    }

    /// <summary>
    /// 水平角度を計算
    /// </summary>
    public static float CalculateHorizontalAngle(float timing, BattingParameters param)
    {
        if (Mathf.Abs(timing) > param.foulThreshold)
        {
            float excessTiming = (Mathf.Abs(timing) - param.foulThreshold) / (1f - param.foulThreshold);
            float foulAngle = Mathf.Lerp(param.maxFairAngle, param.maxFoulAngle, excessTiming);
            return Mathf.Sign(timing) * foulAngle;
        }

        float normalizedTiming = timing / param.foulThreshold;
        return normalizedTiming * param.maxFairAngle;
    }

    /// <summary>
    /// スピン量を計算
    /// </summary>
    public static float CalculateSpinRate(
        float exitVelocity,
        float launchAngle,
        float efficiency,
        BattingParameters param)
    {
        float velocityFactor = exitVelocity / 40f;
        float angleFactor = 1f + (launchAngle / 30f) * 0.3f;
        float spinRate = param.baseBackspinRPM * velocityFactor * angleFactor * efficiency;

        return Mathf.Clamp(spinRate, param.minSpinRate, param.maxSpinRate);
    }

    /// <summary>
    /// 揚力係数を計算
    /// </summary>
    public static float CalculateLiftCoefficient(
        float spinRateRpm,
        float speedMs,
        BattingParameters param)
    {
        float v = Mathf.Max(5f, speedMs);
        float omega = spinRateRpm * RPM_TO_RAD_PER_SEC;
        float spinRatio = (omega * BALL_RADIUS_M) / v;

        float cl = (param.liftCoefficientA * spinRatio) / (param.liftCoefficientB + spinRatio);
        return Mathf.Clamp(cl, 0f, param.maxLiftCoefficient);
    }

    /// <summary>
    /// 打球方向ベクトルを計算
    /// </summary>
    public static Vector3 CalculateBattedBallDirection(float launchAngle, float horizontalAngle)
    {
        float launchRad = launchAngle * Mathf.Deg2Rad;
        float horizontalRad = horizontalAngle * Mathf.Deg2Rad;

        float x = Mathf.Sin(horizontalRad) * Mathf.Cos(launchRad);
        float y = Mathf.Sin(launchRad);
        float z = -Mathf.Cos(horizontalRad) * Mathf.Cos(launchRad);

        return new Vector3(x, y, z).normalized;
    }

    /// <summary>
    /// スピン軸を計算
    /// </summary>
    public static Vector3 CalculateSpinAxis(Vector3 direction)
    {
        Vector3 horizontalDirection = new Vector3(direction.x, 0, direction.z).normalized;
        Vector3 spinAxis = Vector3.Cross(horizontalDirection, Vector3.up).normalized;

        if (spinAxis.sqrMagnitude < 0.01f)
            spinAxis = Vector3.right;

        return spinAxis;
    }
}