using System.Collections.Generic;
using System;
using UnityEngine;

/// <summary>
/// 打球に関するデータ
/// </summary>
[Serializable]
public class TrajectoryData
{
    public Vector3 DropPoint;//落下点
    public float FlightTime;//飛行時間
    public float MaxHeight;//最高到達点の高さ
    public float FlightDistance;//飛距離
    public List<Vector3> TrajectoryPoints;//軌道の点群

    public HitType hitType;//打球の種類
    public bool isHomeRun;

    /// <summary>
    /// 打球の種類を表す列挙型
    /// </summary>
    public enum HitType
    {
        GroundBall,
        LineDrive,
        FlyBall,
        HomeRun
    }
}

/// <summary>
/// バッティングの入力データ
/// </summary>
[Serializable]
public class BattingInputData
{
    public float InputTime;              // 入力した時刻
    public float TimingAccuracy;         // タイミングの精度（0.0～1.0）
    public AccuracyType Accuracy;    // Perfect/Good/Fair/Bad/Miss

    [Header("位置情報")]
    public Vector3 BallPosition;         // 入力時のボール位置
    public Vector3 AtCorePosition;      // 入力時のバット位置
    public float DistanceFromCore;       // ボールとバットの距離

    [Header("プレイヤー設定")]
    public BattingType BatterType;       // 選択したバッタータイプ

    public enum BattingType
    {
        Normal, PullHit, OppositeHit, AimHomeRun
    }
}

/// <summary>
/// 打撃結果のデータ
/// </summary>
[Serializable]
public class BattingResultData
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

    public HitType HittingType; // 打球の種類
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
    public BattingResultData ResultData { get; internal set; }

    public BattingInputData InputData { get; internal set; }


}
