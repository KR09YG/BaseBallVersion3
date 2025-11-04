using System;
using UnityEngine;

/// <summary>
/// バッティングの入力データ
/// </summary>
[Serializable]
public class BattingInputData : MonoBehaviour
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