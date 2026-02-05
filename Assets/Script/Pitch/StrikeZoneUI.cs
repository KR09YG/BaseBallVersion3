using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
public class StrikeZoneUI : MonoBehaviour, IInitializable
{
    [SerializeField] private OnPitchBallReleaseEvent _pitchBallReleaseEvent;
    [SerializeField] private OnBallReachedTargetEvent _ballReachedTargetEvent;
    [SerializeField] private OnStrikeJudgeEvent _onStrikeJudgeEvent;
    [SerializeField] private OnAtBatResetEvent _atBatResetEvent;
    [SerializeField] private OnBattingResultEvent _battingResultEvent;

    [Header("ストライクゾーンUIの設定")]
    [SerializeField] private StrikeZone _strikeZone;
    [SerializeField] private GameObject _marker;
    [SerializeField] private Renderer _renderer;
    [SerializeField] private Color _strikeColor;
    [SerializeField] private Color _ballColor;
    [SerializeField] private LineRenderer _lineRenderer;

    [Header("LineRendererの設定")]
    [SerializeField] private bool _showInGame = true;  // ゲーム画面で表示
    [SerializeField] private float _lineWidth = 0.01f;  // 線の太さ
    [SerializeField] private Material _lineMaterial;  // 線のマテリアル
    [SerializeField] private Color _zoneColor = Color.green;  // ストライクゾーンの色

    private CancellationTokenSource _cts;

    private void Awake()
    {
        if (_pitchBallReleaseEvent != null) _pitchBallReleaseEvent.RegisterListener(OnRelease);
        else Debug.LogError("[StrikeZoneUI] PitchBallReleaseEventが設定されていません！");

        if (_ballReachedTargetEvent != null) _ballReachedTargetEvent.RegisterListener(OnBallReached);
        else Debug.LogError("[StrikeZoneUI] BallReachedTargetEventが設定されていません！");

        if (_onStrikeJudgeEvent != null) _onStrikeJudgeEvent.RegisterListener(ShowPrediction);
        else Debug.LogError("[StrikeZoneUI] OnStrikeJudgeEventが設定されていません！");

        if (_atBatResetEvent != null) _atBatResetEvent.RegisterListener(OnAtBatReset);
        else Debug.LogError("[StrikeZoneUI] AtBatResetEventが設定されていません！");

        if (_battingResultEvent != null) _battingResultEvent.RegisterListener(OnBatterResult);
        else Debug.LogError("[StrikeZoneUI] BattingResultEventが設定されていません！");
    }

    private void OnDestroy()
    {
        _pitchBallReleaseEvent?.UnregisterListener(OnRelease);
        _ballReachedTargetEvent?.UnregisterListener(OnBallReached);
        _onStrikeJudgeEvent?.UnregisterListener(ShowPrediction);
        _atBatResetEvent?.UnregisterListener(OnAtBatReset);
        _battingResultEvent?.UnregisterListener(OnBatterResult);
        _cts?.Cancel();
        _cts?.Dispose();
    }

    private void Start()
    {
        _cts = new CancellationTokenSource();
        _marker.SetActive(false);
        SetupLineRenderer();
    }

    private void OnRelease(PitchBallMove ball)
    {
        _lineRenderer.enabled = false;
    }


    private void OnBallReached(PitchBallMove ball)
    {
        _lineRenderer.enabled = true;
    }

    /// <summary>
    /// 投球予測を表示する
    /// </summary>
    public void ShowPrediction(bool isStrike, Vector3 crossPos)
    {
        _marker.transform.position = crossPos;
        _marker.SetActive(true);
        _renderer.material.color = isStrike ? _strikeColor : _ballColor;
    }

    private void SetupLineRenderer()
    {
        if (!_showInGame) return;

        if (_lineRenderer == null)
        {
            Debug.LogError("[StrikeZoneUI] LineRendererが設定されていません！");
            return;
        }

        // LineRendererの設定
        _lineRenderer.startWidth = _lineWidth;
        _lineRenderer.endWidth = _lineWidth;
        _lineRenderer.loop = true;  // ループして閉じた形にする
        _lineRenderer.positionCount = 4;  // 4つの頂点（矩形）
        _lineRenderer.useWorldSpace = false;  // ローカル座標を使用

        // マテリアル設定
        if (_lineMaterial != null)
        {
            _lineRenderer.material = _lineMaterial;
        }
        else
        {
            // デフォルトマテリアル（Unlit/Color）
            _lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        }

        _lineRenderer.startColor = _zoneColor;
        _lineRenderer.endColor = _zoneColor;

        // 矩形の4つの頂点を設定
        UpdateLineRendererPositions();
    }

    /// <summary>
    /// LineRendererの頂点位置を更新する
    /// </summary>
    private void UpdateLineRendererPositions()
    {
        if (_lineRenderer == null) return;

        // ストライクゾーンのサイズを取得
        float halfWidth = _strikeZone.Size.x * 0.5f;
        float halfHeight = _strikeZone.Size.y * 0.5f;

        // 矩形の4つの頂点（ローカル座標）
        Vector3[] positions = new Vector3[4]
        {
            new Vector3(-halfWidth, -halfHeight, 0),  // 左下
            new Vector3(halfWidth, -halfHeight, 0),   // 右下
            new Vector3(halfWidth, halfHeight, 0),    // 右上
            new Vector3(-halfWidth, halfHeight, 0)    // 左上
        };

        _lineRenderer.SetPositions(positions);
    }

    public void HideAfter(float seconds)
    {
        HideAsync(seconds).Forget();
    }

    private async UniTaskVoid HideAsync(float seconds)
    {
        await UniTask.Delay(
            System.TimeSpan.FromSeconds(seconds),
            cancellationToken: _cts.Token);

        _marker.SetActive(false);
    }

    public void OnInitialized(DefenseSituation situation)
    {
        _lineRenderer.enabled = false;
    }

    public void OnBatterSetCompleted()
    {
        _lineRenderer.enabled = true;
    }

    private void OnBatterResult(BattingBallResult result)
    {
        _lineRenderer.enabled = false;
        _marker.SetActive(false);
    }

    private void OnAtBatReset()
    {
        _marker.SetActive(false);
        _lineRenderer.enabled = true;
    }
}
