using System;

[Serializable]
public struct RunnerFinalState
{
    public RunnerType RunnerType;
    public BaseId StartBase;
    public BaseId EndBase;

    // 進塁数（None->First を1として扱う）
    public int AdvancedBases;

    // このプレイで「ホームに到達」したか（得点処理に使える）
    public bool ReachedHomeThisPlay;

    // このプレイで「走塁が確定するまで」にかかった秒（Outで止められた場合もその時点まで）
    public float TimeSeconds;

    // 将来ゲッツー等で使う：この走者がこのプレイで Out になったか
    public bool IsOutThisPlay;
}

[Serializable]
public struct RunningSummary
{
    public RunnerFinalState[] States;

    public RunnerFinalState GetState(RunnerType type)
    {
        if (States == null) return default;
        for (int i = 0; i < States.Length; i++)
        {
            if (States[i].RunnerType == type) return States[i];
        }
        return default;
    }
}
