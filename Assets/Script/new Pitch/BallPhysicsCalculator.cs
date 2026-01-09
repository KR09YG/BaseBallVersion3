using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 野球ボールの物理計算（共通）- 投球、打撃、バウンドすべて対応
/// </summary>
public static class BallPhysicsCalculator
{
    // === 物理定数 ===
    private const float AIR_DENSITY = 1.225f;
    private const float BALL_MASS = 0.145f;
    private const float BALL_RADIUS = 0.0366f;
    private const float CROSS_SECTION = Mathf.PI * BALL_RADIUS * BALL_RADIUS;
    private const float DRAG_COEFFICIENT = 0.3f;
    private const float GRAVITY_HALF = 0.5f;
    private const float MAGNUS_FORCE_HALF = 0.5f;
    private const float DRAG_FORCE_HALF = 0.5f;

    // === 単位変換定数 ===
    private const float RPM_TO_RAD_PER_SEC = 2f * Mathf.PI / 60f;

    // === マグヌス効果補正 ===
    private const float MAGNUS_VERTICAL_CORRECTION_FACTOR = 0.8f;

    // === 最適化パラメータ ===
    private const int MAX_OPTIMIZATION_ITERATIONS = 20;
    private const float POSITION_TOLERANCE = 0.02f;
    private const float Z_POSITION_TOLERANCE = 0.01f;
    private const float Z_TOLERANCE_FACTOR = 0.5f;
    private const float Z_ERROR_WEIGHT = 2f;

    // === 速度調整パラメータ ===
    private const float Z_ADJUSTMENT_INITIAL = 0.8f;
    private const float Z_ADJUSTMENT_FINAL = 0.3f;
    private const float XY_ADJUSTMENT_INITIAL = 0.6f;
    private const float XY_ADJUSTMENT_FINAL = 0.2f;
    private const float SPEED_ERROR_THRESHOLD = 0.2f;
    private const float SPEED_ADJUSTMENT_INITIAL = 0.15f;
    private const float SPEED_ADJUSTMENT_FINAL = 0.05f;

    // === 初速推定パラメータ ===
    private const float DRAG_FACTOR_BASE = 1.0f;
    private const float DRAG_MASS_FACTOR = 2f;

    // === シミュレーション終了条件 ===
    private const float GROUND_LEVEL = -0.5f;
    private const int MAX_TRAJECTORY_POINTS = 10000;

    // === 物理計算の閾値 ===
    private const float MIN_VELOCITY_SQUARED = 0.01f;
    private const float MIN_MAGNUS_DIRECTION_SQUARED = 0.0001f;
    private const float MIN_DRAG_VELOCITY = 0.001f;

    /// <summary>
    /// 軌道シミュレーション設定
    /// </summary>
    public struct SimulationConfig
    {
        public float DeltaTime;
        public float MaxSimulationTime;
        public float? StopAtZ;
        public BounceSettings BounceSettings;
    }

    /// <summary>
    /// 投球パラメータから軌道を計算（バウンド対応・最適化付き）
    /// </summary>
    public static List<Vector3> CalculateTrajectory(
        PitchParameters parameters,
        BounceSettings bounceSettings = null)
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
            parameters.Settings,
            bounceSettings
        );

        // 最適な初速で最終的な軌道を計算
        var config = new SimulationConfig
        {
            DeltaTime = parameters.Settings?.DeltaTime ?? 0.01f,
            MaxSimulationTime = parameters.Settings?.MaxSimulationTime ?? 5f,
            StopAtZ = parameters.Settings?.StopAtTarget == true ? parameters.Settings.StopPosition.z : (float?)null,
            BounceSettings = bounceSettings
        };

        List<Vector3> trajectory = SimulateTrajectory(
            parameters.ReleasePoint,
            optimalVelocity,
            parameters.SpinAxis.normalized,
            parameters.SpinRate,
            parameters.LiftCoefficient,
            config
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
        TrajectorySettings settings,
        BounceSettings bounceSettings)
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
            var config = new SimulationConfig
            {
                DeltaTime = settings?.DeltaTime ?? 0.01f,
                MaxSimulationTime = settings?.MaxSimulationTime ?? 5f,
                StopAtZ = settings?.StopAtTarget == true ? settings.StopPosition.z : (float?)null,
                BounceSettings = bounceSettings
            };

            List<Vector3> testTrajectory = SimulateTrajectory(
                startPoint,
                currentVelocity,
                spinAxisNormalized,
                spinRateRPM,
                liftCoefficient,
                config
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
        Vector3 displacement = targetPoint - startPoint;
        float horizontalDist = new Vector2(displacement.x, displacement.z).magnitude;
        float verticalDist = displacement.y;

        float dragFactor = DRAG_FACTOR_BASE +
                          (DRAG_COEFFICIENT * AIR_DENSITY * CROSS_SECTION * desiredSpeed) /
                          (DRAG_MASS_FACTOR * BALL_MASS);
        float estimatedTime = (horizontalDist / desiredSpeed) * dragFactor;

        Debug.Log($"[初速推定] 距離: {horizontalDist:F2}m, 推定時間: {estimatedTime:F3}s");

        float gravity = Mathf.Abs(Physics.gravity.y);
        float gravityDrop = GRAVITY_HALF * gravity * estimatedTime * estimatedTime;

        // マグヌス効果の推定
        float angularVelocity = spinRateRPM * RPM_TO_RAD_PER_SEC;
        Vector3 forwardDir = displacement.normalized;
        Vector3 spinVector = spinAxisNormalized * angularVelocity;
        Vector3 magnusDir = Vector3.Cross(spinVector, forwardDir).normalized;

        float magnusAccel = MAGNUS_FORCE_HALF * AIR_DENSITY * desiredSpeed * desiredSpeed
                           * CROSS_SECTION * liftCoefficient / BALL_MASS;
        float magnusDisplacement = GRAVITY_HALF * magnusAccel * estimatedTime * estimatedTime;

        Debug.Log($"[初速推定] 重力落下: {gravityDrop:F3}m, マグヌス変位: {magnusDisplacement:F3}m");

        float zSpeed = displacement.z / estimatedTime;
        float xSpeed = displacement.x / estimatedTime;
        float verticalSpeed = verticalDist / estimatedTime + gravity * estimatedTime * GRAVITY_HALF;

        float magnusVerticalEffect = magnusDir.y * magnusDisplacement / estimatedTime;
        verticalSpeed -= magnusVerticalEffect * MAGNUS_VERTICAL_CORRECTION_FACTOR;

        Vector3 initialVelocity = new Vector3(xSpeed, verticalSpeed, zSpeed);

        Debug.Log($"[初速推定] 初速: {initialVelocity}, 速度: {initialVelocity.magnitude:F2} m/s");

        return initialVelocity;
    }

    /// <summary>
    /// 物理シミュレーション（統一版・バウンド対応）
    /// </summary>
    public static List<Vector3> SimulateTrajectory(
        Vector3 startPosition,
        Vector3 initialVelocity,
        Vector3 spinAxisNormalized,
        float spinRateRPM,
        float liftCoefficient,
        SimulationConfig config)
    {
        List<Vector3> points = new List<Vector3>();
        Vector3 position = startPosition;
        Vector3 velocity = initialVelocity;

        float angularVelocity = spinRateRPM * RPM_TO_RAD_PER_SEC;
        float currentTime = 0f;
        int bounceCount = 0;
        bool isRolling = false;

        Vector3 gravityForce = Physics.gravity * BALL_MASS;
        points.Add(position);

        while (currentTime < config.MaxSimulationTime)
        {
            // === 転がり状態の処理 ===
            if (isRolling && config.BounceSettings != null)
            {
                position = SimulateRolling(position, ref velocity, config.BounceSettings, config.DeltaTime);
                currentTime += config.DeltaTime;
                points.Add(position);

                if (velocity.magnitude < config.BounceSettings.stopVelocityThreshold)
                {
                    break;
                }
                continue;
            }

            // === 空中での物理計算 ===
            Vector3 magnusForce = CalculateMagnusForce(velocity, spinAxisNormalized, angularVelocity, liftCoefficient);
            Vector3 dragForce = CalculateDragForce(velocity);
            Vector3 totalForce = magnusForce + dragForce + gravityForce;
            Vector3 acceleration = totalForce / BALL_MASS;

            velocity += acceleration * config.DeltaTime;
            Vector3 nextPosition = position + velocity * config.DeltaTime;

            // === Z座標停止判定（投球用） ===
            if (config.StopAtZ.HasValue && nextPosition.z >= config.StopAtZ.Value && position.z < config.StopAtZ.Value)
            {
                float t = (config.StopAtZ.Value - position.z) / (nextPosition.z - position.z);
                position = Vector3.Lerp(position, nextPosition, t);
                points.Add(position);
                Debug.Log($"[シミュレーション] Z座標到達: {position.z:F3} >= {config.StopAtZ.Value:F3}");
                break;
            }

            // === 地面バウンド判定 ===
            if (config.BounceSettings != null)
            {
                if (nextPosition.y <= config.BounceSettings.groundLevel && position.y > config.BounceSettings.groundLevel)
                {
                    float t = (config.BounceSettings.groundLevel - position.y) / (nextPosition.y - position.y);
                    Vector3 hitPoint = Vector3.Lerp(position, nextPosition, t);

                    bool shouldRoll = HandleGroundBounce(ref velocity, config.BounceSettings, bounceCount);

                    position = hitPoint;
                    position.y = config.BounceSettings.groundLevel;
                    points.Add(position);

                    bounceCount++;

                    if (shouldRoll)
                    {
                        isRolling = true;
                    }

                    if (bounceCount >= config.BounceSettings.maxBounces)
                    {
                        break;
                    }

                    currentTime += config.DeltaTime * t;
                    continue;
                }

                // === 壁バウンド判定 ===
                if (config.BounceSettings.enableWallBounce)
                {
                    if (IsOutOfBounds(nextPosition, config.BounceSettings.fieldBounds))
                    {
                        HandleWallBounce(ref nextPosition, ref velocity, config.BounceSettings);
                    }
                }
            }

            position = nextPosition;
            currentTime += config.DeltaTime;
            points.Add(position);

            // === 地面到達判定（バウンド無効時） ===
            if (config.BounceSettings == null && position.y < GROUND_LEVEL)
            {
                Debug.Log($"[シミュレーション] 地面到達: Y={position.y:F3}");
                break;
            }

            // === フィールド外判定（✅ 修正箇所） ===
            if (config.BounceSettings != null && config.BounceSettings.enableWallBounce)
            {
                if (IsOutOfBounds(position, config.BounceSettings.fieldBounds))
                {
                    break;
                }
            }

            if (points.Count > MAX_TRAJECTORY_POINTS)
            {
                Debug.LogWarning("[シミュレーション] 軌道点が上限到達");
                break;
            }
        }

        return points;
    }

    /// <summary>
    /// 後方互換性のための旧シグネチャ（投球用）
    /// </summary>
    public static List<Vector3> SimulateTrajectory(
        Vector3 startPosition,
        Vector3 initialVelocity,
        Vector3 spinAxisNormalized,
        float spinRateRPM,
        float liftCoefficient,
        TrajectorySettings settings)
    {
        var config = new SimulationConfig
        {
            DeltaTime = settings?.DeltaTime ?? 0.01f,
            MaxSimulationTime = settings?.MaxSimulationTime ?? 5f,
            StopAtZ = settings?.StopAtTarget == true ? settings.StopPosition.z : (float?)null,
            BounceSettings = null
        };

        return SimulateTrajectory(startPosition, initialVelocity, spinAxisNormalized,
                                 spinRateRPM, liftCoefficient, config);
    }

    /// <summary>
    /// 地面バウンド処理
    /// </summary>
    public static bool HandleGroundBounce(ref Vector3 velocity, BounceSettings settings, int bounceCount)
    {
        velocity.y = -velocity.y * settings.groundRestitution;

        float horizontalSpeed = new Vector2(velocity.x, velocity.z).magnitude;
        float frictionLoss = horizontalSpeed * settings.groundFriction;

        if (horizontalSpeed > 0.01f)
        {
            Vector3 horizontalDir = new Vector3(velocity.x, 0, velocity.z).normalized;
            float newHorizontalSpeed = Mathf.Max(0, horizontalSpeed - frictionLoss);
            velocity.x = horizontalDir.x * newHorizontalSpeed;
            velocity.z = horizontalDir.z * newHorizontalSpeed;
        }

        bool shouldRoll = velocity.y < 2f && bounceCount >= 2;
        return shouldRoll;
    }

    /// <summary>
    /// 壁バウンド処理
    /// </summary>
    public static void HandleWallBounce(ref Vector3 position, ref Vector3 velocity, BounceSettings settings)
    {
        Bounds bounds = settings.fieldBounds;

        if (position.x < bounds.min.x)
        {
            position.x = bounds.min.x;
            velocity.x = -velocity.x * settings.wallRestitution;
        }
        else if (position.x > bounds.max.x)
        {
            position.x = bounds.max.x;
            velocity.x = -velocity.x * settings.wallRestitution;
        }

        if (position.z < bounds.min.z)
        {
            position.z = bounds.min.z;
            velocity.z = -velocity.z * settings.wallRestitution;
        }
        else if (position.z > bounds.max.z)
        {
            position.z = bounds.max.z;
            velocity.z = -velocity.z * settings.wallRestitution;
        }
    }

    /// <summary>
    /// 転がり処理
    /// </summary>
    public static Vector3 SimulateRolling(Vector3 position, ref Vector3 velocity,
                                         BounceSettings settings, float deltaTime)
    {
        velocity.y = 0f;
        velocity.x *= settings.rollingDeceleration;
        velocity.z *= settings.rollingDeceleration;

        Vector3 nextPosition = position + velocity * deltaTime;
        nextPosition.y = settings.groundLevel;
        return nextPosition;
    }

    /// <summary>
    /// フィールド境界外判定
    /// </summary>
    public static bool IsOutOfBounds(Vector3 position, Bounds bounds)
    {
        return position.x < bounds.min.x || position.x > bounds.max.x ||
               position.z < bounds.min.z || position.z > bounds.max.z;
    }

    /// <summary>
    /// マグヌス力を計算
    /// </summary>
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

    /// <summary>
    /// 空気抵抗を計算
    /// </summary>
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

    /// <summary>
    /// 軌道から指定Z座標での通過点を取得
    /// </summary>
    public static Vector3 FindPointAtZ(List<Vector3> trajectory, float targetZ)
    {
        for (int i = 0; i < trajectory.Count - 1; i++)
        {
            Vector3 point1 = trajectory[i];
            Vector3 point2 = trajectory[i + 1];

            if ((point1.z <= targetZ && point2.z >= targetZ) ||
                (point1.z >= targetZ && point2.z <= targetZ))
            {
                float t = Mathf.InverseLerp(point1.z, point2.z, targetZ);
                return Vector3.Lerp(point1, point2, t);
            }
        }

        return trajectory.Count > 0 ? trajectory[trajectory.Count - 1] : Vector3.zero;
    }
}