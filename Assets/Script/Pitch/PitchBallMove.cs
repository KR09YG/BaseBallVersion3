using System.Collections.Generic;
using UnityEngine;

public class PitchBallMove : BallMoveTrajectory
{
    private PitchPreset _preset;

    public List<Vector3> Trajectory => _trajectory;

    [SerializeField] private OnBallReachedTargetEvent _ballReachedTargetEvent;
    [SerializeField] private OnSwingEvent _swingEvent;
    [SerializeField] private OnBattingHitEvent _battingHitEvent;
    [SerializeField] private OnBattingResultEvent _battingResultEvent;

    private void Awake()
    {
        if (_swingEvent != null) _swingEvent.RegisterListener(OnBattingInput);
        else Debug.LogError("[PitchBallMove] ❌ BattingInputEventが設定されていません！");

        if (_battingResultEvent != null) _battingResultEvent.RegisterListener(OnBattingResultEvent);
        else Debug.LogError("[PitchBallMove] ❌ BattingResultEventが設定されていません！");
    }

    private void OnDestroy()
    {
        _swingEvent?.UnregisterListener(OnBattingInput);
        _battingResultEvent?.UnregisterListener(OnBattingResultEvent);
    }

    public void Initialize(List<Vector3> trajectory, PitchPreset preset)
    {
        _elapsedTime = 0f;
        _trajectoryProgress = 0f; 
        _isMoving = false;
        _trajectory = trajectory;
        _preset = preset;
        transform.position = trajectory[0];
        _isMoving = true;
    }

    protected override void Update()
    {
        base.Update();
    }

    private void OnBattingInput()
    {
        _isMoving = false;
        if (_battingHitEvent != null)
            _battingHitEvent.RaiseEvent(this);
        else Debug.LogError("[PitchBallMove] BattingHitEventが設定されていません");
    }

    private void OnBattingResultEvent(BattingBallResult result)
    {
        if (result.BallType == BattingBallType.Miss)
        {
            _isMoving = true;
        }
    }

    protected override void ApplySpin()
    {
        float deg = _preset.SpinRate * 360f / 60f * _spinSpeedMultiplier;
        transform.Rotate(_preset.NormalizedSpinAxis, deg * Time.deltaTime);
    }

    protected override void OnReachedEnd()
    {
        Debug.Log("PitchBallMove: ボールがターゲットに到達しました");
        _isMoving = false;
        if (_ballReachedTargetEvent != null)
        {
            _ballReachedTargetEvent.RaiseEvent(this);
            Debug.Log("PitchBallMove: BallReachedTargetEventを発火しました");
        }
        else Debug.LogError("[PitchBallMove] BallReachedTargetEventが設定されていません");

        _trajectory = null;
        _elapsedTime = 0f;
    }
}
