using System;
using UnityEngine;





/// <summary>
/// 打撃結果のデータ
/// </summary>
[Serializable]
public class InitialSpeedData
{
    /// <summary>
    /// 打球のパワー
    /// </summary>
    public float BattingPower;
    /// <summary>
    /// 打球方向
    /// </summary>
    public Vector3 ActualDirection;
    /// <summary>
    /// 初速ベクトル
    /// </summary> 
    public Vector3 InitialVelocity;
    /// <summary>
    /// 打球角度（Y）
    /// </summary>
    public float LaunchAngle;
    /// <summary>
    /// 打球方向（水平角度）
    /// </summary>
    public float LaunchDirection;
    /// <summary>
    /// 打球タイプ
    /// </summary>
    public HitType HittingType; 
}
public enum HitType
{
    GroundBall,
    LineDrive,
    FoulBall,
    FlyBall,
    PopFly,
    HomeRun
}

public class BattingResultDataManager : MonoBehaviour
{
    public InitialSpeedData ResultData { get; internal set; }

    public BattingInputData InputData { get; internal set; }

    public TrajectoryData TrajectoryData { get; internal set; }
}
