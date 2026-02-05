using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class DefenseManager : MonoBehaviour, IInitializable
{
    [SerializeField] private List<FielderController> _fielders;
    private Dictionary<PositionType, FielderController> _byPosition;
    private Dictionary<FielderController, Vector3> _initialPositions = new Dictionary<FielderController, Vector3>();

    [SerializeField] private OnBattingResultEvent _battingResultEvent;
    [SerializeField] private OnBattingBallTrajectoryEvent _battingBallTrajectoryEvent;

    [SerializeField] private OnBasePlayJudged _onBasePlayJudged;

    [SerializeField] private OnDefenderCatchEvent _defenderCatchEvent;
    [SerializeField] private OnDefensePlayFinishedEvent _defenderFinishedEvent;
    [SerializeField] private OnAtBatResetEvent _atBatResetEvent;
    [SerializeField] private OnDecidedCatchPlan _onDecidedCatchPlan;

    [SerializeField] private BaseManager _baseManager;
    [SerializeField] private RunnerManager _runnerManager;
    [SerializeField] private OnBallSpawnedEvent _ballSpawnedEvent;

    [Header("Throw estimation (Defense-owned)")]
    [Tooltip("送球速度の概算(m/s)。守備調整はここだけ変えればRunnerにも反映される")]
    [SerializeField] private float _estimatedThrowSpeed = 28f;

    [Tooltip("外野判定：ホームからのXZ距離がこの値以上なら外野扱い")]
    [SerializeField] private float _outfieldDistance = 35f;

    private DefenseSituation _situation;

    // 全体のシミュレーションも 0.01f に合わせる
    private const float DELTA_TIME = 0.01f;

    private BattingBallResult _pendingResult;
    private List<Vector3> _pendingTrajectory;

    private FielderThrowBallMove _ballThrow;
    private List<ThrowStep> _currentThrowSteps;
    private List<BaseCoverAssign> _currentBaseCovers;
    private int _currentThrowIndex;
    private CancellationTokenSource _throwCts;
    private bool _isThrowing;
    private bool _hasPendingResult;
    private bool _isFly;

    private readonly List<RunnerETA> _etaBuffer = new(8);

    private void Awake()
    {
        if (_battingResultEvent != null) _battingResultEvent?.RegisterListener(OnBattingResult);
        else Debug.LogError("OnBattingResultEvent is not assigned in DefenseManager.");

        if (_battingBallTrajectoryEvent != null) _battingBallTrajectoryEvent?.RegisterListener(OnBattingBallTrajectory);
        else Debug.LogError("OnBattingBallTrajectoryEvent is not assigned in DefenseManager.");

        if (_defenderCatchEvent != null) _defenderCatchEvent?.RegisterListener(OnDefenderCatchEvent);
        else Debug.LogError("OnDefenderCatchEvent is not assigned in DefenseManager.");

        if (_ballSpawnedEvent != null) _ballSpawnedEvent.RegisterListener(SetBall);
        else Debug.LogError("OnBallSpawnedEvent is not assigned in DefenseManager.");


        _atBatResetEvent?.RegisterListener(OnAtBatReset);

        _byPosition ??= new Dictionary<PositionType, FielderController>();

        for (int i = 0; i < _fielders.Count; i++)
        {
            var f = _fielders[i];
            if (f == null) continue;

            if (!_byPosition.ContainsKey(f.Data.Position))
                _byPosition.Add(f.Data.Position, f);
        }

        _initialPositions.Clear();
        for (int i = 0; i < _fielders.Count; i++)
        {
            var fielder = _fielders[i];
            if (fielder == null) continue;
            if (!_initialPositions.ContainsKey(fielder))
                _initialPositions.Add(fielder, fielder.transform.position);
        }
    }

    public void OnInitialized(DefenseSituation situation)
    {
        _situation = situation;
        _isFly = false;

        for (int i = 0; i < _fielders.Count; i++)
        {
            var fielder = _fielders[i];
            if (fielder == null) continue;

            if (_initialPositions.TryGetValue(fielder, out var initPos))
                fielder.transform.position = initPos;
        }
    }

    public void OnAtBatReset()
    {
        OnInitialized(_situation);
    }

    private void OnDisable()
    {
        _battingResultEvent?.UnregisterListener(OnBattingResult);
        _battingBallTrajectoryEvent?.UnregisterListener(OnBattingBallTrajectory);
        _defenderCatchEvent?.UnregisterListener(OnDefenderCatchEvent);
        _ballSpawnedEvent?.UnregisterListener(SetBall);

        _throwCts?.Cancel();
        _throwCts?.Dispose();
        _throwCts = null;
    }

    private void OnBattingBallTrajectory(List<Vector3> trajectory)
    {
        _pendingTrajectory = trajectory;
        TryStartDefenseFromHit();
    }

    private void OnBattingResult(BattingBallResult result)
    {
        _pendingResult = result;
        _hasPendingResult = true;
        TryStartDefenseFromHit();
    }

    private void TryStartDefenseFromHit()
    {
        if (_pendingTrajectory == null || _pendingTrajectory.Count == 0) return;
        if (!_hasPendingResult) return;
        if (_pendingResult.BallType == BattingBallType.Foul) return;

        var traj = _pendingTrajectory;
        var res = _pendingResult;

        _pendingTrajectory = null;
        _hasPendingResult = false;

        OnBallHit(traj, res);
    }

    private void OnBallHit(List<Vector3> trajectory, BattingBallResult result)
    {
        if (result == null) return;
        if (result.BallType == BattingBallType.Foul || result.BallType == BattingBallType.HomeRun)
            return;

        CatchPlan catchPlan = DefenseCalculator.CalculateCatchPlan(
            trajectory,
            result,
            DELTA_TIME,
            _fielders);

        _isFly = catchPlan.IsFly;

        // 守備側で内外野を確定して渡す（Runner側に閾値を持たせない）
        catchPlan.IsOutfield = EstimateIsOutfield(catchPlan.CatchPoint);

        // 捕球前にベースカバーを走らせる
        _currentBaseCovers = BaseCoverCalculator.BaseCoverCalculation(
            _fielders,
            _situation,
            catchPlan,
            _baseManager);

        // CatchPlan に「各塁への送球到達予測秒」を埋める
        FillThrowArrivalTimes(ref catchPlan, _currentBaseCovers);

        // 完成版を通知（RunnerManager はこれで走塁開始＆長打判断）
        _onDecidedCatchPlan?.RaiseEvent(catchPlan);

        // 捕球者は捕球点へ
        if (catchPlan.Catcher != null)
            catchPlan.Catcher.MoveToCatchPoint(catchPlan.CatchPoint, catchPlan.CatchTime);

        // ベースカバー移動
        if (_currentBaseCovers != null)
        {
            for (int i = 0; i < _currentBaseCovers.Count; i++)
            {
                var cover = _currentBaseCovers[i];
                if (cover.Fielder == null) continue;
                cover.Fielder.MoveToBase(_baseManager.GetBasePosition(cover.BaseId), cover.ArriveTime);
            }
        }
    }

    private void SetBall(GameObject ball)
    {
        if (ball != null && ball.TryGetComponent<FielderThrowBallMove>(out var ballThrow))
            _ballThrow = ballThrow;
    }

    // NOTE: あなたのイベント定義に合わせて引数は1つのまま
    public void OnDefenderCatchEvent(FielderController catchDefender)
    {
        if (_isThrowing) return;
        if (_ballThrow == null) return;
        if (_runnerManager == null) return;
        if (catchDefender == null) return;

        var steps = DefenseThrowDecisionCalculator.ThrowDicision(
            catchDefender,
            _isFly,
            _byPosition,
            _baseManager,
            _situation,
            _runnerManager,
            throwSpeed: 30f,
            catchTime: 0f);

        if (steps == null || steps.Count == 0)
            return;

        _throwCts?.Cancel();
        _throwCts?.Dispose();
        _throwCts = new CancellationTokenSource();

        _currentThrowSteps = steps;
        _currentThrowIndex = 0;

        ExecuteThrowSequenceAsync(_throwCts.Token).Forget();
    }

    private async UniTaskVoid ExecuteThrowSequenceAsync(CancellationToken ct)
    {
        _isThrowing = true;

        DefensePlayOutcome outcome = new DefensePlayOutcome { HasJudgement = false };

        try
        {
            while (_currentThrowSteps != null && _currentThrowIndex < _currentThrowSteps.Count)
            {
                ct.ThrowIfCancellationRequested();

                ThrowStep step = _currentThrowSteps[_currentThrowIndex];

                // ベースへ投げる瞬間に「その塁の判定」を通知（ゲッツー対応の入口）
                if (TryBuildBaseJudgement(step, out var baseJudge, out var legacyOutcome))
                {
                    _onBasePlayJudged?.RaiseEvent(baseJudge);

                    if (!outcome.HasJudgement)
                    {
                        outcome = legacyOutcome;
                    }
                }

                await step.ThrowerFielder.ExecuteThrowStepAsync(step, _ballThrow, ct);

                _currentThrowIndex++;
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _isThrowing = false;

            // フライ捕球はアウト（最終結果）
            if (_isFly)
            {
                outcome.HasJudgement = true;
                outcome.IsOut = true;
                outcome.Plan = ThrowPlan.Return;
                outcome.TargetBase = BaseId.None;
                outcome.BallArriveTime = 0f;
                outcome.RunnerArriveTime = 0f;
                outcome.RunnerName = "(flyout)";

                // 「バッターアウト」をRunnerManagerへ確定させたいならここで通知（推奨）
                // ※ TargetBase は None にして「塁アウトではない」扱い
                if (_onBasePlayJudged != null)
                {
                    _onBasePlayJudged.RaiseEvent(new BasePlayJudgement
                    {
                        RunnerType = RunnerType.Batter,
                        TargetBase = BaseId.None,
                        IsOut = true,
                        BallArriveTime = 0f,
                        RunnerArriveTime = 0f
                    });
                }
            }

            if (_defenderFinishedEvent != null) _defenderFinishedEvent.RaiseEvent(outcome);
        }
    }

    // ベース投げの判定を作る（RunnerManagerへ渡す用 + 互換用Outcome）
    private bool TryBuildBaseJudgement(ThrowStep step, out BasePlayJudgement baseJudge, out DefensePlayOutcome legacyOutcome)
    {
        baseJudge = default;
        legacyOutcome = new DefensePlayOutcome { HasJudgement = false };

        if (!TryPlanToBase(step.Plan, out var baseId))
            return false;

        _etaBuffer.Clear();
        _runnerManager.GetAllRunningETAs(_etaBuffer, true);

        float best = float.MaxValue;
        Runner bestRunner = null;

        for (int i = 0; i < _etaBuffer.Count; i++)
        {
            var eta = _etaBuffer[i];
            if (eta.TargetBase != baseId) continue;

            if (eta.Remaining < best)
            {
                best = eta.Remaining;
                bestRunner = eta.Runner;
            }
        }

        if (bestRunner == null)
            return false;

        // ArriveTime は「今投げたら何秒で着くか」の相対として扱う（あなたの既存設計に合わせる）
        float ballArrive = step.ArriveTime;
        bool isOut = ballArrive <= best;

        // RunnerTypeは Runner が持ってる想定（RunnerData.Type を公開してないなら Runner側に getter を追加）
        RunnerType rtype = bestRunner.Data.Type;

        baseJudge = new BasePlayJudgement
        {
            RunnerType = rtype,
            TargetBase = baseId,
            IsOut = isOut,
            BallArriveTime = ballArrive,
            RunnerArriveTime = best
        };

        legacyOutcome = new DefensePlayOutcome
        {
            HasJudgement = true,
            IsOut = isOut,
            Plan = step.Plan,
            TargetBase = baseId,
            BallArriveTime = ballArrive,
            RunnerArriveTime = best,
            RunnerName = bestRunner != null ? bestRunner.name : "(null)"
        };

        return true;
    }

    private static bool TryPlanToBase(ThrowPlan plan, out BaseId baseId)
    {
        switch (plan)
        {
            case ThrowPlan.First: baseId = BaseId.First; return true;
            case ThrowPlan.Second: baseId = BaseId.Second; return true;
            case ThrowPlan.Third: baseId = BaseId.Third; return true;
            case ThrowPlan.Home: baseId = BaseId.Home; return true;
            default:
                baseId = BaseId.First;
                return false;
        }
    }

    private bool EstimateIsOutfield(Vector3 p)
    {
        if (_baseManager == null) return false;

        Vector3 home = _baseManager.GetBasePosition(BaseId.Home);
        home.y = 0f;

        Vector3 q = p;
        q.y = 0f;

        return Vector3.Distance(home, q) >= _outfieldDistance;
    }

    private void FillThrowArrivalTimes(ref CatchPlan plan, List<BaseCoverAssign> covers)
    {
        float catchTime = Mathf.Max(0f, plan.CatchTime);

        float throwDelaySec = 0f;
        if (plan.Catcher != null && plan.Catcher.Data != null)
            throwDelaySec = Mathf.Max(0, plan.Catcher.Data.ThrowDelay) / 1000f;

        float ReceiverReady(BaseId baseId)
        {
            if (covers == null) return catchTime;
            for (int i = 0; i < covers.Count; i++)
            {
                if (covers[i].BaseId == baseId)
                    return Mathf.Max(catchTime, covers[i].ArriveTime);
            }
            return catchTime;
        }

        plan.ThrowToFirstTime = EstimateThrowArrivalTime(plan.CatchPoint, BaseId.First, ReceiverReady(BaseId.First), throwDelaySec);
        plan.ThrowToSecondTime = EstimateThrowArrivalTime(plan.CatchPoint, BaseId.Second, ReceiverReady(BaseId.Second), throwDelaySec);
        plan.ThrowToThirdTime = EstimateThrowArrivalTime(plan.CatchPoint, BaseId.Third, ReceiverReady(BaseId.Third), throwDelaySec);
        plan.ThrowToHomeTime = EstimateThrowArrivalTime(plan.CatchPoint, BaseId.Home, ReceiverReady(BaseId.Home), throwDelaySec);
    }

    private float EstimateThrowArrivalTime(Vector3 fromPoint, BaseId toBase, float startTime, float throwDelaySec)
    {
        if (_baseManager == null) return float.MaxValue;

        Vector3 from = fromPoint; from.y = 0f;
        Vector3 to = _baseManager.GetBasePosition(toBase); to.y = 0f;

        float dist = Vector3.Distance(from, to);
        float flight = dist / Mathf.Max(0.01f, _estimatedThrowSpeed);

        return startTime + throwDelaySec + flight;
    }
}
