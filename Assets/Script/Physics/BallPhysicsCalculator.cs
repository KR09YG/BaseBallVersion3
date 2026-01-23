using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 野球ボールの物理計算（共通）- 投球、打撃、バウンドすべて対応
/// ※ Net(Layer: Net / MeshCollider) に当たったら Bounds 面で反射（軸平行面）
/// </summary>
public static class BallPhysicsCalculator
{
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

        Vector3 optimalVelocity = PitchVelocitySolver.FindOptimalVelocityAdvanced(
            parameters.ReleasePoint,
            parameters.TargetPoint,
            parameters.SpinAxis.normalized,
            parameters.SpinRate,
            parameters.LiftCoefficient,
            parameters.Velocity,
            parameters.Settings,
            bounceSettings
        );

        var config = new SimulationConfig
        {
            DeltaTime = parameters.Settings?.DeltaTime ?? 0.01f,
            MaxSimulationTime = parameters.Settings?.MaxSimulationTime ?? 5f,
            StopAtZ = parameters.Settings?.StopAtTarget == true
                ? parameters.Settings.StopPosition.z
                : (float?)null,
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
        return BallTrajectorySimulator.SimulateTrajectory(
            startPosition,
            initialVelocity,
            spinAxisNormalized,
            spinRateRPM,
            liftCoefficient,
            config);
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

    // === ここから下は「外部から呼ばれてる可能性がある」ので窓口として残す ===

    public static bool HandleGroundBounce(ref Vector3 velocity, BounceSettings settings, int bounceCount)
        => BallCollisions.HandleGroundBounce(ref velocity, settings, bounceCount);

    public static void HandleWallBounce(ref Vector3 position, ref Vector3 velocity, BounceSettings settings)
        => BallCollisions.HandleWallBounce(ref position, ref velocity, settings);

    public static Vector3 SimulateRolling(Vector3 position, ref Vector3 velocity, BounceSettings settings, float deltaTime)
        => BallCollisions.SimulateRolling(position, ref velocity, settings, deltaTime);

    public static bool IsOutOfBounds(Vector3 position, Bounds bounds)
        => BallCollisions.IsOutOfBounds(position, bounds);

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
