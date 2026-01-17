using UnityEngine;

/// <summary>
/// 打球結果（内部計算用）
/// </summary>
public struct BattingBallResult
{
    public Vector3 InitialVelocity;
    public float ExitVelocity;
    public float LaunchAngle;
    public float HorizontalAngle;
    public Vector3 SpinAxis;
    public float SpinRate;
    public float ImpactDistance;
    public float ImpactEfficiency;
    public bool IsSweetSpot;
    public float Timing;
    public bool IsFoul;

    public BattingBallType BallType;
    public Vector3 LandingPosition;
    public float Distance;
    public bool IsHit;
}
