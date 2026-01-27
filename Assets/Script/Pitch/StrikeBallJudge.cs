using UnityEngine;

public class StrikeBallJudge : MonoBehaviour
{
    [SerializeField] private StrikeZone _strikeZone;
    [SerializeField] private StrikeZoneUI _strikeZoneUI;
    [SerializeField] private OnStrikeJudgeEvent _onStrikeJudgeEvent;
    [SerializeField] private OnPitchBallReleaseEvent _onPitchBallRelease;

    private void Awake()
    {
        if (_onPitchBallRelease != null) _onPitchBallRelease.RegisterListener(OnReleased);
        else Debug.LogError("[StrikeBallJudge] PitchBallReleaseEventが設定されていません！");

        if (_onStrikeJudgeEvent == null) Debug.LogError("[StrikeBallJudge] OnStrikeJudgeEventが設定されていません！");
    }

    private void OnReleased(PitchBallMove ball)
    {
        if (!BallTrajectoryPredictor.TryGetCrossPointAtZ(
                ball.Trajectory, _strikeZone.CenterZ, out var point))
        {
            Debug.LogWarning("[StrikeBallJudge] ストライクゾーンでの交差点の予測に失敗しました！");
            return;
        }

        bool isStrike = _strikeZone.IsInZone(point);
        if (_onStrikeJudgeEvent == null) Debug.LogError("ストライクJudgeイベントない");
        _onStrikeJudgeEvent?.RaiseEvent(isStrike, point);
    }
}
