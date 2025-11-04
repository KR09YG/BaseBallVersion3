using System.Collections.Generic;
using System;
using UnityEngine;

/// <summary>
/// 打球に関するデータ
/// </summary>
[Serializable]
public class TrajectoryData : MonoBehaviour
{
    public Vector3 DropPoint; // 落下点
    public float FlightTime; // 飛行時間
    public float MaxHeight; // 最高到達点の高さ
    public float FlightDistance; // 飛距離
    public List<Vector3> TrajectoryPoints; // 軌道の点群
    public Vector3 LandingPosition;

    public HitType hitType; // 打球の種類
    public bool isHomeRun;

    /// <summary>
    /// 打球の種類を表す列挙型
    /// </summary>
    public enum HitType
    {
        Miss,
        GroundBall,
        FoulBall,
        LineDrive,
        FlyBall,
        HomeRun
    }
}