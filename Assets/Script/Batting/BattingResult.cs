using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 打球結果
/// </summary>
public class BattingBallResult
{
    // 物理パラメータ
    public Vector3 InitialVelocity;
    public float ExitVelocity;
    public float LaunchAngle;
    public float HorizontalAngle;
    public Vector3 SpinAxis;
    public float SpinRate;

    // インパクト情報
    public float ImpactDistance;
    public float ImpactEfficiency;
    public bool IsSweetSpot;
    public float Timing;

    // 弾道リスト
    public List<Vector3> TrajectoryPoints = new List<Vector3>();
    // 結果（シンプル化）
    public BattingBallType BallType; // Miss/Foul/Hit/HomeRun/GroundBall
    public Vector3 LandingPosition;
    public float Distance;

    // 削除: IsFoul, IsHit, FirstGroundLayer
}