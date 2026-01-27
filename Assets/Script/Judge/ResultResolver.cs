using UnityEngine;

public class ResultResolver : MonoBehaviour
{
    [SerializeField] private AtBatManager _atBatManager;

    [SerializeField] private OnBattingResultEvent _battingBallResult;
    [SerializeField] private OnStrikeJudgeEvent _onStrikeJudgeEvent;
    [SerializeField] private OnBallReachedTargetEvent _ballReachedTargetEvent;
    [SerializeField] private OnFoulBallCompletedEvent _foulBallCompleted;
    [SerializeField] private OnDefensePlayFinishedEvent _defensePlayFinished;
    private PlayOutcomeType _playOutcome;
    private bool _currentIsStrike;

    private void Awake()
    {
        if (_battingBallResult != null) _battingBallResult.RegisterListener(OnBattingResult);
        else Debug.LogError("OnBattingResultEvent is not assigned in ResultResolver.");

        if (_onStrikeJudgeEvent != null) _onStrikeJudgeEvent.RegisterListener(OnStrikeJudge);
        else Debug.LogError("OnStrikeJudgeEvent is not assigned in ResultResolver.");

        if (_ballReachedTargetEvent != null) _ballReachedTargetEvent.RegisterListener(OnBallReached);
        else Debug.LogError("OnBallReachedTargetEvent is not assigned in ResultResolver");

        if (_foulBallCompleted != null) _foulBallCompleted.RegisterListener(OnFoulCompleted);
        else Debug.LogError("OnFoulBallCompleted is not assigned in ResultResolver");

        if (_defensePlayFinished != null) _defensePlayFinished.RegisterListener(OnDefensFinished);
        else Debug.LogError("OnDefensePlayFinishedEvent is not assigned in ResultResolver");
    }

    private void OnDestroy()
    {
        _battingBallResult?.UnregisterListener(OnBattingResult);
        _onStrikeJudgeEvent?.UnregisterListener(OnStrikeJudge);
        _ballReachedTargetEvent?.UnregisterListener(OnBallReached);
        _foulBallCompleted?.UnregisterListener(OnFoulCompleted);
        _defensePlayFinished?.UnregisterListener(OnDefensFinished);
    }

    private void OnBattingResult(BattingBallResult result)
    {
        if (result.IsFoul) _playOutcome = PlayOutcomeType.Foul;
        else if (result.BallType == BattingBallType.Miss) _playOutcome = PlayOutcomeType.StrikeSwinging;
        else if (result.IsHit) _playOutcome = PlayOutcomeType.Hit;
        Debug.Log($"Pitch Outcome: {_playOutcome}");
    }

    private void OnStrikeJudge(bool isStrike, Vector3 crossPos)
    {
        _currentIsStrike = isStrike;
        _playOutcome = _currentIsStrike ?
            PlayOutcomeType.StrikeLooking : PlayOutcomeType.Ball;
    }

    private void OnBallReached(PitchBallMove ball)
    {
        NotifyPitchOutcomeResolved();
    }

    private void OnFoulCompleted()
    {
        NotifyPitchOutcomeResolved();
    }

    private void OnDefensFinished(DefensePlayOutcome outcome)
    {
        _playOutcome = outcome.IsOut ? PlayOutcomeType.Out : PlayOutcomeType.Hit;
        NotifyPitchOutcomeResolved();
    }

    private void NotifyPitchOutcomeResolved()
    {
        _atBatManager.ReceivedResult(_playOutcome);
        _playOutcome = PlayOutcomeType.None;
    }
}


