using System.Collections.Generic;
using UnityEngine;

public class PitchBallMove : BallMoveTrajectory
{
    private PitchPreset _preset;

    public List<Vector3> Trajectory => _trajectory;

    [SerializeField] private BallReachedTargetEvent _ballReachedTargetEvent;
    [SerializeField] private SwingEvent _swingEvent;
    [SerializeField] private BattingHitEvent _battingHitEvent;

    private void Awake()
    {
        if (_swingEvent != null)
        {
            _swingEvent.RegisterListener(OnBattingInput);
        }
        else
        {
            Debug.LogError("[PitchBallMove] ❌ BattingInputEventが設定されていません！");
        }
    }

    public void Initialize(List<Vector3> trajectory, PitchPreset preset)
    {
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
        _battingHitEvent.RaiseEvent(this);
    }

    protected override void ApplySpin()
    {
        float deg = _preset.SpinRate * 360f / 60f * _spinSpeedMultiplier;
        transform.Rotate(_preset.NormalizedSpinAxis, deg * Time.deltaTime);
    }

    protected override void OnReachedEnd()
    {
        _isMoving = false;
        _ballReachedTargetEvent.RaiseEvent(this);
    }
}
