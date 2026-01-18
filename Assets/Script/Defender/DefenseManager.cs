using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class DefenseManager : MonoBehaviour
{
    [Header("Fielders")]
    [SerializeField] private List<FielderController> _fielders;

    [Header("Hit Events")]
    [SerializeField] private BattingResultEvent _battingResultEvent;
    [SerializeField] private BattingBallTrajectoryEvent _battingBallTrajectoryEvent;

    [Header("Catch & Throw Events")]
    [SerializeField] private DefenderCatchEvent _defenderCatchEvent;

    [Header("World refs")]
    [SerializeField] private BaseManager _baseManager;
    [SerializeField] private BallSpawnedEvent _ballSpawnedEvent;

    private const float DELTA_TIME = 0.01f;

    private BattingBallResult _pendingResult;
    private List<Vector3> _pendingTrajectory;

    private FielderThrowBallMove _ballThrow;
    private List<ThrowStep> _currentThrowSteps;
    private int _currentThrowIndex;
    private CancellationTokenSource _throwCts;
    private bool _isThrowing;

    private void OnEnable()
    {
        // Hit events
        _battingResultEvent?.RegisterListener(OnBattingResult);
        _battingBallTrajectoryEvent?.RegisterListener(OnBattingBallTrajectory);

        // Catch/Throw
        _defenderCatchEvent?.RegisterListener(OnDefenderCatchEvent);

        // Ball ref
        _ballSpawnedEvent?.RegisterListener(SetBall);
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
        TryStartDefenseFromHit();
    }

    private void TryStartDefenseFromHit()
    {
        // 両方揃うまで待つ
        if (_pendingTrajectory == null) return;

        // ここでまとめて処理して、バッファはクリア
        var traj = _pendingTrajectory;
        var res = _pendingResult;
        _pendingTrajectory = null;

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

        Debug.Log($"CanCatch: {catchPlan.CanCatch}");
        Debug.Log($"Catcher: {catchPlan.Catcher.Data.Position}");
        Debug.Log($"CatchPoint: {catchPlan.CatchPoint}");
        Debug.Log($"CatchTime: {catchPlan.CatchTime}");

        // 捕球地点へ移動（捕球判定自体はトリガで発火）
        catchPlan.Catcher.MoveTo(catchPlan.CatchPoint, catchPlan.CatchTime);
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
        // 送球中は新規開始しない（暫定ガード）
        if (_isThrowing) return;

        Debug.Assert(_ballThrow != null, "Ball reference (_ballThrow) is null. Did BallSpawnedEvent fire?");

        var steps = DefenseThrowDecisionCalculator.ThrowDicision(
            catchDefender, isFly, _fielders, _baseManager);

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

        ExecuteThrowSequenceAsync(_throwCts.Token).Forget();
    }

    private async UniTaskVoid ExecuteThrowSequenceAsync(CancellationToken ct)
    {
        _isThrowing = true;

        try
        {
            while (_currentThrowSteps != null && _currentThrowIndex < _currentThrowSteps.Count)
            {
                ct.ThrowIfCancellationRequested();

                var step = _currentThrowSteps[_currentThrowIndex];

                Debug.Assert(step.ThrowerFielder != null, $"Thrower null at step {_currentThrowIndex}");
                Debug.Assert(step.ReceiverFielder != null, $"Receiver null at step {_currentThrowIndex}");

                Debug.Log($"[ThrowStep {_currentThrowIndex}] Plan={step.Plan} Thrower={step.ThrowerFielder.name} Receiver={step.ReceiverFielder.name}");

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
        }
    }
}
