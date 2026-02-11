using UnityEngine;

public class BattingCursor : MonoBehaviour, IInitializable
{
    [SerializeField] private GameObject _strikeZoon;
    [SerializeField] private GameObject _battingCursor;
    [SerializeField] private MeshRenderer _cursorRenderer;
    [SerializeField] private OnSwingEvent _swingEvent;
    [SerializeField] private OnAtBatResetEvent _atBatResetEvent;
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _moveRange = 0.5f;
    private Collider _strikeZoneCollider;
    private Vector3 _currentPos;
    private bool _isInputActive = false;
    public Vector3 CurrentPos => _currentPos;

    private void Awake()
    {
        if (_swingEvent != null) _swingEvent.RegisterListener(FinishInput);
        else Debug.LogWarning("_swingEvent is not assigned in BattingCursor.");

        if (_atBatResetEvent != null) _atBatResetEvent.RegisterListener(StartInput);
        else Debug.LogWarning("_atBatResetEvent is not assigned in BattingCursor.");
    }

    private void Start()
    {
        if (_strikeZoon != null)
        {
            _strikeZoneCollider = _strikeZoon.GetComponent<Collider>();
        }
        StartInput();
    }

    private void OnDestroy()
    {
        _swingEvent?.UnregisterListener(FinishInput);
        _atBatResetEvent?.UnregisterListener(StartInput);
    }

    private void Update()
    {
        if (!_isInputActive) return;
        // マウスの位置を取得
        Vector3 mousePosition = Input.mousePosition;
        mousePosition.z = Camera.main.WorldToScreenPoint(_strikeZoneCollider.bounds.center).z;
        Vector3 worldMousePosition = Camera.main.ScreenToWorldPoint(mousePosition);
        // ストライクゾーンの範囲+_moveRange内に制限
        Vector3 clampedPosition = _currentPos;
        if (_strikeZoneCollider != null)
        {
            Bounds bounds = _strikeZoneCollider.bounds;
            clampedPosition.x = Mathf.Clamp(worldMousePosition.x, bounds.min.x - _moveRange, bounds.max.x + _moveRange);
            clampedPosition.y = Mathf.Clamp(worldMousePosition.y, bounds.min.y - _moveRange, bounds.max.y + _moveRange);
            clampedPosition.z = bounds.center.z; // Z位置はストライクゾーンの中心に固定
        }
        // カーソルの位置を滑らかに移動
        _currentPos = Vector3.Lerp(_currentPos, clampedPosition, Time.deltaTime * _moveSpeed);
        _battingCursor.transform.position = _currentPos;
    }

    /// <summary>
    /// ピッチャーの投球動作が始まったときに呼び出される
    /// </summary>
    private void StartInput()
    {
        // マウスカーソルを非表示にする
        Cursor.visible = false;
        _isInputActive = true;
        // カーソルの初期位置をストライクゾーンの中心に設定
        if (_strikeZoneCollider != null)
        {
            _currentPos = _strikeZoneCollider.bounds.center;
            _battingCursor.transform.position = _currentPos;
        }
    }

    /// <summary>
    /// ボールが終点に到達もしくはスイングしたときに呼び出される
    /// </summary>
    private void FinishInput()
    {
        // マウスカーソルを表示する
        Cursor.visible = true;
        _isInputActive = false;
    }

    public void OnInitialized(DefenseSituation situation)
    {
        _cursorRenderer.enabled = false;
    }

    public void OnSetBatter()
    {
        _cursorRenderer.enabled = true;
        _currentPos = _strikeZoneCollider.bounds.center;
        _battingCursor.transform.position = _currentPos;
    }
}
