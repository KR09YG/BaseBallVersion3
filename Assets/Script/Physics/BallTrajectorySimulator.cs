using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 軌道シミュレーション結果（メタデータ付き）
/// </summary>
public struct TrajectoryResult
{
    public List<Vector3> Points;
    public string FirstGroundLayer;
    public int BounceCount;
}

internal static class BallTrajectorySimulator
{
    /// <summary>
    /// 軌道シミュレーション（通常版）
    /// ・Pitching用（レイヤー情報不要）
    /// </summary>
    public static List<Vector3> SimulateTrajectory(
        Vector3 startPosition,
        Vector3 initialVelocity,
        Vector3 spinAxisNormalized,
        float spinRateRPM,
        float liftCoefficient,
        BallPhysicsCalculator.SimulationConfig config)
    {
        List<Vector3> trajectory = new List<Vector3> { startPosition };

        Vector3 position = startPosition;
        Vector3 velocity = initialVelocity;

        float elapsed = 0f;
        int bounceCount = 0;
        bool isRolling = false;

        if (config.DeltaTime <= 0f)
        {
            Debug.LogError($"[BallTrajectorySimulator] Invalid DeltaTime: {config.DeltaTime}");
            return trajectory;
        }

        while (elapsed < config.MaxSimulationTime)
        {
            float prevZ = position.z;
            Vector3 prevPos = position;

            Vector3 newPos = position;
            Vector3 newVel = velocity;

            SimulatePhysicsStep(
                ref newPos,
                ref newVel,
                spinAxisNormalized,
                spinRateRPM,
                liftCoefficient,
                config.DeltaTime
            );

            // フェンス/ネット反射
            if (!isRolling && config.BounceSettings != null)
            {
                float restitution = config.BounceSettings.wallRestitution;

                BallCollisions.TryReflectOnFence(
                    prevPos,
                    ref newPos,
                    ref newVel,
                    config.DeltaTime,
                    restitution
                );
            }

            // 地面チェック（レイヤー情報不要）
            if (config.BounceSettings != null && newPos.y <= config.BounceSettings.groundLevel)
            {
                newPos.y = config.BounceSettings.groundLevel;

                bool shouldRoll = BallCollisions.HandleGroundBounce(
                    ref newVel,
                    config.BounceSettings,
                    bounceCount
                );

                bounceCount++;

                if (shouldRoll)
                {
                    isRolling = true;
                }
            }

            position = newPos;
            velocity = newVel;

            trajectory.Add(position);
            elapsed += config.DeltaTime;

            // 転がり処理
            if (isRolling && config.BounceSettings != null)
            {
                position = BallCollisions.SimulateRolling(
                    position,
                    ref velocity,
                    config.BounceSettings,
                    config.DeltaTime
                );

                trajectory[trajectory.Count - 1] = position;

                if (velocity.magnitude < config.BounceSettings.stopVelocityThreshold)
                {
                    break;
                }
            }

            // StopAtZ
            if (config.StopAtZ.HasValue)
            {
                float stopZ = config.StopAtZ.Value;
                float currZ = position.z;

                if ((prevZ - stopZ) * (currZ - stopZ) <= 0f)
                {
                    break;
                }
            }
        }

        return trajectory;
    }

    public static TrajectoryResult SimulateTrajectoryWithMetadata(
    Vector3 startPosition,
    Vector3 initialVelocity,
    Vector3 spinAxisNormalized,
    float spinRateRPM,
    float liftCoefficient,
    BallPhysicsCalculator.SimulationConfig config)
    {
        List<Vector3> trajectory = new List<Vector3> { startPosition };

        Vector3 position = startPosition;
        Vector3 velocity = initialVelocity;

        float elapsed = 0f;
        int bounceCount = 0;
        bool isRolling = false;
        string firstGroundLayer = null;

        if (config.DeltaTime <= 0f)
        {
            Debug.LogError($"[BallTrajectorySimulator] Invalid DeltaTime: {config.DeltaTime}");
            return new TrajectoryResult
            {
                Points = trajectory,
                FirstGroundLayer = null,
                BounceCount = 0
            };
        }

        while (elapsed < config.MaxSimulationTime)
        {
            float prevZ = position.z;
            Vector3 prevPos = position;

            Vector3 newPos = position;
            Vector3 newVel = velocity;

            SimulatePhysicsStep(
                ref newPos,
                ref newVel,
                spinAxisNormalized,
                spinRateRPM,
                liftCoefficient,
                config.DeltaTime
            );

            // フェンス/ネット反射（元のSimulateTrajectoryと同じ：continueしない）
            if (!isRolling && config.BounceSettings != null)
            {
                float restitution = config.BounceSettings.wallRestitution;

                BallCollisions.TryReflectOnFence(
                    prevPos,
                    ref newPos,
                    ref newVel,
                    config.DeltaTime,
                    restitution
                );
            }

            if (config.BounceSettings != null && newPos.y <= config.BounceSettings.groundLevel)
            {
                // 先に groundLevel へ吸着（元の挙動維持）
                newPos.y = config.BounceSettings.groundLevel;

                // 最初の接地レイヤーを確定（1回だけ）
                if (firstGroundLayer == null)
                {
                    // 判定したい中心点（groundLevelに吸着した地点）
                    Vector3 center = new Vector3(newPos.x, config.BounceSettings.groundLevel, newPos.z);

                    int mask = LayerMask.GetMask("Ground", "HomerunZone");
                    if (mask == 0) mask = ~0;

                    // 1) 上→下
                    bool hitOk = Physics.Raycast(
                        center + Vector3.up * 5f,
                        Vector3.down,
                        out RaycastHit hit,
                        20f,
                        mask,
                        QueryTriggerInteraction.Collide);

                    // 2) 下→上（埋まり/裏面/開始点問題の救済）
                    if (!hitOk)
                    {
                        hitOk = Physics.Raycast(
                            center + Vector3.down * 5f,
                            Vector3.up,
                            out hit,
                            20f,
                            mask,
                            QueryTriggerInteraction.Collide);
                    }

                    // 3) それでもダメなら OverlapSphere で近傍コライダーを直接拾う（最終保険）
                    if (!hitOk)
                    {
                        // 半径は「確実優先」で少し大きめ。球スケール0.1ならまずこれで拾える
                        float r = 0.5f;

                        Collider[] cols = Physics.OverlapSphere(
                            center,
                            r,
                            mask,
                            QueryTriggerInteraction.Collide);

                        if (cols != null && cols.Length > 0)
                        {
                            // 一番近いコライダーを採用（想定外が混ざる可能性を減らす）
                            Collider nearest = cols[0];
                            float best = (nearest.ClosestPoint(center) - center).sqrMagnitude;

                            for (int i = 1; i < cols.Length; i++)
                            {
                                float d = (cols[i].ClosestPoint(center) - center).sqrMagnitude;
                                if (d < best)
                                {
                                    best = d;
                                    nearest = cols[i];
                                }
                            }

                            firstGroundLayer = LayerMask.LayerToName(nearest.gameObject.layer);
                            Debug.Log($"[FirstGround] OverlapSphere picked layer='{firstGroundLayer}' obj={nearest.name} layerId={nearest.gameObject.layer}");
                        }
                        else
                        {
                            // ここまで来て0件は「その地点近傍にコライダーが存在しない」か、別PhysicsSceneなど
                            firstGroundLayer = "Unknown";
                            Debug.Log("[FirstGround] No collider found by Raycasts and OverlapSphere near landing point.");
                        }
                    }
                    else
                    {
                        firstGroundLayer = LayerMask.LayerToName(hit.collider.gameObject.layer);
                        Debug.Log($"[FirstGround] Ray hit layer='{firstGroundLayer}' obj={hit.collider.name} layerId={hit.collider.gameObject.layer} hitY={hit.point.y:F3}");
                    }
                }


                // バウンド処理（元のSimulateTrajectoryと同じ）
                bool shouldRoll = BallCollisions.HandleGroundBounce(
                    ref newVel,
                    config.BounceSettings,
                    bounceCount
                );

                bounceCount++;

                if (shouldRoll)
                {
                    isRolling = true;
                }

                // HomerunZoneなら即終了したいならここ（任意）
                if (firstGroundLayer == "HomerunZone")
                {
                    position = newPos;
                    velocity = newVel;
                    trajectory.Add(position);
                    break;
                }
            }


            position = newPos;
            velocity = newVel;

            trajectory.Add(position);
            elapsed += config.DeltaTime;

            // 転がり処理（元のSimulateTrajectoryと同じ）
            if (isRolling && config.BounceSettings != null)
            {
                position = BallCollisions.SimulateRolling(
                    position,
                    ref velocity,
                    config.BounceSettings,
                    config.DeltaTime
                );

                trajectory[trajectory.Count - 1] = position;

                if (velocity.magnitude < config.BounceSettings.stopVelocityThreshold)
                {
                    break;
                }
            }

            // StopAtZ（元のSimulateTrajectoryと同じ）
            if (config.StopAtZ.HasValue)
            {
                float stopZ = config.StopAtZ.Value;
                float currZ = position.z;

                if ((prevZ - stopZ) * (currZ - stopZ) <= 0f)
                {
                    break;
                }
            }
        }


        return new TrajectoryResult
        {
            Points = trajectory,
            FirstGroundLayer = firstGroundLayer,
            BounceCount = bounceCount
        };
    }

    /// <summary>
    /// 物理ステップ計算（重力・抗力・マグヌス力）
    /// </summary>
    private static void SimulatePhysicsStep(
        ref Vector3 position,
        ref Vector3 velocity,
        Vector3 spinAxisNormalized,
        float spinRateRPM,
        float liftCoefficient,
        float deltaTime)
    {
        Vector3 gravity = Physics.gravity;
        Vector3 dragForce = BallAerodynamics.CalculateDragForce(velocity);
        Vector3 magnusForce = BallAerodynamics.CalculateMagnusForce(
            velocity,
            spinAxisNormalized,
            spinRateRPM,
            liftCoefficient
        );

        const float BALL_MASS_KG = 0.145f;
        Vector3 acceleration = gravity + (dragForce + magnusForce) / BALL_MASS_KG;

        velocity += acceleration * deltaTime;
        position += velocity * deltaTime;
    }
}