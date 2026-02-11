using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine.UIElements;

public class PitchController : MonoBehaviour, IInitializable
{
    [Header("必須参照")]
    [SerializeField] private Animator _pitcherAnimator;
    [SerializeField] private Transform _releasePoint;
    [SerializeField] private Transform _targetPoint;
    [SerializeField] private GameObject _ballPrefab;

    [Header("プリセット設定")]
    [SerializeField] private List<PitchPreset> _availablePresets = new List<PitchPreset>();
    [SerializeField] private PitchPreset _currentPreset;

    [Header("アニメーション設定")]
    [SerializeField] private string _idleTriggerName = "Idle";
    [SerializeField] private string _pitchTriggerName = "Pitch";

    [Header("デバッグ設定")]
    [SerializeField] private bool _showTrajectory = true;
    [SerializeField] private LineRenderer _trajectoryLine;
    [SerializeField] private bool _enableDebugLogs = false;

    [Header("イベント")]
    [SerializeField] private OnPitchBallReleaseEvent _ballReleaseEvent;
    [SerializeField] private OnBallSpawnedEvent _ballSpawnedEvent;
    [SerializeField] private OnStartPitchEvent _startPitchEvent;
    [SerializeField] private OnBallReachedTargetEvent _ballReachedTargetEvent;
    [SerializeField] private OnAtBatResetEvent _atBatResetEvent;

    private PitchBallMove _ball;
    private List<Vector3> _currentTrajectory;
    private bool _isPitching = false;
    private CancellationTokenSource _cancellationTokenSource;
    private Quaternion _initRotation;
    private Vector3 _initPosition;
    

    private void Awake()
    {
        if (_startPitchEvent != null) _startPitchEvent.RegisterListener(StartPitch);
        else Debug.LogWarning("[PitchController] StartPitchEventが設定されていません");
        if (_ballReachedTargetEvent != null) _ballReachedTargetEvent.RegisterListener(OnBallReachedTarget);
        else Debug.LogWarning("[PitchController] BallReachedTargetEventが設定されていません");
        if (_atBatResetEvent != null) _atBatResetEvent.RegisterListener(OnAtBatReset);
        else Debug.LogWarning("[PitchController] AtBatResetEventが設定されていません");

        _initRotation = transform.rotation;
        _initPosition = transform.position;
    }

    private void Start()
    {
        _cancellationTokenSource = new CancellationTokenSource();

        CreateBall();

        if (_trajectoryLine != null)
        {
            _trajectoryLine.enabled = _showTrajectory;
        }

        ValidateSetup();
    }

    private void OnDestroy()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        if (_startPitchEvent != null)
        {
            _startPitchEvent.UnregisterListener(StartPitch);
        }
    }

    /// <summary>
    /// 初期化処理
    /// </summary>
    public void OnInitialized(DefenseSituation situation)
    {
        _isPitching = false;

        if (_pitcherAnimator != null)
        {
            _pitcherAnimator.SetTrigger(_idleTriggerName);
            _pitcherAnimator.applyRootMotion = true;
        }
        transform.rotation = _initRotation;
        transform.position = _initPosition;
    }

    private void OnAtBatReset()
    {
        if (_pitcherAnimator != null)
        {
            _pitcherAnimator.Play("Idle");
            _pitcherAnimator.applyRootMotion = true;
        }
        transform.rotation = _initRotation;
        transform.position = _initPosition;
        _isPitching = false;
    }

    private void CreateBall()
    {
        if (_ballPrefab == null)
        {
            Debug.LogError("BallPrefabが設定されていません");
            return;
        }

        GameObject ballObj = Instantiate(_ballPrefab);
        _ball = ballObj.GetComponent<PitchBallMove>();
        _ballSpawnedEvent.RaiseEvent(ballObj);

        if (_ball == null)
        {
            _ball = ballObj.AddComponent<PitchBallMove>();
        }

        _ball.gameObject.SetActive(false);
    }

    public void StartPitch()
    {
        if (_releasePoint == null || _targetPoint == null)
        {
            Debug.LogError("ReleasePointまたはTargetPointが設定されていません");
            return;
        }

        if (_enableDebugLogs)
        {
            Debug.Log($"投球開始: {_currentPreset.PitchName}");
        }

        if (_pitcherAnimator != null)
        {
            _pitcherAnimator.SetTrigger(_pitchTriggerName);
        }
        else
        {
            OnReleaseBall();
        }
    }

    /// <summary>
    /// アニメーションイベントから呼ばれる関数
    /// </summary>
    public void OnReleaseBall()
    {
        OnReleaseBallAsync().Forget();
    }

    /// <summary>
    /// ボールリリース処理（非同期）
    /// </summary>
    private async UniTaskVoid OnReleaseBallAsync()
    {
        int pitchIndex = Random.Range(0, _availablePresets.Count);
        ChangePitchPreset(pitchIndex);
        _ball.transform.position = _releasePoint.position;

        if (_enableDebugLogs)
        {
            Debug.Log($"OnReleaseBall - 手の位置: {_releasePoint.position}");
        }

        // 軌道計算（非同期）
        await CalculateTrajectoryAsync();

        if (_currentTrajectory == null || _currentTrajectory.Count == 0)
        {
            Debug.LogError("軌道の計算に失敗しました");
            _isPitching = false;
            return;
        }

        // LineRendererで描画
        if (_showTrajectory && _trajectoryLine != null)
        {
            DrawTrajectory(_currentTrajectory);
        }

        // ボールを投げる（軌道計算完了後）
        ThrowBall();
    }

    /// <summary>
    /// 軌道を計算（非同期版）
    /// </summary>
    private async UniTask CalculateTrajectoryAsync()
    {
        if (_enableDebugLogs)
        {
            Debug.Log("========== 軌道計算開始 ==========");
            Debug.Log($"Release: {_releasePoint.position}");
            Debug.Log($"Target: {_targetPoint.position}");
            Debug.Log($"球種: {_currentPreset.PitchName}");
        }

        var parameters = _currentPreset.CreateParameters(
            _releasePoint.position,
            _targetPoint.position
        );

        if (_enableDebugLogs)
        {
            Debug.Log($"Spin Axis: {parameters.SpinAxis}");
            Debug.Log($"Spin Rate: {parameters.SpinRate} rpm");
            Debug.Log($"Lift Coefficient: {parameters.LiftCoefficient}");
            Debug.Log($"Velocity: {parameters.Velocity} m/s ({_currentPreset.VelocityKmh} km/h)");
        }

        // 別スレッドで軌道計算
        _currentTrajectory = await UniTask.RunOnThreadPool(() =>
        {
            return BallPhysicsCalculator.CalculateTrajectory(parameters);
        });

        // メインスレッドに戻る
        await UniTask.SwitchToMainThread();

        Debug.Log($"{_currentTrajectory.Count}個の軌道ポイントを計算しました");

        if (_enableDebugLogs)
        {
            Debug.Log($"軌道ポイント数: {_currentTrajectory.Count}");

            if (_currentTrajectory.Count > 0)
            {
                Debug.Log($"軌道開始点: {_currentTrajectory[0]}");
                Debug.Log($"軌道終点: {_currentTrajectory[_currentTrajectory.Count - 1]}");

                float curveAmount = CalculateTotalCurve(_currentTrajectory);
                Debug.Log($"変化量: {curveAmount * 100f:F2}cm");
            }

            Debug.Log("========== 軌道計算完了 ==========");
        }
    }

    private void ThrowBall()
    {
        if (_ball == null) return;

        _ball.transform.position = _releasePoint.position;
        _ball.gameObject.SetActive(true);

        if (_enableDebugLogs)
        {
            Debug.Log("ボールリリース");
            Debug.Log($"[PitchController] Ball.Trajectory: {(_ball.Trajectory != null ? _ball.Trajectory.Count.ToString() : "null")}");
        }

        _ball.Initialize(_currentTrajectory, _currentPreset);

        // 軌道セット後にイベント発火
        if (_ballReleaseEvent != null)
        {
            _ballReleaseEvent.RaiseEvent(_ball);

            if (_enableDebugLogs)
            {
                Debug.Log("[PitchController] Ball Release Event発火");
            }
        }
        else
        {
            Debug.LogWarning("[PitchController] Ball Release Eventが設定されていません");
        }

    }

    private void OnBallReachedTarget(PitchBallMove ball)
    {
        _isPitching = false;
        Debug.Log($"[PitchController] ボールがターゲットに到達しました: {_isPitching}");
        //HideBallAfterDelayAsync(1.0f).Forget();
    }

    /// <summary>
    /// 遅延後にボールを非表示（UniTask版）
    /// </summary>
    private async UniTaskVoid HideBallAfterDelayAsync(float delay)
    {
        await UniTask.Delay(
            System.TimeSpan.FromSeconds(delay),
            cancellationToken: _cancellationTokenSource.Token
        );

        if (_ball != null)
        {
            _ball.gameObject.SetActive(false);
        }
    }

    private float CalculateTotalCurve(List<Vector3> trajectory)
    {
        if (trajectory.Count < 3) return 0f;

        Vector3 start = trajectory[0];
        Vector3 end = trajectory[trajectory.Count - 1];
        Vector3 straightLine = end - start;

        float maxDeviation = 0f;

        for (int i = 1; i < trajectory.Count - 1; i++)
        {
            Vector3 point = trajectory[i];
            float deviation = Vector3.Cross(straightLine, point - start).magnitude / straightLine.magnitude;
            if (deviation > maxDeviation)
            {
                maxDeviation = deviation;
            }
        }

        return maxDeviation;
    }

    public void ChangePitchPreset(int index)
    {
        if (index < 0 || index >= _availablePresets.Count) return;

        _currentPreset = _availablePresets[index];

        if (_enableDebugLogs)
        {
            Debug.Log($"球種変更: {_currentPreset.PitchName}");
        }
    }

    private void DrawTrajectory(List<Vector3> trajectory)
    {
        if (_trajectoryLine == null || trajectory == null) return;

        _trajectoryLine.positionCount = trajectory.Count;
        _trajectoryLine.SetPositions(trajectory.ToArray());

        if (_currentPreset != null)
        {
            _trajectoryLine.startColor = _currentPreset.TrajectoryColor;
            _trajectoryLine.endColor = _currentPreset.TrajectoryColor;
        }

        _trajectoryLine.useWorldSpace = true;
        _trajectoryLine.numCapVertices = 5;
        _trajectoryLine.numCornerVertices = 5;
    }

    private void ValidateSetup()
    {
        if (_pitcherAnimator == null)
            Debug.LogWarning("[PitchController] Animatorが設定されていません");

        if (_releasePoint == null)
            Debug.LogError("[PitchController] ❌ ReleasePointが必要です");

        if (_targetPoint == null)
            Debug.LogError("[PitchController] ❌ TargetPointが必要です");

        if (_ballPrefab == null)
            Debug.LogError("[PitchController] ❌ BallPrefabが必要です");

        if (_currentPreset == null && _availablePresets.Count == 0)
            Debug.LogError("[PitchController] ❌ PitchPresetが必要です");
    }

    private void OnDrawGizmos()
    {
        if (_releasePoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_releasePoint.position, 0.1f);
        }

        if (_targetPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_targetPoint.position, 0.15f);
        }
    }
}