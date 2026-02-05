using System;

[Serializable]
public struct BasePlayJudgement
{
    public BaseId TargetBase;        // どの塁で
    public bool IsOut;               // アウトになる見込みか
    public float BallArriveTime;     // 今から何秒でボールが着くか（相対）
    public float RunnerArriveTime;   // 今から何秒で走者が着くか（相対）
    public RunnerType RunnerType;    // 対象走者（将来ゲッツー対応の要）
}
