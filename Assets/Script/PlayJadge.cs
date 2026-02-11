using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 打球結果を受け取って守備・走塁をシミュレーションし、各Managerに実行指示を出す
/// </summary>
public class PlayJudge : MonoBehaviour, IInitializable
{
    [Header("References")]
    [SerializeField] private DefenseManager _defenseManager;
    [SerializeField] private RunnerManager _runnerManager;
    [SerializeField] private BaseManager _baseManager;
    [SerializeField] private ResultResolver _resultResolver;
    [SerializeField] private OnBattingResultEvent _onBattingResultEvent;

    [Header("Settings")]
    [Tooltip("ランナー有利判定のマージン(秒)。既存コードと同じ0.15秒推奨")]
    [SerializeField] private float _safetyMargin = 0.15f;

    [Tooltip("シミュレーションの時間刻み。既存コードと同じ0.01秒")]
    [SerializeField] private float _simulationDeltaTime = 0.01f;

    [Tooltip("送球速度の概算(m/s)。DefenseManagerと同じ値を推奨")]
    [SerializeField] private float _estimatedThrowSpeed = 28f;

    private DefenseSituation _situation;

    private List<BaseCoverAssign> _baseCovers = new List<BaseCoverAssign>(4);
    private Dictionary<BaseId,float> _throwTimes = new Dictionary<BaseId, float>(4);
    private List<RunnerPlan> _runnerPlans = new List<RunnerPlan>(4);
    private List<RunnerAction> _runnerActions = new List<RunnerAction>(4);
    private List<ThrowStep> _throwSteps = new List<ThrowStep>(4);
    private Dictionary<BaseId, BaseJudgement> _baseJudges = new Dictionary<BaseId, BaseJudgement>(4);
    //private List<>

    private void Awake()
    {
        if (_onBattingResultEvent != null)
            _onBattingResultEvent.RegisterListener(OnBattingResult);
        else
            Debug.LogError("[PlayJudge] OnBattingResultEvent が未設定");
    }

    public void OnInitialized(DefenseSituation situation)
    {
        _situation = situation;
        ValidateReferences();
    }

    private void OnDestroy()
    {
        _onBattingResultEvent?.UnregisterListener(OnBattingResult);
    }

    private void ResetForReuse()
    {
        _baseCovers.Clear();
        _runnerActions.Clear();
        _throwSteps.Clear();
        _baseJudges.Clear();
    }

    /// <summary>
    /// 打球結果を受け取ったらシミュレーション開始
    /// </summary>
    private void OnBattingResult(BattingBallResult result)
    {
        if (result == null) return;
        
        ResetForReuse();

        // ファウル・ホームランは既存Managerに任せる
        if (result.BallType == BattingBallType.Foul)
        {
            RunnerAction action = new RunnerAction
            {
                RunnerType = RunnerType.Batter,
                StartBase = BaseId.None,
                TargetBase = BaseId.First,
                StartDelay = 0f
            };
            _runnerActions.Add(action);

            _runnerManager.ExecuteRunningPlan(new RunningPlan
            {
                IsHomerun = false,
                RunnerActions = _runnerActions,
                ExpectedResults = new Dictionary<BaseId, BaseJudgement>()
            });
            return;
        }

        // ホームランの場合はRunnerManagerにRunnerを走らせる
        if (result.BallType == BattingBallType.HomeRun)
        {
            RunningPlan plan = SetHomerunRunnerPlan();
            _runnerManager?.ExecuteRunningPlan(plan);
            return;
        }

        if (result.BallType == BattingBallType.Miss) return;

        Debug.Log($"=== [PlayJudge] OnBattingResult: {result.BallType} ===");

        // シミュレーション実行
        PlaySimulationResult simulationResult = SimulateEntirePlay(result);

        Debug.Log($"[PlayJudge] シミュレーション完了");
        Debug.Log($"[PlayJudge] 守備計画: 捕球者={simulationResult.DefensePlan.CatchPlan.Catcher?.Data?.Position}, 捕球時刻={simulationResult.DefensePlan.CatchPlan.CatchTime:F2}秒");
        Debug.Log($"[PlayJudge] 走塁計画: {simulationResult.RunningPlan.RunnerActions.Count}人");

        foreach (var action in simulationResult.RunningPlan.RunnerActions)
        {
            Debug.Log($"[PlayJudge]   - {action.RunnerType}: {action.StartBase} → {action.TargetBase}");
        }

        _resultResolver?.SetExpectedResult(simulationResult.DefensePlan.ExpectedJudgements);

        // DefenseManagerとRunnerManagerに実行指示
        Debug.Log("[PlayJudge] DefenseManager.ExecuteDefensePlan() 呼び出し");
        _defenseManager?.ExecuteDefensePlan(simulationResult.DefensePlan);

        Debug.Log("[PlayJudge] RunnerManager.ExecuteRunningPlan() 呼び出し");
        _runnerManager?.ExecuteRunningPlan(simulationResult.RunningPlan);
    }

    private RunningPlan SetHomerunRunnerPlan()
    {
        var plan = new RunningPlan();
        plan.IsHomerun = true;
        var actions = new List<RunnerAction>();
        // 既存ランナー（1塁、2塁、3塁）
        foreach (BaseId baseId in new[] { BaseId.First, BaseId.Second, BaseId.Third })
        {
            bool hasRunner = false;
            switch (baseId)
            {
                case BaseId.First: hasRunner = _situation?.OnFirstBase ?? false; break;
                case BaseId.Second: hasRunner = _situation?.OnSecondBase ?? false; break;
                case BaseId.Third: hasRunner = _situation?.OnThirdBase ?? false; break;
            }
            if (!hasRunner) continue;
            var runnerType = GetRunnerTypeAtBase(baseId);
            actions.Add(new RunnerAction(
                runnerType,
                baseId,
                BaseId.Home,
                0f
            ));
            Debug.Log($"[PlayJudge] {runnerType}: {baseId} → Home");
        }
        // 打者ランナー
        actions.Add(new RunnerAction(
            RunnerType.Batter,
            BaseId.None,
            BaseId.Home,
            0f
        ));
        Debug.Log($"[PlayJudge] Batter: None → Home");
        plan.RunnerActions = actions;
        plan.ExpectedResults = new Dictionary<BaseId, BaseJudgement>();
        return plan;
    }

    /// <summary>
    /// メインのシミュレーションロジック
    /// </summary>
    public PlaySimulationResult SimulateEntirePlay(BattingBallResult battingResult)
    {
        if (!ValidateReferences())
        {
            Debug.LogError("[PlayJudge] 参照が不正です");
            return new PlaySimulationResult();
        }

        // 1. 捕球計画を立てる
        CatchPlan catchPlan = CalculateCatchPlan(battingResult);

        // 2. ベースカバーを決める
        var baseCovers = CalculateBaseCovers(catchPlan);

        // 3. 各塁への送球到達時刻を計算
        var throwTimes = CalculateThrowTimes(catchPlan, baseCovers);

        // 4. フライの場合は打者アウト、走者は走らない
        if (catchPlan.IsFly) return FlayJudgement(catchPlan, baseCovers);

        // 5. ランナーの最大進塁先をシミュレーション
        var runnerPlans = SimulateAllRunners(battingResult, throwTimes);

        // 6. 最適な送球先を決定
        var throwSequence = DecideOptimalThrows(runnerPlans, throwTimes, catchPlan, baseCovers);

        // 7. 各塁でのアウト/セーフ判定
        var judgements = JudgeAllBases(runnerPlans, throwTimes);

        // 結果をまとめて返す
        var finalDefensePlan = new DefensePlan
        {
            CatchPlan = catchPlan,
            BaseCovers = baseCovers,
            ThrowSequence = throwSequence,
            ExpectedJudgements = judgements
        };

        var finalRunningPlan = new RunningPlan
        {
            RunnerActions = ConvertToRunnerActions(runnerPlans),
            ExpectedResults = judgements
        };

        _resultResolver?.SetExpectedResult(judgements);

        return new PlaySimulationResult
        {
            DefensePlan = finalDefensePlan,
            RunningPlan = finalRunningPlan
        };
    }

    /// <summary>
    /// どの野手がどこで捕球するか計算（既存 DefenseCalculator を使用）
    /// </summary>
    private CatchPlan CalculateCatchPlan(BattingBallResult result)
    {
        var fielders = GetFielders();

        if (fielders == null || fielders.Count == 0)
        {
            Debug.LogError("[PlayJudge] Fielders not found!");
            return CreateNoDefensePlan();
        }

        var catchPlan = DefenseCalculator.CalculateCatchPlan(
            result.TrajectoryPoints,
            result,
            _simulationDeltaTime,
            fielders);

        // 内外野判定を追加（既存 DefenseManager と同じロジック）
        if (catchPlan.Catcher != null && _baseManager != null)
        {
            catchPlan.IsOutfield = EstimateIsOutfield(catchPlan.CatchPoint);
        }

        // 送球到達時間を計算して CatchPlan に埋め込む（既存と同じ）
        FillThrowArrivalTimes(ref catchPlan, _baseCovers);

        return catchPlan;
    }

    private CatchPlan CreateNoDefensePlan()
    {
        return new CatchPlan
        {
            CanCatch = false,
            Catcher = null,
            CatchPoint = Vector3.zero,
            CatchTime = float.MaxValue,
            CatchTrajectoryIndex = -1,
            IsFly = false,
            IsOutfield = false,
            ThrowToFirstTime = float.MaxValue,
            ThrowToSecondTime = float.MaxValue,
            ThrowToThirdTime = float.MaxValue,
            ThrowToHomeTime = float.MaxValue
        };
    }

    /// <summary>
    /// 内外野判定（既存 DefenseManager と同じロジック）
    /// </summary>
    private bool EstimateIsOutfield(Vector3 point)
    {
        if (_baseManager == null) return false;

        Vector3 home = _baseManager.GetBasePosition(BaseId.Home);
        home.y = 0f;

        Vector3 p = point;
        p.y = 0f;

        const float OUTFIELD_DISTANCE = 35f; // DefenseManagerと同じ閾値
        return Vector3.Distance(home, p) >= OUTFIELD_DISTANCE;
    }

    private PlaySimulationResult FlayJudgement(CatchPlan catchPlan, List<BaseCoverAssign> baseCovers)
    {
        Debug.Log("[PlayJudge] フライ捕球シミュレーション");
        var defensePlan = new DefensePlan
        {
            CatchPlan = catchPlan,
            BaseCovers = baseCovers,
            ThrowSequence = _throwSteps,
            ExpectedJudgements = _baseJudges
        };

        RunnerAction action = new RunnerAction
        {
            RunnerType = RunnerType.Batter,
            StartBase = BaseId.None,
            TargetBase = BaseId.First,
            StartDelay = 0f
        };

        _runnerActions.Add(action);
        if (_baseJudges.ContainsKey(BaseId.None) == false)
        {
            var baseJudge = new BaseJudgement
            {
                RunnerType = RunnerType.Batter,
                TargetBase = BaseId.First,
                IsOut = true,
                BallArriveTime = catchPlan.CatchTime,
                RunnerArriveTime = _runnerManager.GetRunnerSpeed(RunnerType.Batter)
            };
            _baseJudges.Add(BaseId.None, baseJudge);
        }

        // 打者はファーストへ
        var runningPlan = new RunningPlan
        {
            IsHomerun = false,
            RunnerActions = _runnerActions,
            ExpectedResults = _baseJudges
        };

        return new PlaySimulationResult
        {
            DefensePlan = defensePlan,
            RunningPlan = runningPlan
        };
    }

    /// <summary>
    /// どの野手がどの塁をカバーするか決める（既存 BaseCoverCalculator を使用）
    /// </summary>
    private List<BaseCoverAssign> CalculateBaseCovers(CatchPlan catchPlan)
    {
        var fielders = GetFielders();

        return BaseCoverCalculator.BaseCoverCalculation(
            fielders,
            _situation,
            catchPlan,
            _baseManager
        );
    }

    /// <summary>
    /// 各塁への送球到達時刻を計算（既存 DefenseManager.FillThrowArrivalTimes と同じロジック）
    /// </summary>
    private Dictionary<BaseId, float> CalculateThrowTimes(
        CatchPlan catchPlan,
        List<BaseCoverAssign> baseCovers)
    {
        var throwTimes = _throwTimes;

        if (catchPlan.Catcher == null || _baseManager == null)
        {
            throwTimes[BaseId.First] = float.MaxValue;
            throwTimes[BaseId.Second] = float.MaxValue;
            throwTimes[BaseId.Third] = float.MaxValue;
            throwTimes[BaseId.Home] = float.MaxValue;
            return throwTimes;
        }

        float catchTime = Mathf.Max(0f, catchPlan.CatchTime);
        float throwDelaySec = Mathf.Max(0, catchPlan.Catcher.Data.ThrowDelay) / 1000f; // ms→秒

        Vector3 catchPoint = catchPlan.CatchPoint;
        catchPoint.y = 0f;

        // 受け手の準備完了時間を取得
        float GetReceiverReady(BaseId baseId)
        {
            if (baseCovers == null) return catchTime;

            foreach (var cover in baseCovers)
            {
                if (cover.BaseId == baseId)
                    return Mathf.Max(catchTime, cover.ArriveTime);
            }

            return catchTime;
        }

        foreach (BaseId baseId in new[] { BaseId.First, BaseId.Second, BaseId.Third, BaseId.Home })
        {
            Vector3 basePos = _baseManager.GetBasePosition(baseId);
            basePos.y = 0f;

            float distance = Vector3.Distance(catchPoint, basePos);
            float flightTime = distance / Mathf.Max(0.01f, _estimatedThrowSpeed);

            // 総到達時間 = 受け手準備完了 + 送球遅延 + 飛行時間
            float receiverReady = GetReceiverReady(baseId);
            throwTimes[baseId] = receiverReady + throwDelaySec + flightTime;
        }

        return throwTimes;
    }

    /// <summary>
    /// CatchPlan構造体に送球到達時間を埋め込む（既存 DefenseManager と同じ）
    /// </summary>
    private void FillThrowArrivalTimes(ref CatchPlan plan, List<BaseCoverAssign> covers)
    {
        if (plan.Catcher == null || _baseManager == null) return;

        float catchTime = Mathf.Max(0f, plan.CatchTime);
        float throwDelaySec = Mathf.Max(0, plan.Catcher.Data.ThrowDelay) / 1000f;

        float GetReceiverReady(BaseId baseId)
        {
            if (covers == null) return catchTime;
            foreach (var cover in covers)
            {
                if (cover.BaseId == baseId)
                    return Mathf.Max(catchTime, cover.ArriveTime);
            }
            return catchTime;
        }

        plan.ThrowToFirstTime = EstimateThrowArrivalTime(plan.CatchPoint, BaseId.First, GetReceiverReady(BaseId.First), throwDelaySec);
        plan.ThrowToSecondTime = EstimateThrowArrivalTime(plan.CatchPoint, BaseId.Second, GetReceiverReady(BaseId.Second), throwDelaySec);
        plan.ThrowToThirdTime = EstimateThrowArrivalTime(plan.CatchPoint, BaseId.Third, GetReceiverReady(BaseId.Third), throwDelaySec);
        plan.ThrowToHomeTime = EstimateThrowArrivalTime(plan.CatchPoint, BaseId.Home, GetReceiverReady(BaseId.Home), throwDelaySec);
    }

    private float EstimateThrowArrivalTime(Vector3 fromPoint, BaseId toBase, float startTime, float throwDelaySec)
    {
        if (_baseManager == null) return float.MaxValue;

        Vector3 from = fromPoint;
        from.y = 0f;

        Vector3 to = _baseManager.GetBasePosition(toBase);
        to.y = 0f;

        float dist = Vector3.Distance(from, to);
        float flight = dist / Mathf.Max(0.01f, _estimatedThrowSpeed);

        return startTime + throwDelaySec + flight;
    }

    /// <summary>
    /// 全ランナーの最大進塁先をシミュレーション
    /// </summary>
    private List<RunnerPlan> SimulateAllRunners(
        BattingBallResult result,
        Dictionary<BaseId, float> throwTimes)
    {
        var plans = _runnerPlans;

        // 既存ランナー（1塁、2塁、3塁）
        foreach (BaseId baseId in new[] { BaseId.First, BaseId.Second, BaseId.Third })
        {
            bool hasRunner = false;
            switch (baseId)
            {
                case BaseId.First: hasRunner = _situation?.OnFirstBase ?? false; break;
                case BaseId.Second: hasRunner = _situation?.OnSecondBase ?? false; break;
                case BaseId.Third: hasRunner = _situation?.OnThirdBase ?? false; break;
            }

            if (!hasRunner) continue;

            var runnerType = GetRunnerTypeAtBase(baseId);
            var maxBase = CalculateMaxAdvance(baseId, runnerType, throwTimes);

            plans.Add(new RunnerPlan
            {
                RunnerType = runnerType,
                StartBase = baseId,
                MaxReachableBase = maxBase,
                StartDelay = 0f
            });

            Debug.Log($"[PlayJudge] {runnerType}: {baseId} → {maxBase}");
        }

        // 打者ランナー
        if (result.BallType != BattingBallType.Miss)
        {
            var batterTarget = CalculateBatterTarget(result, throwTimes);

            plans.Add(new RunnerPlan
            {
                RunnerType = RunnerType.Batter,
                StartBase = BaseId.None,
                MaxReachableBase = batterTarget,
                StartDelay = 0f
            });

            Debug.Log($"[PlayJudge] Batter: None → {batterTarget}");
        }

        return plans;
    }

    /// <summary>
    /// ランナーがどこまで進塁できるか計算（既存 RunnerManager.CanBeatThrowTo と同じロジック）
    /// </summary>
    private BaseId CalculateMaxAdvance(
        BaseId currentBase,
        RunnerType runnerType,
        Dictionary<BaseId, float> throwTimes)
    {
        if (_runnerManager == null) return currentBase;

        float runnerSpeed = _runnerManager.GetRunnerSpeed(runnerType);

        // ホームから降順でチェック（既存と同じ）
        var targets = new[] { BaseId.Home, BaseId.Third, BaseId.Second, BaseId.First };

        foreach (var target in targets)
        {
            if ((int)target <= (int)currentBase) continue;

            int basesToAdvance = (int)target - (int)currentBase;
            float runTime = basesToAdvance * runnerSpeed;

            if (throwTimes.TryGetValue(target, out float throwTime))
            {
                // 既存コードと同じ: runTime + margin < throwTime でセーフ
                if (runTime + _safetyMargin < throwTime)
                {
                    return target;
                }
            }
        }

        return currentBase;
    }

    /// <summary>
    /// 打者がどこまで走れるか計算（既存 RunnerManager.DecideBatterTargetBase と同じロジック）
    /// </summary>
    private BaseId CalculateBatterTarget(
        BattingBallResult result,
        Dictionary<BaseId, float> throwTimes)
    {
        if (_runnerManager == null) return BaseId.First;

        float runnerSpeed = _runnerManager.GetRunnerSpeed(RunnerType.Batter);

        // ホームから降順でチェック（既存と同じ）
        var targets = new[] {
            (BaseId.Home, 4),
            (BaseId.Third, 3),
            (BaseId.Second, 2),
            (BaseId.First, 1)
        };

        foreach (var (targetBase, basesToRun) in targets)
        {
            float runTime = basesToRun * runnerSpeed;

            if (throwTimes.TryGetValue(targetBase, out float throwTime))
            {
                // 既存コードと同じ: runTime + margin < throwTime でセーフ
                if (runTime + _safetyMargin < throwTime)
                {
                    return targetBase;
                }
            }
        }

        return BaseId.First;
    }

    /// <summary>
    /// 最適な送球シーケンスを決定
    /// </summary>
    private List<ThrowStep> DecideOptimalThrows(
        List<RunnerPlan> runnerPlans,
        Dictionary<BaseId, float> throwTimes,
        CatchPlan catchPlan,
        List<BaseCoverAssign> baseCovers)
    {
        if (_defenseManager == null || _baseManager == null)
            return _throwSteps;

        if (catchPlan.Catcher == null)
            return _throwSteps;

        var fielders = GetFielders();
        if (fielders == null || fielders.Count == 0)
            return _throwSteps;

        var fieldersByPosition = fielders
            .Where(f => f?.Data != null)
            .ToDictionary(f => f.Data.Position, f => f);

        float throwSpeed = catchPlan.Catcher.Data?.ThrowSpeed ?? 28f;

        List<RunnerAction> runnerActions = ConvertToRunnerActions(runnerPlans);

        return DefenseThrowDecisionCalculator.DecideOptimalThrows(
            runnerActions,
            throwTimes,
            catchPlan,
            _baseManager,
            fieldersByPosition,
            baseCovers,
            throwSpeed);
    }

    /// <summary>
    /// 各塁でのアウト/セーフ判定（BasePlayJudgement型で返す）
    /// </summary>
    private Dictionary<BaseId, BaseJudgement> JudgeAllBases(
        List<RunnerPlan> runnerPlans,
        Dictionary<BaseId, float> throwTimes)
    {
        Dictionary<BaseId,BaseJudgement> judgements = _baseJudges;

        if (_runnerManager == null) return judgements;

        // ランナーごとに判定
        foreach (var plan in runnerPlans)
        {
            // 進塁先がない場合は判定不要
            BaseId targetBase = plan.MaxReachableBase;
            if (targetBase == BaseId.None) continue;

            // 進塁先ごとに判定
            int basesToRun = (int)targetBase - (int)plan.StartBase;
            if (plan.StartBase == BaseId.None) basesToRun = (int)targetBase;
            // ランナー到達時間を計算
            float runnerSpeed = _runnerManager.GetRunnerSpeed(plan.RunnerType);
            float runnerArriveTime = basesToRun * runnerSpeed + plan.StartDelay;

            // ボール到達時間を取得
            if (throwTimes.TryGetValue(targetBase, out float ballArriveTime))
            {
                // アウト/セーフ判定
                bool isOut = runnerArriveTime >= ballArriveTime;

                judgements[targetBase] = new BaseJudgement
                {
                    RunnerType = plan.RunnerType,
                    TargetBase = targetBase,
                    IsOut = isOut,
                    BallArriveTime = ballArriveTime,
                    RunnerArriveTime = runnerArriveTime
                };

                Debug.Log($"[PlayJudge] 判定 {plan.RunnerType}@{targetBase}: " +
                    $"ランナー={runnerArriveTime:F2}秒, ボール={ballArriveTime:F2}秒, " +
                    $"結果={(isOut ? "アウト" : "セーフ")}");
            }
        }

        return judgements;
    }

    private List<FielderController> GetFielders()
    {
        return _defenseManager?.GetAllFielders();
    }

    private RunnerType GetRunnerTypeAtBase(BaseId baseId)
    {
        return baseId switch
        {
            BaseId.First => RunnerType.First,
            BaseId.Second => RunnerType.Second,
            BaseId.Third => RunnerType.Third,
            _ => RunnerType.Batter
        };
    }

    private List<RunnerAction> ConvertToRunnerActions(List<RunnerPlan> plans)
    {
        return plans.Select(p => new RunnerAction(
            p.RunnerType,
            p.StartBase,
            p.MaxReachableBase,
            p.StartDelay
        )).ToList();
    }

    private bool ValidateReferences()
    {
        bool valid = true;

        if (_defenseManager == null)
        {
            Debug.LogError("[PlayJudge] DefenseManager is null!");
            valid = false;
        }

        if (_runnerManager == null)
        {
            Debug.LogError("[PlayJudge] RunnerManager is null!");
            valid = false;
        }

        if (_baseManager == null)
        {
            Debug.LogError("[PlayJudge] BaseManager is null!");
            valid = false;
        }

        return valid;
    }
}