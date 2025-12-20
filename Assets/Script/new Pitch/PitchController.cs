using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PitchController : MonoBehaviour
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
    [SerializeField] private string _pitchTriggerName = "Pitch";

    [Header("デバッグ設定")]
    [SerializeField] private bool _showTrajectory = true;
    [SerializeField] private LineRenderer _trajectoryLine;
    [SerializeField] private bool _enableDebugLogs = false;
    [SerializeField] private TrajectoryDebugger _trajectoryDebugger;

    private Ball _ball;
    private List<Vector3> _currentTrajectory;
    private bool _isPitching = false;

    private void Start()
    {
        CreateBall();

        // LineRendererの表示設定
        if (_trajectoryLine != null)
        {
            _trajectoryLine.enabled = _showTrajectory;
        }

        // TrajectoryDebuggerがなければ自動追加
        if (_trajectoryDebugger == null)
        {
            _trajectoryDebugger = gameObject.AddComponent<TrajectoryDebugger>();
        }

        ValidateSetup();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) && !_isPitching)
        {
            StartPitch();
        }
    }

    private void CreateBall()
    {
        if (_ballPrefab == null)
        {
            Debug.LogError("BallPrefabが設定されていません");
            return;
        }

        GameObject ballObj = Instantiate(_ballPrefab);
        _ball = ballObj.GetComponent<Ball>();

        if (_ball == null)
        {
            _ball = ballObj.AddComponent<Ball>();
        }

        _ball.gameObject.SetActive(false);
    }

    public void StartPitch()
    {
        if (_isPitching)
        {
            Debug.LogWarning("既に投球中です");
            return;
        }

        if (_releasePoint == null || _targetPoint == null)
        {
            Debug.LogError("ReleasePointまたはTargetPointが設定されていません");
            return;
        }

        _isPitching = true;

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
        int pitchIndex = Random.Range(0, _availablePresets.Count);
        ChangePitchPreset(pitchIndex);

        if (_enableDebugLogs)
        {
            Debug.Log($"OnReleaseBall - 手の位置: {_releasePoint.position}");
        }

        // 軌道計算 (静的メソッド呼び出し)
        _currentTrajectory = PitchTrajectoryCalculator.PitchCalculate(
            _currentPreset,
            _releasePoint.position,
            _targetPoint.position,
            _enableDebugLogs,
            _trajectoryDebugger
        );

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

        // ボールを投げる
        ThrowBall();
    }

    private void ThrowBall()
    {
        if (_ball == null) return;

        _ball.ResetBall();
        _ball.transform.position = _releasePoint.position;
        _ball.gameObject.SetActive(true);

        _ball.Initialize(_currentTrajectory, _currentPreset);
        _ball.OnBallReachedTarget += OnBallReachedTarget;
        _ball.StartMoving();

        if (_enableDebugLogs)
        {
            Debug.Log("ボールリリース");
        }
    }

    /// <summary>
    /// ボールがターゲットに到達したときの処理
    /// </summary>
    private void OnBallReachedTarget(Ball ball)
    {
        _isPitching = false;

        if (ball != null)
        {
            ball.OnBallReachedTarget -= OnBallReachedTarget;
        }

        StartCoroutine(HideBallAfterDelay(1.0f));
    }

    private IEnumerator HideBallAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (_ball != null)
        {
            _ball.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 球種を変更
    /// </summary>
    public void ChangePitchPreset(int index)
    {
        if (index < 0 || index >= _availablePresets.Count) return;

        _currentPreset = _availablePresets[index];
        Debug.Log($"球種変更: {_currentPreset.PitchName}");
    }

    /// <summary>
    /// 軌道を描画
    /// </summary>
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

    /// <summary>
    /// セットアップの検証
    /// </summary>
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