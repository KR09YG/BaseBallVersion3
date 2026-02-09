using System.Collections.Generic;

/// <summary>
/// シミュレーション結果全体
/// </summary>
public struct PlaySimulationResult
{
    public DefensePlan DefensePlan;
    public RunningPlan RunningPlan;
}

/// <summary>
/// 守備計画
/// </summary>
public struct DefensePlan
{
    public CatchPlan CatchPlan;
    public List<BaseCoverAssign> BaseCovers;
    public List<ThrowStep> ThrowSequence;
    public Dictionary<BaseId, BaseJudgement> ExpectedJudgements;
}

/// <summary>
/// 走塁計画
/// </summary>
public struct RunningPlan
{
    public bool IsHomerun;
    public List<RunnerAction> RunnerActions;
    public Dictionary<BaseId, BaseJudgement> ExpectedResults;
}

/// <summary>
/// ベースカバー割り当て
/// </summary>
public struct BaseCoverAssign
{
    public BaseId BaseId;
    public FielderController Fielder;
    public float ArriveTime; // カバー位置への到着時刻
    public BaseCoverAssign(BaseId baseId, FielderController fielder, float arriveTime)
    {
        BaseId = baseId;
        Fielder = fielder;
        ArriveTime = arriveTime;
    }
}

/// <summary>
/// ランナー走塁計画（内部用）
/// </summary>
public struct RunnerPlan
{
    public RunnerType RunnerType;
    public BaseId StartBase;
    public BaseId MaxReachableBase;
    public float StartDelay;
}

/// <summary>
/// ランナーアクション（RunnerManagerに渡す）
/// </summary>
public struct RunnerAction
{
    public RunnerType RunnerType;
    public BaseId StartBase;
    public BaseId TargetBase;
    public float StartDelay;

    public RunnerAction(RunnerType type, BaseId start, BaseId target, float delay = 0f)
    {
        RunnerType = type;
        StartBase = start;
        TargetBase = target;
        StartDelay = delay;
    }
}

/// <summary>
/// 塁での判定結果
/// </summary>
public struct BaseJudgement
{
    public RunnerType RunnerType;
    public BaseId TargetBase;
    public bool IsOut;
    public float BallArriveTime;
    public float RunnerArriveTime;
}