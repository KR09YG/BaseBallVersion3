using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// 打球制御（デバッグ強化版）
/// </summary>
public class BattingBall : MonoBehaviour
{
    [Header("イベント")]
    [SerializeField] private BattingBallTrajectoryEvent _trajectoryEvent;
    [SerializeField] private BattingResultEvent _resultEvent;

    [Header("スピン設定")]
    [SerializeField] private bool _enableSpin = true;
    [SerializeField] private float _spinSpeedMultiplier = 1.0f;

    [Header("速度調整")]
    [Tooltip("1.0 = 通常速度, 0.5 = 半分の速度, 2.0 = 2倍速")]
    [SerializeField] private float _visualSpeedMultiplier = 1.0f;

    [Header("デバッグ")]
    [SerializeField] private bool _enableDebugLogs = true;
    [SerializeField] private bool _showTrajectory = true;

    private List<Vector3> _trajectory;
    private Vector3 _spinAxis;
    private float _spinRate;
    private float _trajectoryProgress = 0f;
    private float _trajectorySpeed = 1.0f;
    private bool _isMoving = false;
    private bool _hasLanded = false;
    private BattingBallResult _result;

    public event Action<BattingBall> OnBallLanded;
    public event Action<BattingBall> OnBallCaught;

    public List<Vector3> Trajectory => _trajectory;
    public bool IsMoving => _isMoving;
    public bool HasLanded => _hasLanded;
    public BattingBallResult Result => _result;

    private void Awake()
    {
        Debug.Log("[BattingBall] Awake呼ばれた");
    }

    private void OnEnable()
    {
        Debug.Log("[BattingBall] OnEnable呼ばれた");

        // 軌道イベント購読
        if (_trajectoryEvent != null)
        {
            _trajectoryEvent.RegisterListener(OnTrajectoryReceived);
            Debug.Log("[BattingBall] 軌道イベント購読成功");
        }
        else
        {
            Debug.LogError("[BattingBall] BattingBallTrajectoryEventが設定されていません");
        }

        // 結果イベント購読
        if (_resultEvent != null)
        {
            _resultEvent.RegisterListener(OnResultReceived);
            Debug.Log("[BattingBall] 結果イベント購読成功");
        }
        else
        {
            Debug.LogError("[BattingBall] BattingResultEventが設定されていません");
        }
    }

    private void OnDisable()
    {
        Debug.Log("[BattingBall] OnDisable呼ばれた");

        if (_trajectoryEvent != null)
        {
            _trajectoryEvent.UnregisterListener(OnTrajectoryReceived);
        }

        if (_resultEvent != null)
        {
            _resultEvent.UnregisterListener(OnResultReceived);
        }
    }

    /// <summary>
    /// 軌道を受け取る（BattingBallTrajectoryEventから呼ばれる）
    /// </summary>
    private void OnTrajectoryReceived(List<Vector3> trajectory)
    {
        Debug.Log($"[BattingBall] ★OnTrajectoryReceived呼ばれた★ trajectory={(trajectory != null ? trajectory.Count.ToString() : "null")}");

        if (trajectory == null || trajectory.Count == 0)
        {
            Debug.LogWarning("[BattingBall] 空の軌道を受信（空振りの可能性）");
            return;
        }

        _trajectory = trajectory;
        _trajectoryProgress = 0f;
        _isMoving = false;
        _hasLanded = false;

        // 開始位置に移動
        if (_trajectory.Count > 0)
        {
            transform.position = _trajectory[0];
            Debug.Log($"[BattingBall] 開始位置に移動: {_trajectory[0]}");
        }

        Debug.Log($"[BattingBall] 軌道設定完了: {_trajectory.Count}点");
    }

    /// <summary>
    /// 結果を受け取る（BattingResultEventから呼ばれる）
    /// </summary>
    private void OnResultReceived(BattingBallResult result)
    {
        Debug.Log($"[BattingBall] ★OnResultReceived呼ばれた★ BallType={result.BallType}");

        _result = result;
        _spinAxis = result.SpinAxis;
        _spinRate = result.SpinRate;

        // 空振りの場合は何もしない
        if (result.BallType == BattingBallType.Miss)
        {
            Debug.Log("[BattingBall] 空振りのため移動なし");
            return;
        }

        // 軌道が設定されていない場合は警告
        if (_trajectory == null || _trajectory.Count == 0)
        {
            Debug.LogError("[BattingBall] ★軌道データがない！★");
            return;
        }

        // 軌道速度を計算
        CalculateTrajectorySpeed(result.ExitVelocity);

        Debug.Log($"[BattingBall] StartMoving()を呼ぶ直前");
        // 自動的に移動開始
        StartMoving();
    }

    /// <summary>
    /// 打球移動開始
    /// </summary>
    public void StartMoving()
    {
        Debug.Log($"[BattingBall] ★StartMoving呼ばれた★");

        if (_trajectory == null || _trajectory.Count == 0)
        {
            Debug.LogError("[BattingBall] StartMoving: 軌道データがありません");
            return;
        }

        _isMoving = true;

        Debug.Log($"[BattingBall] 移動開始 - _isMoving={_isMoving}, 軌道点数={_trajectory.Count}, Speed={_visualSpeedMultiplier}x");
    }

    /// <summary>
    /// 打球停止
    /// </summary>
    public void StopMoving()
    {
        _isMoving = false;
        Debug.Log("[BattingBall] 移動停止");
    }

    private void Update()
    {
        // ✅ デバッグ：常に状態を表示
        if (_enableDebugLogs && _isMoving)
        {
            Debug.Log($"[BattingBall] Update - _isMoving={_isMoving}, position={transform.position}, progress={_trajectoryProgress:F3}");
        }

        if (!_isMoving || _trajectory == null || _trajectory.Count == 0)
            return;

        // 軌道に沿って移動
        MoveAlongTrajectory();

        // スピン適用
        if (_enableSpin && _spinRate > 0f)
        {
            ApplySpin();
        }
    }

    /// <summary>
    /// 軌道に沿って移動（投球Ballと同じロジック）
    /// </summary>
    private void MoveAlongTrajectory()
    {
        // 進行速度に視覚的速度倍率を適用
        _trajectoryProgress += _trajectorySpeed * _visualSpeedMultiplier * Time.deltaTime;

        if (_enableDebugLogs && Time.frameCount % 30 == 0)  // 30フレームごと
        {
            Debug.Log($"[BattingBall] MoveAlongTrajectory - progress={_trajectoryProgress:F3}, speed={_trajectorySpeed:F3}");
        }

        // 現在のインデックスと補間係数を計算
        float floatIndex = _trajectoryProgress * (_trajectory.Count - 1);
        int currentIndex = Mathf.FloorToInt(floatIndex);

        // 軌道の終端に到達
        if (currentIndex >= _trajectory.Count - 1 || _trajectoryProgress >= 1.0f)
        {
            transform.position = _trajectory[_trajectory.Count - 1];
            Land();
            return;
        }

        // 現在の点と次の点の間を補間
        float t = floatIndex - currentIndex;
        Vector3 currentPos = _trajectory[currentIndex];
        Vector3 nextPos = _trajectory[currentIndex + 1];

        transform.position = Vector3.Lerp(currentPos, nextPos, t);
    }

    /// <summary>
    /// スピンを適用（投球Ballと同じロジック）
    /// </summary>
    private void ApplySpin()
    {
        // 回転速度を計算（rpm → deg/sec、視覚的速度倍率も適用）
        float degreesPerSecond = _spinRate * 360f / 60f * _spinSpeedMultiplier * _visualSpeedMultiplier;
        transform.Rotate(_spinAxis, degreesPerSecond * Time.deltaTime, Space.World);
    }

    /// <summary>
    /// 軌道を指定速度で完了するための進行速度係数を計算（投球Ballと同じロジック）
    /// </summary>
    private void CalculateTrajectorySpeed(float velocityMs)
    {
        Debug.Log($"[BattingBall] CalculateTrajectorySpeed開始 - velocityMs={velocityMs}");

        if (_trajectory == null || _trajectory.Count < 2)
        {
            _trajectorySpeed = 1.0f;
            Debug.LogWarning("[BattingBall] 軌道が不足: speed=1.0");
            return;
        }

        // 軌道の総距離を計算
        float totalDistance = 0f;
        for (int i = 0; i < _trajectory.Count - 1; i++)
        {
            totalDistance += Vector3.Distance(_trajectory[i], _trajectory[i + 1]);
        }

        Debug.Log($"[BattingBall] 総距離={totalDistance:F2}m");

        if (totalDistance <= 0f || velocityMs <= 0f)
        {
            _trajectorySpeed = 1.0f;
            Debug.LogWarning($"[BattingBall] 無効な値: distance={totalDistance}, velocity={velocityMs}");
            return;
        }

        // 指定速度で完了するための時間
        float timeToComplete = totalDistance / velocityMs;

        // 進行速度係数（1.0 / 時間 = 1秒あたりの進行率）
        _trajectorySpeed = 1.0f / timeToComplete;

        Debug.Log($"[BattingBall] ★軌道速度計算完了★ 距離={totalDistance:F1}m, 初速={velocityMs:F1}m/s, 時間={timeToComplete:F2}s, 速度係数={_trajectorySpeed:F3}");
    }

    /// <summary>
    /// 着地処理
    /// </summary>
    private void Land()
    {
        _isMoving = false;
        _hasLanded = true;

        Debug.Log($"[BattingBall] ★着地★ 最終位置: {transform.position}");
        Debug.Log($"[BattingBall] 結果: {_result.BallType}, 飛距離: {_result.Distance:F1}m");

        OnBallLanded?.Invoke(this);
    }

    /// <summary>
    /// フィールダーに捕球された
    /// </summary>
    public void CaughtByFielder()
    {
        StopMoving();
        _hasLanded = true;
        Debug.Log("[BattingBall] フィールダーに捕球された");
        OnBallCaught?.Invoke(this);
    }

    /// <summary>
    /// 速度変更
    /// </summary>
    public void SetVisualSpeed(float multiplier)
    {
        _visualSpeedMultiplier = Mathf.Max(0.1f, multiplier);
        Debug.Log($"[BattingBall] 速度変更: {_visualSpeedMultiplier}x");
    }

    /// <summary>
    /// リセット
    /// </summary>
    public void ResetBall()
    {
        _isMoving = false;
        _hasLanded = false;
        _trajectory = null;
        _trajectoryProgress = 0f;
        _spinAxis = Vector3.zero;
        _spinRate = 0f;
        OnBallLanded = null;
        OnBallCaught = null;
        transform.rotation = Quaternion.identity;
        Debug.Log("[BattingBall] リセット完了");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Fielder"))
        {
            CaughtByFielder();
        }
    }

    private void OnDrawGizmos()
    {
        if (!_showTrajectory) return;

        if (_trajectory != null && _trajectory.Count > 1)
        {
            Gizmos.color = Color.yellow;
            for (int i = 0; i < _trajectory.Count - 1; i++)
            {
                Gizmos.DrawLine(_trajectory[i], _trajectory[i + 1]);
            }

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_trajectory[0], 0.15f);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_trajectory[_trajectory.Count - 1], 0.15f);
        }

        if (_isMoving)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, 0.12f);
        }
    }
}