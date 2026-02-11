using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

public class ResultResolver : MonoBehaviour, IInitializable
{
    [SerializeField] private AtBatManager _atBatManager;

    [SerializeField] private OnStrikeJudgeEvent _onStrikeJudgeEvent;
    [SerializeField] private OnBallReachedTargetEvent _ballReachedTargetEvent;
    [SerializeField] private OnBattingInputEvent _battingInputEvent;
    [SerializeField] private OnFoulBallCompletedEvent _foulBallCompleted;
    [SerializeField] private OnDefenseCompletedEvent _defensePlayFinished;
    [SerializeField] private OnAllRunnersStopped _onAllRunnersStopped;
    [SerializeField] private OnFinMissSwingEvent _onFinMissSwingEvent;
    [SerializeField] private OnHomeRunRunnerCompleted _onHomeRunRunnerCompleted;
    private PlayOutcomeType _playOutcome;
    private bool _currentIsStrike;
    private bool _hasDefenseResult = false;
    private bool _hasFinRunning = false;
    private int _scoreThisPlay = 0;
    private bool _hasSwingInput = false;

    private void Awake()
    {
        if (_onStrikeJudgeEvent != null) _onStrikeJudgeEvent.RegisterListener(OnStrikeJudge);
        else Debug.LogError("OnStrikeJudgeEvent is not assigned in ResultResolver.");

        if (_ballReachedTargetEvent != null) _ballReachedTargetEvent.RegisterListener(OnBallReached);
        else Debug.LogError("OnBallReachedTargetEvent is not assigned in ResultResolver");

        if (_foulBallCompleted != null) _foulBallCompleted.RegisterListener(OnFoulCompleted);
        else Debug.LogError("OnFoulBallCompleted is not assigned in ResultResolver");

        if (_defensePlayFinished != null) _defensePlayFinished.RegisterListener(OnDefensFinished);
        else Debug.LogError("OnDefensePlayFinishedEvent is not assigned in ResultResolver");

        if (_onFinMissSwingEvent != null) _onFinMissSwingEvent.RegisterListener(OnFinSwing);
        else Debug.LogError("OnFinMissSwingEvent is not assigned in ResultResolver");

        if (_onAllRunnersStopped != null) _onAllRunnersStopped.RegisterListener(OnFinRunning);
        else Debug.LogError("OnAllRunnersStopped is not assigned in ResultResolver");

        if (_onHomeRunRunnerCompleted != null) _onHomeRunRunnerCompleted.RegisterListener(OnHomeRunRunnerCompleted);
        else Debug.LogError("OnHomeRunRunnerCompleted is not assigned in ResultResolver");

        if (_battingInputEvent != null) _battingInputEvent.RegisterListener(OnBattingInput);
        else Debug.LogError("OnBattingInputEvent is not assigned in ResultResolver.");
    }

    private void OnDestroy()
    {
        //_battingBallResult?.UnregisterListener(OnBattingResult);
        _onStrikeJudgeEvent?.UnregisterListener(OnStrikeJudge);
        _ballReachedTargetEvent?.UnregisterListener(OnBallReached);
        _foulBallCompleted?.UnregisterListener(OnFoulCompleted);
        _defensePlayFinished?.UnregisterListener(OnDefensFinished);
        _onFinMissSwingEvent?.UnregisterListener(OnFinSwing);
        _onAllRunnersStopped?.UnregisterListener(OnFinRunning);
        _onHomeRunRunnerCompleted?.UnregisterListener(OnHomeRunRunnerCompleted);
    }

    public void SetExpectedResult(Dictionary<BaseId, BaseJudgement> judges)
    {
        int outCount = 0;
        _scoreThisPlay = 0;
        _hasDefenseResult = false;
        _hasFinRunning = false;
        _playOutcome = PlayOutcomeType.Hit;
        foreach (var judge in judges.Values)
        {
            Debug.Log($"判定確認: ターゲットベース {judge.TargetBase}, アウト判定 {judge.IsOut}");
            // アウトの数をカウント
            if (judge.IsOut)
            {
                outCount++;
                _playOutcome = PlayOutcomeType.Out;
            }
            // ホームに生還したランナーをカウント
            if (!judge.IsOut && judge.TargetBase == BaseId.Home)
            {
                _scoreThisPlay++;
                Debug.Log("スコア加算対象ランナー検出");
            }
        }

        if (outCount == 0) _playOutcome = PlayOutcomeType.Hit;

        Debug.Log($"守備判定完了: アウト数 {outCount}, スコア数 {_scoreThisPlay}, 結果 {_playOutcome}");

        WaitForPlayCompletionAsync().Forget();
    }

    private void OnHomeRunRunnerCompleted(int runnerCount)
    {
        _scoreThisPlay = runnerCount;
        _playOutcome = PlayOutcomeType.Homerun;
        NotifyPitchOutcomeResolved();
    }

    private async UniTaskVoid WaitForPlayCompletionAsync()
    {
        // 守備、走塁が完了するまで待機
        await UniTask.WaitUntil(() => _hasDefenseResult && _hasFinRunning);
        Debug.Log("守備・走塁完了検出");
        _hasDefenseResult = false;
        _hasFinRunning = false;
        NotifyPitchOutcomeResolved();
    }

    /// <summary>
    /// ストライクかボールかの判定が出たとき
    /// </summary>
    /// <param name="isStrike"></param>
    /// <param name="crossPos"></param>
    private void OnStrikeJudge(bool isStrike, Vector3 crossPos)
    {
        Debug.Log($"Strike Judge: {(isStrike ? "Strike" : "Ball")}");
        _currentIsStrike = isStrike;
        _playOutcome = _currentIsStrike ?
            PlayOutcomeType.StrikeLooking : PlayOutcomeType.Ball;
    }

    /// <summary>
    /// 空振りのアニメーションが完了したとき
    /// </summary>
    private void OnFinSwing()
    {
        Debug.Log("Finished Miss Swing");
        NotifyPitchOutcomeResolved();
    }

    private void OnBattingInput()
    {
        _hasSwingInput = true;
        _playOutcome = PlayOutcomeType.StrikeSwinging;
    }

    /// <summary>
    /// 見逃した or 空振りのボールがターゲットに到達したとき
    /// </summary>
    /// <param name="ball"></param>
    private void OnBallReached(PitchBallMove ball)
    {
        if (_hasSwingInput) return;
        Debug.Log("Ball Reached Target");
        if (_playOutcome != PlayOutcomeType.StrikeSwinging)
            NotifyPitchOutcomeResolved();
    }

    /// <summary>
    /// ファウルボールの完了
    /// </summary>
    private void OnFoulCompleted()
    {
        Debug.Log("Foul Ball Completed");
        _playOutcome = PlayOutcomeType.Foul;
        NotifyPitchOutcomeResolved();
    }

    private void OnFinRunning(RunningSummary summary)
    {
        Debug.Log($"All Runners Stopped");
        _hasFinRunning = true;
    }

    /// <summary>
    /// 守備プレイが完了したとき
    /// </summary>
    /// <param name="outcome"></param>
    private void OnDefensFinished()
    {
        Debug.Log("Defense Play Finished");
        _hasDefenseResult = true;
    }

    private void NotifyPitchOutcomeResolved()
    {
        Debug.Log($"結果通知: {_playOutcome}, スコア: {_scoreThisPlay}");
        _atBatManager.ReceivedResult(_playOutcome, _scoreThisPlay);
        _playOutcome = PlayOutcomeType.None;
        _scoreThisPlay = 0;
        _hasSwingInput = false;
    }

    public void OnInitialized(DefenseSituation situation)
    {
        _playOutcome = PlayOutcomeType.None;
        _currentIsStrike = false;
        _hasDefenseResult = false;
        _hasFinRunning = false;
        _scoreThisPlay = 0;
    }
}


