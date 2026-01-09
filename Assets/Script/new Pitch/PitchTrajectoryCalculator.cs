using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 投球軌道の計算を担当する静的クラス
/// </summary>
public static class PitchTrajectoryCalculator
{
    /// <summary>
    /// 投球軌道を計算
    /// </summary>
    public static List<Vector3> PitchCalculate(
        PitchPreset preset,
        Vector3 releasePoint,
        Vector3 targetPoint,
        bool enableDebugLogs = false,
        TrajectoryDebugger debugger = null)
    {
        if (enableDebugLogs)
        {
            Debug.Log("========== 軌道計算開始 ==========");
            Debug.Log($"Release: {releasePoint}");
            Debug.Log($"Target: {targetPoint}");
            Debug.Log($"球種: {preset.PitchName}");
        }

        // パラメータ作成
        var parameters = preset.CreateParameters(releasePoint, targetPoint);

        if (enableDebugLogs)
        {
            LogParameters(parameters, preset);
        }

        // 軌道計算
        List<Vector3> trajectory = BallPhysicsCalculator.CalculateTrajectory(parameters);

        if (enableDebugLogs)
        {
            LogTrajectoryResults(trajectory);
        }

        // デバッガーに軌道を渡す
        if (debugger != null)
        {
            debugger.SetTrajectory(trajectory);
        }

        if (enableDebugLogs)
        {
            Debug.Log("========== 軌道計算完了 ==========");
        }

        return trajectory;
    }

    /// <summary>
    /// 軌道の総変化量を計算
    /// </summary>
    public static float CalculateTotalCurve(List<Vector3> trajectory)
    {
        if (trajectory == null || trajectory.Count < 3)
        {
            return 0f;
        }

        Vector3 start = trajectory[0];
        Vector3 end = trajectory[trajectory.Count - 1];
        Vector3 straightLine = end - start;

        float maxDeviation = 0f;

        for (int i = 1; i < trajectory.Count - 1; i++)
        {
            Vector3 point = trajectory[i];
            float deviation = Vector3.Cross(straightLine, point - start).magnitude / straightLine.magnitude;
            if (deviation > maxDeviation)
            {
                maxDeviation = deviation;
            }
        }

        return maxDeviation;
    }

    /// <summary>
    /// パラメータ情報をログ出力
    /// </summary>
    private static void LogParameters(PitchParameters parameters, PitchPreset preset)
    {
        Debug.Log($"Spin Axis: {parameters.SpinAxis}");
        Debug.Log($"Spin Rate: {parameters.SpinRate} rpm");
        Debug.Log($"Lift Coefficient: {parameters.LiftCoefficient}");
        Debug.Log($"Velocity: {parameters.Velocity} m/s ({preset.VelocityKmh} km/h)");
    }

    /// <summary>
    /// 軌道計算結果をログ出力
    /// </summary>
    private static void LogTrajectoryResults(List<Vector3> trajectory)
    {
        Debug.Log($"軌道ポイント数: {trajectory.Count}");

        if (trajectory.Count > 0)
        {
            Debug.Log($"軌道開始点: {trajectory[0]}");
            Debug.Log($"軌道終点: {trajectory[trajectory.Count - 1]}");

            float curveAmount = CalculateTotalCurve(trajectory);
            Debug.Log($"変化量: {curveAmount * 100f:F2}cm");
        }
    }
}