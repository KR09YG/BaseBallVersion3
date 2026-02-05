public struct DefensePlayOutcome
{
    public bool HasJudgement;        // 判定ができたか（ベース送球が無い等でfalseになり得る）
    public bool IsOut;               // OUTならtrue
    public ThrowPlan Plan;           // First/Second/Third/Home/Cutoff/Return
    public BaseId TargetBase;        // 判定した塁
    public float BallArriveTime;     // その塁にボールが着く予測秒（相対）
    public float RunnerArriveTime;   // その塁に走者が着く残り秒（相対）
    public string RunnerName;        // 最短走者名（デバッグ用）
}
