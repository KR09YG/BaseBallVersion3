using UnityEngine;

[CreateAssetMenu]
public class BattingData : ScriptableObject
{
    [Header("基本計算パラメータ")]
    public float BasePower;
    public float BaseHeight;

    [Header("ボールとバットの位置差に対する倍率")]
    public float BattingXMultiplier = 2f;
    public float BattingYMultiplier = 1f;

    [Header("打球のz方向の線形補完の範囲")]
    public float MinBattingZRange;
    public float MaxBattingZRange;

    [Header("タイミングごとの補正値")]
    public float AccuracyPerfect;
    public float AccuracyGood;
    public float AccuracyFair;
    public float AccuracyBad;
    public float AccuracyMiss;

    [Header("各タイミングの結果の範囲（各タイミングの最小値）")]
    public float PerfectMin;
    public float GoodMin;
    public float FairMin;
    public float Maxtolerable;

    [Header("タイミング判定の基準値(%)")]
    public float TimingReferenceValue;

    [Header("タイミングのズレに対する許容値(0～100％)")]
    public float PerfectTimingRange;
    public float GoodTimingRange;
    public float FairTimingRange;
    public float BadTimingRange;

    [Header("タイミング精度ごとの基準となる打球の高さ")]
    public float PerfectHeight;
    public float GoodHeight;
    public float FairHeight;
    public float BadHeight;
}
