using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using System.Threading;

/// <summary>
/// ストライクゾーン通過予測マーカー
/// </summary>
public class StrikeZoneUI : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private StrikeZone _strikeZone;
    [SerializeField] private GameObject _predictionMarker;
    [SerializeField] private Renderer _markerRenderer;

    [Header("イベント")]
    [SerializeField] private PitchBallReleaseEvent _pitchBallReleaseEvent;

    [Header("表示設定")]
    [SerializeField] private Color _strikeColor = new Color(0f, 1f, 0f, 0.8f);
    [SerializeField] private Color _ballColor = new Color(1f, 0f, 0f, 0.8f);
    [SerializeField] private bool _enableDebugLogs = false;

    private Ball _currentBall;
    private Vector3 _predictedPosition;
    private bool _hasPrediction = false;
    private CancellationTokenSource _cancellationTokenSource;

    private void Start()
    {
        _cancellationTokenSource = new CancellationTokenSource();

        if (_enableDebugLogs)
        {
            Debug.Log("[StrikeZoneUI] Start");
        }

        // 初期状態でマーカー非表示
        if (_predictionMarker != null)
        {
            _predictionMarker.SetActive(false);
        }
        else
        {
            Debug.LogError("[StrikeZoneUI] ❌ Prediction Markerが設定されていません！");
        }

        // バリデーション
        if (_strikeZone == null)
        {
            Debug.LogError("[StrikeZoneUI] ❌ Strike Zoneが設定されていません！");
        }

        // イベント購読
        if (_pitchBallReleaseEvent != null)
        {
            _pitchBallReleaseEvent.RegisterListener(OnBallReleased);
            if (_enableDebugLogs)
            {
                Debug.Log("[StrikeZoneUI] PitchBallReleaseEventに購読");
            }
        }
        else
        {
            Debug.LogError("[StrikeZoneUI] ❌ PitchBallReleaseEventが設定されていません！");
        }
    }

    private void OnDestroy()
    {
        // イベント購読解除
        if (_pitchBallReleaseEvent != null)
        {
            _pitchBallReleaseEvent.UnregisterListener(OnBallReleased);
        }

        // Ball.OnBallReachedTarget購読解除
        if (_currentBall != null)
        {
            _currentBall.OnBallReachedTarget -= OnBallReachedTarget;
        }

        // キャンセル
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
    }

    /// <summary>
    /// ボールリリース時の処理（PitchBallReleaseEventから呼ばれる）
    /// </summary>
    private void OnBallReleased(Ball ball)
    {
        if (_enableDebugLogs)
        {
            Debug.Log("[StrikeZoneUI] OnBallReleased呼び出し");
        }

        // 前のBallの購読を解除
        if (_currentBall != null)
        {
            _currentBall.OnBallReachedTarget -= OnBallReachedTarget;
        }

        _currentBall = ball;
        _hasPrediction = false;

        if (_currentBall == null)
        {
            Debug.LogWarning("[StrikeZoneUI] ⚠️ Ballがnullです");
            return;
        }

        // Ball.OnBallReachedTargetを購読
        _currentBall.OnBallReachedTarget += OnBallReachedTarget;

        // 通過予測を計算
        CalculatePredictedPosition(ball);
    }

    /// <summary>
    /// ストライクゾーン通過予測位置を計算
    /// </summary>
    private void CalculatePredictedPosition(Ball ball)
    {
        if (_enableDebugLogs)
        {
            Debug.Log("[StrikeZoneUI] 予測計算開始");
        }

        if (ball == null)
        {
            Debug.LogError("[StrikeZoneUI] ❌ Ballがnullです");
            return;
        }

        List<Vector3> trajectory = ball.Trajectory;

        if (trajectory == null)
        {
            Debug.LogError("[StrikeZoneUI] ❌ 軌道データがnullです");
            return;
        }

        if (trajectory.Count < 2)
        {
            Debug.LogError($"[StrikeZoneUI] ❌ 軌道データが不足: {trajectory.Count}点");
            return;
        }

        if (_enableDebugLogs)
        {
            Debug.Log($"[StrikeZoneUI] 軌道データ: {trajectory.Count}点");
        }

        float strikeZoneZ = _strikeZone.Center.z;

        if (_enableDebugLogs)
        {
            Debug.Log($"[StrikeZoneUI] ストライクゾーンZ座標: {strikeZoneZ}");
            Debug.Log($"[StrikeZoneUI] 軌道開始Z: {trajectory[0].z}, 終了Z: {trajectory[trajectory.Count - 1].z}");
        }

        // ストライクゾーンを通過する点を探す
        for (int i = 0; i < trajectory.Count - 1; i++)
        {
            Vector3 point1 = trajectory[i];
            Vector3 point2 = trajectory[i + 1];

            if ((point1.z <= strikeZoneZ && point2.z >= strikeZoneZ) ||
                (point1.z >= strikeZoneZ && point2.z <= strikeZoneZ))
            {
                float t = Mathf.InverseLerp(point1.z, point2.z, strikeZoneZ);
                _predictedPosition = Vector3.Lerp(point1, point2, t);
                _hasPrediction = true;

                Debug.Log($"[StrikeZoneUI] ✅ 予測通過位置: {_predictedPosition}");

                ShowPredictionMarker(_predictedPosition);
                return;
            }
        }

        Debug.LogWarning("[StrikeZoneUI] ⚠️ 通過位置が見つかりませんでした");
    }

    /// <summary>
    /// 予測マーカーを表示
    /// </summary>
    private void ShowPredictionMarker(Vector3 worldPosition)
    {
        if (_predictionMarker == null)
        {
            Debug.LogError("[StrikeZoneUI] ❌ マーカーがnullで表示できません");
            return;
        }

        _predictionMarker.transform.position = worldPosition;
        _predictionMarker.SetActive(true);

        if (_enableDebugLogs)
        {
            Debug.Log($"[StrikeZoneUI] マーカー表示: 位置={worldPosition}");
        }

        bool isStrike = _strikeZone.IsInZone(worldPosition);

        if (_markerRenderer != null)
        {
            Color targetColor = isStrike ? _strikeColor : _ballColor;
            _markerRenderer.material.color = targetColor;

            if (_enableDebugLogs)
            {
                Debug.Log($"[StrikeZoneUI] 判定: {(isStrike ? "ストライク" : "ボール")}, 色: {targetColor}");
            }
        }
        else
        {
            Debug.LogWarning("[StrikeZoneUI] ⚠️ Marker Rendererが設定されていません");
        }
    }

    /// <summary>
    /// ボール到達時の処理（Ball.OnBallReachedTargetから呼ばれる）
    /// </summary>
    private void OnBallReachedTarget(Ball ball)
    {
        if (ball == null) return;

        Vector3 ballPosition = ball.transform.position;
        bool isStrike = _strikeZone.IsInZone(ballPosition);

        Debug.Log($"[StrikeZoneUI] 最終判定: {(isStrike ? "ストライク" : "ボール")}");

        if (_hasPrediction)
        {
            float error = Vector3.Distance(_predictedPosition, ballPosition);
            Debug.Log($"[StrikeZoneUI] 予測誤差: {error * 100f:F2}cm");
        }

        // 購読解除
        ball.OnBallReachedTarget -= OnBallReachedTarget;
        _currentBall = null;

        // 遅延後にマーカー非表示
        HideMarkerAsync(2f).Forget();
    }

    /// <summary>
    /// 遅延後にマーカーを非表示
    /// </summary>
    private async UniTaskVoid HideMarkerAsync(float delay)
    {
        await UniTask.Delay(
            System.TimeSpan.FromSeconds(delay),
            cancellationToken: _cancellationTokenSource.Token
        );

        if (_predictionMarker != null)
        {
            _predictionMarker.SetActive(false);

            if (_enableDebugLogs)
            {
                Debug.Log("[StrikeZoneUI] マーカー非表示");
            }
        }

        _hasPrediction = false;
    }
}