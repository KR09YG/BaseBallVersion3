using System.Collections.Generic;
using UnityEngine;

public static class PitchPhysicsCalculator
{
    // === 物理定数 ===
    private const float AIR_DENSITY = 1.225f;
    private const float BALL_MASS = 0.145f;
    private const float BALL_RADIUS = 0.0366f;
    private const float CROSS_SECTION = Mathf.PI * BALL_RADIUS * BALL_RADIUS;
    private const float DRAG_COEFFICIENT = 0.3f;
    private const float GRAVITY_HALF = 0.5f;  // 重力計算での1/2係数
    private const float MAGNUS_FORCE_HALF = 0.5f;  // マグヌス力計算での1/2係数
    private const float DRAG_FORCE_HALF = 0.5f;  // 抗力計算での1/2係数

    // === 単位変換定数 ===
    private const float RPM_TO_RAD_PER_SEC = 2f * Mathf.PI / 60f;  // RPM → rad/s

    // === マグヌス効果補正 ===
    private const float MAGNUS_VERTICAL_CORRECTION_FACTOR = 0.8f;

    // === 最適化パラメータ ===
    private const int MAX_OPTIMIZATION_ITERATIONS = 20;  // 最大反復回数
    private const float POSITION_TOLERANCE = 0.02f;  // 位置の許容誤差 (2cm)
    private const float Z_POSITION_TOLERANCE = 0.01f;  // Z方向の厳密な許容誤差 (1cm)
    private const float Z_TOLERANCE_FACTOR = 0.5f;  // Z方向の収束判定係数
    private const float Z_ERROR_WEIGHT = 2f;  // Z方向の誤差重み (2倍重視)

    // === 速度調整パラメータ ===
    private const float Z_ADJUSTMENT_INITIAL = 0.8f;  // Z方向調整の初期係数
    private const float Z_ADJUSTMENT_FINAL = 0.3f;  // Z方向調整の最終係数
    private const float XY_ADJUSTMENT_INITIAL = 0.6f;  // XY方向調整の初期係数
    private const float XY_ADJUSTMENT_FINAL = 0.2f;  // XY方向調整の最終係数
    private const float SPEED_ERROR_THRESHOLD = 0.2f;  // 速度誤差の調整閾値 (20%)
    private const float SPEED_ADJUSTMENT_INITIAL = 0.15f;  // 速度調整の初期係数
    private const float SPEED_ADJUSTMENT_FINAL = 0.05f;  // 速度調整の最終係数

    // === 初速推定パラメータ ===
    private const float DRAG_FACTOR_BASE = 1.0f;  // 空気抵抗補正の基本値
    private const float DRAG_MASS_FACTOR = 2f;  // 抗力計算での質量係数

    // === シミュレーション終了条件 ===
    private const float GROUND_LEVEL = -0.5f;  // 地面到達判定
    private const int MAX_TRAJECTORY_POINTS = 1000;  // 最大軌道点数

    // === 物理計算の閾値 ===
    private const float MIN_VELOCITY_SQUARED = 0.01f;  // マグヌス力計算の最小速度²
    private const float MIN_MAGNUS_DIRECTION_SQUARED = 0.0001f;  // マグヌス方向の最小値²
    private const float MIN_DRAG_VELOCITY = 0.001f;  // 抗力計算の最小速度

    /// <summary>
    /// 投球軌道を計算
    /// </summary>
    public static List<Vector3> CalculateTrajectory(PitchParameters parameters)
    {
        Debug.Log("========== 軌道計算開始 ==========");

        // 終点に到達する最適な初速を探索
        Vector3 optimalVelocity = FindOptimalVelocityAdvanced(
            parameters.ReleasePoint,
            parameters.TargetPoint,
            parameters.SpinAxis.normalized,
            parameters.SpinRate,
            parameters.LiftCoefficient,
            parameters.Velocity,
            parameters.Settings
        );

        // 最適な初速で最終的な軌道を計算
        List<Vector3> trajectory = SimulateTrajectory(
            parameters.ReleasePoint,
            optimalVelocity,
            parameters.SpinAxis.normalized,
            parameters.SpinRate,
            parameters.LiftCoefficient,
            parameters.Settings
        );

        if (trajectory.Count > 0)
        {
            Vector3 endPoint = trajectory[trajectory.Count - 1];
            float error = Vector3.Distance(endPoint, parameters.TargetPoint);
            Debug.Log($"[最終] 終点: {endPoint}, 誤差: {error * 100f:F2}cm");
        }

        Debug.Log("========== 軌道計算完了 ==========");
        return trajectory;
    }

    /// <summary>
    /// 終点に到達する最適な初速を探索
    /// </summary>
    private static Vector3 FindOptimalVelocityAdvanced(
        Vector3 startPoint,
        Vector3 targetPoint,
        Vector3 spinAxisNormalized,
        float spinRateRPM,
        float liftCoefficient,
        float desiredSpeed,
        TrajectorySettings settings)
    {
        Debug.Log("[最適化] 開始");

        // 初速の初期推定
        Vector3 currentVelocity = EstimateInitialVelocityImproved(
            startPoint,
            targetPoint,
            spinAxisNormalized,
            spinRateRPM,
            liftCoefficient,
            desiredSpeed
        );

        Vector3 bestVelocity = currentVelocity;
        float bestError = float.MaxValue;

        // 誤差が一定以下になるもしくは最大反復に達するまで繰り返し
        for (int i = 0; i < MAX_OPTIMIZATION_ITERATIONS; i++)
        {
            // テスト軌道を計算
            List<Vector3> testTrajectory = SimulateTrajectory(
                startPoint,
                currentVelocity,
                spinAxisNormalized,
                spinRateRPM,
                liftCoefficient,
                settings
            );

            if (testTrajectory.Count == 0)
            {
                Debug.LogWarning("[最適化] 軌道計算失敗");
                break;
            }

            // 終点の誤差を計算
            Vector3 endPoint = testTrajectory[testTrajectory.Count - 1];
            Vector3 error = targetPoint - endPoint;

            // Z座標（距離方向）の誤差を重視
            float errorZ = Mathf.Abs(error.z);
            float errorXY = new Vector2(error.x, error.y).magnitude;
            float totalError = errorZ * Z_ERROR_WEIGHT + errorXY;

            // より良い結果を保存
            if (totalError < bestError)
            {
                bestError = totalError;
                bestVelocity = currentVelocity;
            }

            Debug.Log($"[最適化] 反復 {i + 1}: 誤差 XY={errorXY * 100f:F2}cm, Z={errorZ * 100f:F2}cm, 総合={totalError * 100f:F2}");

            // 収束判定（Z方向を特に重視）
            if (errorZ < POSITION_TOLERANCE * Z_TOLERANCE_FACTOR && errorXY < POSITION_TOLERANCE)
            {
                Debug.Log($"[最適化] 収束成功！反復: {i + 1}回");
                return currentVelocity;
            }

            // 速度の調整（適応的な減衰係数）
            float progress = (float)i / MAX_OPTIMIZATION_ITERATIONS;

            // Z方向の調整（距離が足りない/行き過ぎ）
            if (errorZ > Z_POSITION_TOLERANCE)
            {
                // Z方向の速度を増やす
                float zAdjustment = error.z * Mathf.Lerp(Z_ADJUSTMENT_INITIAL, Z_ADJUSTMENT_FINAL, progress);
                currentVelocity.z += zAdjustment;
            }

            // XY方向の調整（横・高さのズレ）
            Vector3 xyAdjustment = new Vector3(error.x, error.y, 0) *
                                   Mathf.Lerp(XY_ADJUSTMENT_INITIAL, XY_ADJUSTMENT_FINAL, progress);
            currentVelocity += xyAdjustment;

            // 速度の大きさを目標に近づける（方向は自由）
            float currentSpeed = currentVelocity.magnitude;
            float speedError = desiredSpeed - currentSpeed;

            // 速度が大きくズレている場合のみ調整
            if (Mathf.Abs(speedError) > desiredSpeed * SPEED_ERROR_THRESHOLD)
            {
                float speedAdjustmentFactor = Mathf.Lerp(SPEED_ADJUSTMENT_INITIAL, SPEED_ADJUSTMENT_FINAL, progress);
                currentVelocity = currentVelocity.normalized *
                                 Mathf.Lerp(currentSpeed, desiredSpeed, speedAdjustmentFactor);
            }

            Debug.Log($"[最適化] 調整後の初速: {currentVelocity}, 速度: {currentVelocity.magnitude:F2} m/s");
        }

        Debug.Log($"[最適化] 最大反復到達。最良誤差: {bestError * 100f:F2}cm");
        return bestVelocity;
    }

    /// <summary>
    /// マグヌス効果、空気抵抗を考慮して、目標に到達する初速を推定
    /// </summary>
    private static Vector3 EstimateInitialVelocityImproved(
        Vector3 startPoint,
        Vector3 targetPoint,
        Vector3 spinAxisNormalized,
        float spinRateRPM,
        float liftCoefficient,
        float desiredSpeed)
    {
        // 目標までのベクトル
        Vector3 displacement = targetPoint - startPoint;
        // 水平距離と垂直距離に分解
        float horizontalDist = new Vector2(displacement.x, displacement.z).magnitude;
        float verticalDist = displacement.y;

        // 空気抵抗によって生じる速度減衰の補正係数
        float dragFactor = DRAG_FACTOR_BASE +
                          (DRAG_COEFFICIENT * AIR_DENSITY * CROSS_SECTION * desiredSpeed) /
                          (DRAG_MASS_FACTOR * BALL_MASS);
        // 上の補正係数を使用し、飛行時間を推定
        float estimatedTime = (horizontalDist / desiredSpeed) * dragFactor;

        Debug.Log($"[初速推定] 距離: {horizontalDist:F2}m, 推定時間: {estimatedTime:F3}s");

        float gravity = Mathf.Abs(Physics.gravity.y);
        // 等加速度運動の公式: h = 1/2 × g × t²
        float gravityDrop = GRAVITY_HALF * gravity * estimatedTime * estimatedTime;

        // マグヌス効果の推定
        // 角速度の計算
        float angularVelocity = spinRateRPM * RPM_TO_RAD_PER_SEC;

        // ボールの進行方向
        Vector3 forwardDir = displacement.normalized;
        // 回転軸
        Vector3 spinVector = spinAxisNormalized * angularVelocity;
        // ボールの進行方向と回転軸からマグヌス力の方向を計算
        Vector3 magnusDir = Vector3.Cross(spinVector, forwardDir).normalized;

        // マグヌス力による加速度 (F = 1/2 × ρ × v² × A × Cl)
        float magnusAccel = MAGNUS_FORCE_HALF * AIR_DENSITY * desiredSpeed * desiredSpeed
                           * CROSS_SECTION * liftCoefficient / BALL_MASS;

        // estimatedTime秒間でマグヌス力によって曲がる距離
        float magnusDisplacement = GRAVITY_HALF * magnusAccel * estimatedTime * estimatedTime;

        Debug.Log($"[初速推定] 重力落下: {gravityDrop:F3}m, マグヌス変位: {magnusDisplacement:F3}m");
        Debug.Log($"[初速推定] マグヌス方向: {magnusDir}");

        // 水平成分（XZ平面）
        Vector3 horizontalDir = new Vector3(displacement.x, 0, displacement.z).normalized;

        // Z方向の速度（目標距離を確実に到達）
        float zSpeed = displacement.z / estimatedTime;

        // X方向の速度
        float xSpeed = displacement.x / estimatedTime;

        // 垂直速度（重力とマグヌス効果を考慮）
        float verticalSpeed = verticalDist / estimatedTime + gravity * estimatedTime * GRAVITY_HALF;

        // マグヌス効果の補正
        // 下向きのマグヌス力がある場合、さらに上向きに投げる
        float magnusVerticalEffect = magnusDir.y * magnusDisplacement / estimatedTime;
        verticalSpeed -= magnusVerticalEffect * MAGNUS_VERTICAL_CORRECTION_FACTOR;

        Debug.Log($"[初速推定] マグヌス垂直補正: {-magnusVerticalEffect * MAGNUS_VERTICAL_CORRECTION_FACTOR:F2} m/s");

        // 初速ベクトルを組み立て
        Vector3 initialVelocity = new Vector3(xSpeed, verticalSpeed, zSpeed);

        Debug.Log($"[初速推定] 初速: {initialVelocity}, 速度: {initialVelocity.magnitude:F2} m/s");
        Debug.Log($"[初速推定] 目標速度: {desiredSpeed:F2} m/s, 差: {(initialVelocity.magnitude - desiredSpeed):F2} m/s");

        return initialVelocity;
    }

    /// <summary>
    /// 物理シミュレーション
    /// </summary>
    public static List<Vector3> SimulateTrajectory(
        Vector3 startPosition,
        Vector3 initialVelocity,
        Vector3 spinAxisNormalized,
        float spinRateRPM,
        float liftCoefficient,
        TrajectorySettings settings)
    {
        List<Vector3> points = new List<Vector3>();
        Vector3 position = startPosition;
        Vector3 velocity = initialVelocity;

        float angularVelocity = spinRateRPM * RPM_TO_RAD_PER_SEC;
        float currentTime = 0f;

        points.Add(position);

        float targetZ = settings.StopAtTarget ? settings.StopPosition.z : float.MaxValue;
        
        // 受ける重力の計算
        Vector3 gravityForce = Physics.gravity * BALL_MASS;

        // 終点まで軌道をシミュレーション
        while (currentTime < settings.MaxSimulationTime)
        {
            // マグヌス力の計算
            Vector3 magnusForce = CalculateMagnusForce(velocity, spinAxisNormalized, angularVelocity, liftCoefficient);
            // 空気抵抗の計算
            Vector3 dragForce = CalculateDragForce(velocity);

            Vector3 totalForce = magnusForce + dragForce + gravityForce;
            // 加速度の計算
            Vector3 acceleration = totalForce / BALL_MASS;

            velocity += acceleration * settings.DeltaTime;
            position += velocity * settings.DeltaTime;
            currentTime += settings.DeltaTime;

            points.Add(position);

            // Z座標が目標を超えたら終了
            if (settings.StopAtTarget && position.z >= targetZ)
            {
                Debug.Log($"[シミュレーション] Z座標到達: {position.z:F3} >= {targetZ:F3}");
                break;
            }

            // 地面に落ちたら終了
            if (position.y < GROUND_LEVEL)
            {
                Debug.Log($"[シミュレーション] 地面到達: Y={position.y:F3}");
                break;
            }

            if (points.Count > MAX_TRAJECTORY_POINTS)
            {
                Debug.LogWarning("[シミュレーション] 軌道点が1000を超えました");
                break;
            }
        }

        return points;
    }

    /// <summary>
    /// マグヌス力を計算
    /// </summary>
    /// <param name="velocity"></param>
    /// <param name="spinAxisNorm"></param>
    /// <param name="angularVelocity"></param>
    /// <param name="liftCoeff"></param>
    /// <returns></returns>
    private static Vector3 CalculateMagnusForce(
        Vector3 velocity,
        Vector3 spinAxisNorm,
        float angularVelocity,
        float liftCoeff)
    {
        if (velocity.sqrMagnitude < MIN_VELOCITY_SQUARED)
            return Vector3.zero;

        Vector3 spinVector = spinAxisNorm * angularVelocity;
        Vector3 magnusDirection = Vector3.Cross(spinVector, velocity);

        if (magnusDirection.sqrMagnitude < MIN_MAGNUS_DIRECTION_SQUARED)
            return Vector3.zero;

        magnusDirection.Normalize();

        float velocityMagnitude = velocity.magnitude;
        float magnusForceMagnitude = MAGNUS_FORCE_HALF * AIR_DENSITY
            * velocityMagnitude * velocityMagnitude
            * CROSS_SECTION
            * liftCoeff;

        return magnusDirection * magnusForceMagnitude;
    }

    private static Vector3 CalculateDragForce(Vector3 velocity)
    {
        float velocityMagnitude = velocity.magnitude;
        if (velocityMagnitude < MIN_DRAG_VELOCITY) return Vector3.zero;

        float dragMagnitude = DRAG_FORCE_HALF * AIR_DENSITY
            * velocityMagnitude * velocityMagnitude
            * CROSS_SECTION
            * DRAG_COEFFICIENT;

        return -velocity.normalized * dragMagnitude;
    }
}