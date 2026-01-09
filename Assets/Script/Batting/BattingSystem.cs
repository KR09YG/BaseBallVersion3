using UnityEngine;

public class BattingSystem : MonoBehaviour
{
    [SerializeField] private BattingInputEvent _inputEvent;
    [SerializeField] private SwingEvent _swingEvent;
    [SerializeField] private PitchBallReleaseEvent _startPitch;
    [SerializeField] private PitchBallReleaseEvent _endPitch;

    private bool _canSwing = true;
    private bool _isSwinging = false;

    private void Awake()
    {
        _startPitch.RegisterListener(ReleasedBall);
    }

    private void Update()
    {
        if (_canSwing && Input.GetMouseButtonDown(0) && !_isSwinging)
        {
            _isSwinging = true;
            _canSwing = false;
            _inputEvent.RaiseEvent();
            StartBattingCalculate();
        }
    }

    public void StartBattingCalculate()
    {
        _swingEvent.RaiseEvent();
    }

    /// <summary>
    /// ボールがリリースされたときの処理
    /// </summary>
    private void ReleasedBall(Ball ball)
    {
        Debug.Log("[BattingSystem] Ball Released - Swing is now allowed.");
        _canSwing = true;
        _isSwinging = false;
    }
}
