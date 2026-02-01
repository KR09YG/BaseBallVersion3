using UnityEngine;

public class BattingSystem : MonoBehaviour
{
    [SerializeField] private OnBattingInputEvent _inputEvent;
    [SerializeField] private OnSwingEvent _swingEvent;
    [SerializeField] private OnPitchBallReleaseEvent _releseEvent;

    private bool _canSwing = true;
    private bool _isSwinging = false;

    private void Awake()
    {
        if (_releseEvent == null) Debug.LogError("[BattingSystem] ❌ PitchBallReleaseEventが設定されていません！");
        else _releseEvent.RegisterListener(ReleasedBall);

        _canSwing = false;
        _isSwinging = false;
    }

    private void OnDestroy()
    {
        _releseEvent?.UnregisterListener(ReleasedBall);
    }

    private void Update()
    {
        if (_canSwing && Input.GetMouseButtonDown(0) && !_isSwinging)
        {
            _isSwinging = true;
            _canSwing = false;
            _inputEvent.RaiseEvent();
        }
    }

    public void StartBattingCalculate()
    {
        if (_swingEvent == null)
        {
            Debug.LogError("[BattingSystem] ❌ SwingEventが設定されていません！");
            return;
        }

        _swingEvent.RaiseEvent();
    }

    /// <summary>
    /// ボールがリリースされたときの処理
    /// </summary>
    private void ReleasedBall(PitchBallMove ball)
    {
        Debug.Log("[BattingSystem] Ball Released - Swing is now allowed.");
        _canSwing = true;
        _isSwinging = false;
    }
}
