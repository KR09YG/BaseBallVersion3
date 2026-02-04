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

        if (_defenderCatchEvent != null) ;
        else Debug.LogError("OnDefenderCatchEvent is not assigned in DefenseManager.");

        if (_ballSpawnedEvent != null) _ballSpawnedEvent.RegisterListener(SetBall);
        else Debug.LogError("OnBallSpawnedEvent is not assigned in DefenseManager.");

        if (_atBatResetEvent != null) _atBatResetEvent?.RegisterListener(OnAtBatReset);
        else Debug.LogError("OnAtBatResetEvent is not assigned in DefenseManager.");

        // 初期化可能なオブジェクトを辞書に追加
        _byPosition ??= new Dictionary<PositionType, FielderController>();

        for (int i = 0; i < _fielders.Count; i++)
        {
            var f = _fielders[i];
            if (f == null) continue;

            if (!_byPosition.ContainsKey(f.Data.Position))
                _byPosition.Add(f.Data.Position, f);
        }

        // 初期位置を保存
        _initialPositions.Clear();
        for (int i = 0; i < _fielders.Count; i++)
        {
            var fielder = _fielders[i];
            if (fielder == null) continue;
            if (!_initialPositions.ContainsKey(fielder))
                _initialPositions.Add(fielder, fielder.transform.position);
        }
    }

    /// <summary>
    /// 初期化処理
    /// </summary>
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

    /// <summary>
    /// 打席リセット時処理
    /// </summary>
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

    /// <summary>
    /// 打球軌道受信時処理
    /// </summary>
    private void OnBattingBallTrajectory(List<Vector3> trajectory)
    {
        _pendingTrajectory = trajectory;
        TryStartDefenseFromHit();
    }

    /// <summary>
    /// 打球結果受信時処理
    /// </summary>
    private void OnBattingResult(BattingBallResult result)
    {
        _pendingResult = result;
        _hasPendingResult = true;
        TryStartDefenseFromHit();
    }

    /// <summary>
    /// 打球情報が揃ったら守備処理開始
    /// </summary>
    private void TryStartDefenseFromHit()
    {
        // 両方揃ってないなら待つ
        if (_pendingTrajectory == null || _pendingTrajectory.Count == 0) return;
        if (!_hasPendingResult) return;

        var traj = _pendingTrajectory;
        var res = _pendingResult;

        _pendingTrajectory = null;
        _hasPendingResult = false;

        OnBallHit(traj, res);
    }

    /// <summary>
    /// 打球処理開始時処理
    /// </summary>
    private void OnBallHit(List<Vector3> trajectory, BattingBallResult result)
    {
        if (result == null) return;
        // ファール・本塁打は守備処理しない
        if (result.BallType == BattingBallType.Foul || result.BallType == BattingBallType.HomeRun) return;

        // 捕球プラン計算
        CatchPlan catchPlan = DefenseCalculator.CalculateCatchPlan(
            trajectory,
            result,
            DELTA_TIME,
            _fielders);

        _isFly = catchPlan.IsFly;

        catchPlan.IsOutfield = catchPlan.Catcher.Data.PositionGroupType == PositionGroupType.Outfield;

        // 捕球前にベースカバーを走らせる
        _currentBaseCovers = BaseCoverCalculator.BaseCoverCalculation(
            _fielders,
            _situation,
            catchPlan,
            _baseManager);

        // CatchPlan に「各塁への送球到達予測秒」を埋める
        FillThrowArrivalTimes(ref catchPlan, _currentBaseCovers);

        // 決定した捕球プランを通知
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

    /// <summary>
    /// ボールオブジェクト設定時処理
    /// </summary>
    /// <param name="ball"></param>
    private void SetBall(GameObject ball)
    {
        if (ball != null && ball.TryGetComponent<FielderThrowBallMove>(out var ballThrow))
            _ballThrow = ballThrow;
    }

    /// <summary>
    /// 守備選手の捕球イベント処理
    /// </summary>
    /// <param name="catchDefender"></param>
    public void OnDefenderCatchEvent(FielderController catchDefender)
    {
        // 既に送球処理中なら無視
        if (_isThrowing) return;
        if (_ballThrow == null) return;
        if (_runnerManager == null) return;
        if (catchDefender == null) return;

        // 送球プラン計算
        var steps = DefenseThrowDecisionCalculator.ThrowDicision(
            catchDefender,
            _isFly,
            _byPosition,
            _baseManager,
            _situation,
            _runnerManager,
            throwSpeed: 30f,
            catchTime: 0f);

        if (steps == null || steps.Count == 0) return;

        // 送球シーケンス開始
        _throwCts?.Cancel();
        _throwCts?.Dispose();
        _throwCts = new CancellationTokenSource();

        _currentThrowSteps = steps;
        _currentThrowIndex = 0;

        ExecuteThrowSequenceAsync(_throwCts.Token).Forget();
    }

    /// <summary>
    /// 送球シーケンス実行
    /// </summary>
    private async UniTaskVoid ExecuteThrowSequenceAsync(CancellationToken ct)
    {
        _isThrowing = true;

        DefensePlayOutcome outcome = new DefensePlayOutcome { HasJudgement = false };

        try
        {
            // 送球ステップを順次実行
            while (_currentThrowSteps != null && _currentThrowIndex < _currentThrowSteps.Count)
            {
                ct.ThrowIfCancellationRequested();

                ThrowStep step = _currentThrowSteps[_currentThrowIndex];

                // ゲッツー用（後で実装）
                if (TryBuildBaseJudgement(step, out var baseJudge))
                {
                    _onBasePlayJudged?.RaiseEvent(baseJudge);
                }
                // 送球ステップ実行
                await step.ThrowerFielder.ExecuteThrowStepAsync(step, _ballThrow, ct);

                _currentThrowIndex++;
            }
        }
        catch (OperationCanceledException) // キャンセル時無視
        {
        }
        finally
        {
            _isThrowing = false;

            // フライ捕球はアウト
            if (_isFly)
            {
                outcome.HasJudgement = true;
                outcome.IsOut = true;
                outcome.Plan = ThrowPlan.Return;
                outcome.TargetBase = BaseId.None;
                outcome.BallArriveTime = 0f;
                outcome.RunnerArriveTime = 0f;
                outcome.RunnerName = "(flyout)";

                // 後で実装
                //if (_onBasePlayJudged != null)
                //{
                //    _onBasePlayJudged.RaiseEvent(new BasePlayJudgement
                //    {
                //        RunnerType = RunnerType.Batter,
                //        TargetBase = BaseId.None,
                //        IsOut = true,
                //        BallArriveTime = 0f,
                //        RunnerArriveTime = 0f
                //    });
                //}
            }

            // 送球シーケンス終了通知
            if (_defenderFinishedEvent != null) _defenderFinishedEvent.RaiseEvent(outcome);
        }
    }

    /// <summary>
    /// 塁上の走者に対する送球判定を構築する
    /// </summary>
    private bool TryBuildBaseJudgement(ThrowStep step, out BasePlayJudgement baseJudge)
    {
        baseJudge = default;

        // 送球対象塁を取得
        if (!TryPlanToBase(step.Plan, out var baseId)) return false;

        // 送球対象塁にいる走者の中で最も早く到達する走者を探す
        _etaBuffer.Clear();
        _runnerManager.GetAllRunningETAs(_etaBuffer, true);

        // 最速走者を探す
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

        if (bestRunner == null) return false;

        // 走者の到達時間と送球到達時間を比較してアウトかセーフか判定
        float ballArrive = step.ArriveTime;
        bool isOut = ballArrive <= best;

        // 判定情報構築
        RunnerType rtype = bestRunner.Data.Type;

        // 判定情報
        baseJudge = new BasePlayJudgement
        {
            RunnerType = rtype,
            TargetBase = baseId,
            IsOut = isOut,
            BallArriveTime = ballArrive,
            RunnerArriveTime = best
        };

        return true;
    }

    /// <summary>
    /// 送球プランを塁IDに変換する
    /// </summary>
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

    /// <summary>
    /// CatchPlan に各塁への送球到達予測秒を埋める
    /// </summary>
    private void FillThrowArrivalTimes(ref CatchPlan plan, List<BaseCoverAssign> covers)
    {
        float catchTime = Mathf.Max(0f, plan.CatchTime);

        // 送球遅延時間
        float throwDelaySec = 0f;
        if (plan.Catcher != null && plan.Catcher.Data != null)
            throwDelaySec = Mathf.Max(0, plan.Catcher.Data.ThrowDelay) / 1000f;

        // 受取側の準備完了時間取得関数
        float ReceiverReady(BaseId baseId)
        {
            if (covers == null) return catchTime;
            
            for (int i = 0; i < covers.Count; i++)
            {
                // 該当塁の到着時間を返す
                if (covers[i].BaseId == baseId)
                    return Mathf.Max(catchTime, covers[i].ArriveTime);
            }
            return catchTime;
        }

        // 各塁への送球到達予測秒を計算して埋める
        plan.ThrowToFirstTime = EstimateThrowArrivalTime(plan.CatchPoint, BaseId.First, ReceiverReady(BaseId.First), throwDelaySec);
        plan.ThrowToSecondTime = EstimateThrowArrivalTime(plan.CatchPoint, BaseId.Second, ReceiverReady(BaseId.Second), throwDelaySec);
        plan.ThrowToThirdTime = EstimateThrowArrivalTime(plan.CatchPoint, BaseId.Third, ReceiverReady(BaseId.Third), throwDelaySec);
        plan.ThrowToHomeTime = EstimateThrowArrivalTime(plan.CatchPoint, BaseId.Home, ReceiverReady(BaseId.Home), throwDelaySec);
    }

    /// <summary>
    /// 送球到達予測時間を見積もる
    /// </summary>
    private float EstimateThrowArrivalTime(Vector3 fromPoint, BaseId toBase, float startTime, float throwDelaySec)
    {
        if (_baseManager == null) return float.MaxValue;

        // XZ距離で計算
        Vector3 from = fromPoint; from.y = 0f;
        Vector3 to = _baseManager.GetBasePosition(toBase); to.y = 0f;
        // 距離計算
        float dist = Vector3.Distance(from, to);
        float flight = dist / Mathf.Max(0.01f, _estimatedThrowSpeed);
        // 到達時間返却
        return startTime + throwDelaySec + flight;
    }
}
