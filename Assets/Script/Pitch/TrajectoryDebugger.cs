using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 軌道のデバッグ表示
/// </summary>
public class TrajectoryDebugger : MonoBehaviour
{
    [Header("デバッグ表示設定")]
    [SerializeField] private bool _showDebugSpheres = true;
    [SerializeField] private Color _trajectoryPointColor = Color.cyan;
    [SerializeField] private Color _startPointColor = Color.green;
    [SerializeField] private Color _endPointColor = Color.red;
    [SerializeField] private float _sphereSize = 0.05f;

    private List<Vector3> _lastTrajectory;

    /// <summary>
    /// 軌道をセット
    /// </summary>
    public void SetTrajectory(List<Vector3> trajectory)
    {
        _lastTrajectory = trajectory;

        if (trajectory != null && trajectory.Count > 0)
        {
            Debug.Log($"[TrajectoryDebugger] 軌道を記録: {trajectory.Count}ポイント");

            // 変化量を計算
            float curveAmount = CalculateCurveAmount(trajectory);
            Debug.Log($"[TrajectoryDebugger] 変化量: {curveAmount * 100f:F2}cm");
        }
    }

    /// <summary>
    /// 軌道の変化量を計算
    /// </summary>
    private float CalculateCurveAmount(List<Vector3> trajectory)
    {
        if (trajectory.Count < 3) return 0f;

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

    private void OnDrawGizmos()
    {
        if (!_showDebugSpheres || _lastTrajectory == null || _lastTrajectory.Count == 0)
            return;

        // 軌道の各点を球で表示
        Gizmos.color = _trajectoryPointColor;
        for (int i = 1; i < _lastTrajectory.Count - 1; i++)
        {
            Gizmos.DrawSphere(_lastTrajectory[i], _sphereSize);
        }

        // 始点を強調
        Gizmos.color = _startPointColor;
        Gizmos.DrawSphere(_lastTrajectory[0], _sphereSize * 2f);

        // 終点を強調
        Gizmos.color = _endPointColor;
        Gizmos.DrawSphere(_lastTrajectory[_lastTrajectory.Count - 1], _sphereSize * 2f);

        // 始点と終点を結ぶ直線（比較用）
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(_lastTrajectory[0], _lastTrajectory[_lastTrajectory.Count - 1]);
    }
}