using System.Collections.Generic;
using UnityEngine;

internal static class BallTrajectorySimulator
{
    /// <summary>
    /// 物理シミュレーション（統一版・バウンド対応）
    /// ※ Net(Layer: Net / MeshCollider)に当たったら Bounds面で反射（enableWallBounce時）
    /// </summary>
    internal static List<Vector3> SimulateTrajectory(
        Vector3 startPosition,
        Vector3 initialVelocity,
        Vector3 spinAxisNormalized,
        float spinRateRPM,
        float liftCoefficient,
        BallPhysicsCalculator.SimulationConfig config)
    {
        List<Vector3> points = new List<Vector3>();
        Vector3 position = startPosition;
        Vector3 velocity = initialVelocity;

        float angularVelocity = spinRateRPM * BallPhysicsConstants.RPM_TO_RAD_PER_SEC;
        float currentTime = 0f;
        int bounceCount = 0;
        bool isRolling = false;

        Vector3 gravityForce = Physics.gravity * BallPhysicsConstants.BALL_MASS;
        points.Add(position);

        while (currentTime < config.MaxSimulationTime)
        {
            // === 転がり状態の処理 ===
            if (isRolling && config.BounceSettings != null)
            {
                position = BallCollisions.SimulateRolling(position, ref velocity, config.BounceSettings, config.DeltaTime);
                currentTime += config.DeltaTime;
                points.Add(position);

                if (velocity.magnitude < config.BounceSettings.stopVelocityThreshold)
                    break;

                continue;
            }

            // === 空中での物理計算 ===
            Vector3 magnusForce = BallAerodynamics.CalculateMagnusForce(velocity, spinAxisNormalized, angularVelocity, liftCoefficient);
            Vector3 dragForce = BallAerodynamics.CalculateDragForce(velocity);
            Vector3 totalForce = magnusForce + dragForce + gravityForce;
            Vector3 acceleration = totalForce / BallPhysicsConstants.BALL_MASS;

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

                    bool shouldRoll = BallCollisions.HandleGroundBounce(ref velocity, config.BounceSettings, bounceCount);

                    position = hitPoint;
                    position.y = config.BounceSettings.groundLevel;
                    points.Add(position);

                    bounceCount++;

                    if (shouldRoll) isRolling = true;

                    if (bounceCount >= config.BounceSettings.maxBounces)
                        break;

                    currentTime += config.DeltaTime * t;
                    continue;
                }

                // === 壁バウンド判定 ===
                if (config.BounceSettings.enableWallBounce)
                {
                    // 1) Netレイヤーに当たったら bounds面反射（優先）
                    if (!BallCollisions.TryReflectOnNetBoundsPlane(
                            position,
                            ref nextPosition,
                            ref velocity,
                            config.DeltaTime,
                            config.BounceSettings.wallRestitution))
                    {
                        // 2) 当たってない場合は従来のfieldBounds反射（外周など）
                        if (BallCollisions.IsOutOfBounds(nextPosition, config.BounceSettings.fieldBounds))
                        {
                            BallCollisions.HandleWallBounce(ref nextPosition, ref velocity, config.BounceSettings);
                        }
                    }
                }
            }

            position = nextPosition;
            currentTime += config.DeltaTime;
            points.Add(position);

            // === 地面到達判定（バウンド無効時） ===
            if (config.BounceSettings == null && position.y < BallPhysicsConstants.GROUND_LEVEL)
            {
                Debug.Log($"[シミュレーション] 地面到達: Y={position.y:F3}");
                break;
            }

            // === フィールド外判定（外周に出たら停止） ===
            if (config.BounceSettings != null && config.BounceSettings.enableWallBounce)
            {
                if (BallCollisions.IsOutOfBounds(position, config.BounceSettings.fieldBounds))
                    break;
            }

            if (points.Count > BallPhysicsConstants.MAX_TRAJECTORY_POINTS)
            {
                Debug.LogWarning("[シミュレーション] 軌道点が上限到達");
                break;
            }
        }

        return points;
    }
}
