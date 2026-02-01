using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public struct DefensePlayOutcome
{
    public bool HasJudgement;        // 判定ができたか（ベース送球が無い等でfalseになり得る）
    public bool IsOut;               // OUTならtrue
    public ThrowPlan Plan;           // First/Second/Third/Home
    public BaseId TargetBase;        // 判定した塁
    public float BallArriveTime;     // その塁にボールが着く予測秒（相対）
    public float RunnerArriveTime;   // その塁に走者が着く残り秒（相対）
    public string RunnerName;        // 最短走者名（デバッグ用）
}

public class DefenseManager : MonoBehaviour, IInitializable
{
    [Header("Fielders")]
    [SerializeField] private List<FielderController> _fielders;
    private Dictionary<PositionType, FielderController> _byPosition;
    private Dictionary<FielderController, Vector3> _initialPositions = new Dictionary<FielderController, Vector3>();

    [SerializeField] private OnBattingResultEvent _battingResultEvent;
    [SerializeField] private OnBattingBallTrajectoryEvent _battingBallTrajectoryEvent;
    [SerializeField] private OnDefensePlayJudged _defensePlayJudgedEvent;
    [SerializeField] private OnDefenderCatchEvent _defenderCatchEvent;
    [SerializeField] private OnDefensePlayFinishedEvent _defenderFinishedEvent;
    [SerializeField] private OnAtBatResetEvent _atBatResetEvent;

    [Header("World refs")]
    [SerializeField] private BaseManager _baseManager;
    [SerializeField] private RunnerManager _runnerManager;
    [SerializeField] private OnBallSpawnedEvent _ballSpawnedEvent;

    private DefenseSituation _situation;

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


    // RunnerETA を取るバッファ（RunnerManager.GetAllRunningETAs を使う前提）
    private readonly List<RunnerETA> _etaBuffer = new(8);

    private void Awake()
    {
        if (_battingResultEvent != null) _battingResultEvent.RegisterListener(OnBattingResult);
        else Debug.LogError("BattingResultEvent reference is not set in DefenseManager.");

        if (_battingBallTrajectoryEvent != null) _battingBallTrajectoryEvent.RegisterListener(OnBattingBallTrajectory);
        else Debug.LogError("BattingBallTrajectoryEvent reference is not set in DefenseManager.");

        if (_defenderCatchEvent != null) _defenderCatchEvent.RegisterListener(OnDefenderCatchEvent);
        else Debug.LogError("DefenderCatchEvent reference is not set in DefenseManager.");

        if (_ballSpawnedEvent != null) _ballSpawnedEvent.RegisterListener(SetBall);
        else Debug.LogError("BallSpawnedEvent reference is not set in DefenseManager.");

        if (_atBatResetEvent != null) _atBatResetEvent.RegisterListener(OnAtBatReset);
        else Debug.LogError("AtBatResetEvent reference is not set in DefenseManager.");

        _byPosition ??= new Dictionary<PositionType, FielderController>();

        foreach (var f in _fielders)
        {
            if (f == null) continue;

            if (!_byPosition.ContainsKey(f.Data.Position))
                _byPosition.Add(f.Data.Position, f);
            else
                Debug.LogWarning($"{f.Data.Position}が重複しています");
        }

        // 初期位置記録
        _initialPositions.Clear();
        foreach (var fielder in _fielders)
        {
            if (!_initialPositions.ContainsKey(fielder))
            {
                _initialPositions.Add(fielder, fielder.transform.position);
            }
            else
            {
                Debug.LogWarning($"Fielder {_fielders} position is already recorded.");
            }
        }
    }

    public void OnInitialized(DefenseSituation situation)
    {
        _situation = situation;
        // フィールダーを初期位置に戻す
        foreach (var fielder in _fielders)
        {
            if (_initialPositions.TryGetValue(fielder, out var initPos))
            {
                fielder.transform.position = initPos;
            }
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

        var traj = _pendingTrajectory;
        var res = _pendingResult;

        _pendingTrajectory = null;
        _hasPendingResult = false;

        OnBallHit(traj, res);
    }

    private void OnBallHit(List<Vector3> trajectory, BattingBallResult result)
    {
        if (result.IsFoul)
        {
            Debug.Log("Foul Ball - No Defense Action");
            return;
        }

        CatchPlan catchPlan =
            DefenseCalculator.CalculateCatchPlan(
                trajectory,
                result,
                DELTA_TIME,
                _fielders);

        // 捕球前にベースカバーを走らせる（投げる頃には受け手がいる状態を作る）
        _currentBaseCovers = BaseCoverCalculator.BaseCoverCalculation(
            _fielders,
            _situation,
            catchPlan,
            _baseManager,
            result);

        // 捕球者は捕球点へ
        catchPlan.Catcher.MoveToCatchPoint(catchPlan.CatchPoint, catchPlan.CatchTime);

        // ベースカバー移動
        foreach (var cover in _currentBaseCovers)
        {
            cover.Fielder.MoveToBase(
                _baseManager.GetBasePosition(cover.BaseId),
                cover.ArriveTime);
        }
    }

    private void SetBall(GameObject ball)
    {
        if (ball != null && ball.TryGetComponent<FielderThrowBallMove>(out var ballThrow))
        {
            _ballThrow = ballThrow;
        }
        else
        {
            Debug.LogError("FielderThrowBallMove component not found on the spawned ball.");
        }
    }

    public void OnDefenderCatchEvent(FielderController catchDefender, bool isFly)
    {
        if (_isThrowing) return;

        Debug.Assert(_ballThrow != null, "Ball reference (_ballThrow) is null. Did BallSpawnedEvent fire?");

        if (_runnerManager == null)
        {
            Debug.LogError("RunnerManager reference is null in DefenseManager.");
            return;
        }

        var steps = DefenseThrowDecisionCalculator.ThrowDicision(
            catchDefender, isFly, _byPosition, _baseManager, _situation, _runnerManager);

        if (steps == null || steps.Count == 0)
        {
            Debug.LogWarning("Throw steps empty.");
            return;
        }

        _throwCts?.Cancel();
        _throwCts?.Dispose();
        _throwCts = new CancellationTokenSource();

        _currentThrowSteps = steps;
        _currentThrowIndex = 0;

        ExecuteThrowSequenceAsync(_throwCts.Token, isFly).Forget();
    }

    private async UniTaskVoid ExecuteThrowSequenceAsync(CancellationToken ct, bool isFly)
    {
        _isThrowing = true;

        // この守備プレイの判定結果（複数送球でも「最初にベースへ投げた判定」を採用）
        DefensePlayOutcome outcome = new DefensePlayOutcome { HasJudgement = false };

        try
        {
            while (_currentThrowSteps != null && _currentThrowIndex < _currentThrowSteps.Count)
            {
                ct.ThrowIfCancellationRequested();

                ThrowStep step = _currentThrowSteps[_currentThrowIndex];

                Debug.Assert(step.ThrowerFielder != null, $"Thrower null at step {_currentThrowIndex}");
                Debug.Assert(step.ReceiverFielder != null, $"Receiver null at step {_currentThrowIndex}");

                Debug.Log($"[ThrowStep {_currentThrowIndex}] Plan={step.Plan} Thrower={step.ThrowerFielder.name} Receiver={step.ReceiverFielder.name}");

                // ✅ 送球前に判定材料を取る（相対時間比較）
                // - RunnerArriveTime は “今から何秒で塁に着くか”
                // - BallArriveTime は “今投げ始めてから何秒で塁に着くか”（ArriveTimeのみ。ThrowDelay等は必要なら加算）
                if (!outcome.HasJudgement && TryEvaluateOutSafe(step, out outcome))
                {
                    Debug.Log($"[Judgement] Base={outcome.TargetBase} Plan={outcome.Plan} " +
                              $"Ball={outcome.BallArriveTime:F2}s Runner={outcome.RunnerArriveTime:F2}s => {(outcome.IsOut ? "OUT" : "SAFE")} " +
                              $"Runner={outcome.RunnerName}");
                    // 判定自体は「投げる前」に確定させてよい（MVP）。
                    // 実際の見た目は投げ切る。
                    _defensePlayJudgedEvent.RaiseEvent(outcome);

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
            if (isFly) outcome.IsOut = true;
            // 最終的な結果を通知
            if (_defenderFinishedEvent != null) _defenderFinishedEvent.RaiseEvent(outcome);
            else Debug.LogError("[DefenseManager] OnDefenderFinishedEventが設定されていない");
        }
    }

    /// <summary>
    /// OUT/SAFE 判定（MVP）
    /// 「ベースへ投げた場合のみ」判定する。Cutoff/Returnは無視。
    /// </summary>
    private bool TryEvaluateOutSafe(ThrowStep step, out DefensePlayOutcome outcome)
    {
        outcome = new DefensePlayOutcome { HasJudgement = false };

        if (!TryPlanToBase(step.Plan, out var baseId))
            return false;

        // RunnerETA を取得
        _etaBuffer.Clear();
        _runnerManager.GetAllRunningETAs(_etaBuffer, true);

        // そのベースに向かっている最短走者を探す（RunnerETAは「TargetBase」がある前提）
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

        // その塁へ向かう走者がいないなら判定不能（投げ先が「アウト取り」じゃない可能性）
        if (bestRunner == null)
            return false;

        // ボール到達時間（MVP）
        // TODO: ThrowDelay や 捕球猶予などを加算したければここに足す
        float ballArrive = step.ArriveTime;

        bool isOut = ballArrive <= best;

        outcome = new DefensePlayOutcome
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
}