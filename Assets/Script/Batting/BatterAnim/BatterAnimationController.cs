using UnityEngine;
using System.Collections;

/// <summary>
/// バッティングアニメーション制御（Preparation統合版）
/// </summary>
[RequireComponent(typeof(Animator))]
public class BattingAnimationController : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private Animator _animator;
    [SerializeField] private BattingIKController _ikController;

    [Header("イベント")]
    [SerializeField] private PitchBallReleaseEvent _ballReleaseEvent;
    [SerializeField] private BattingResultEvent _battingResultEvent;
    [SerializeField] private BattingInputEvent _battingInputEvent;
    [SerializeField] private SwingEvent _swingEvent;

    [Header("アニメーションパラメータ名")]
    [SerializeField] private string _preparationTrigger = "Prepare";
    [SerializeField] private string _resultTypeParameter = "ResultType";

    [Header("アニメーション名")]
    [SerializeField] private string _preparationStateName = "Preparation";
    [SerializeField] private string _foulStateName = "Foul";
    [SerializeField] private string _groundBallStateName = "GroundBall";
    [SerializeField] private string _hitStateName = "Hit";
    [SerializeField] private string _homeRunStateName = "HomeRun";

    [Header("途中再生設定")]
    [Tooltip("結果アニメーションの開始位置（normalized time）")]
    [Range(0f, 1f)]
    [SerializeField] private float _resultStartNormalizedTime = 0.617f;

    [Tooltip("途中再生時の一時停止時間（秒）")]
    [SerializeField] private float _resultAnimationPauseTime = 0.1f;

    [Header("デバッグ")]
    [SerializeField] private bool _enableDebugLogs = true;

    private bool _isWaitingForResult = false;
    private BattingState _currentState = BattingState.Idle;
    private bool _canSwing = false;
    private bool _hasJudged = false;
    private bool _isPreparationPaused = false;

    public BattingState CurrentState => _currentState;
    public bool CanSwing => _canSwing;

    private void Awake()
    {
        if (_animator == null)
        {
            _animator = GetComponent<Animator>();
        }
    }

    private void OnEnable()
    {
        if (_ballReleaseEvent != null)
        {
            _ballReleaseEvent.RegisterListener(OnBallReleased);
        }
        else
        {
            Debug.LogError("[BattingAnimation] PitchBallReleaseEventが設定されていません");
        }

        if (_battingInputEvent != null)
        {
            _battingInputEvent.RegisterListener(OnSwingInput);
        }
        else
        {
            Debug.LogError("[BattingAnimation] BattingInputEventが設定されていません");
        }

        if (_battingResultEvent != null)
        {
            _battingResultEvent.RegisterListener(OnBattingResult);
        }
        else
        {
            Debug.LogError("[BattingAnimation] BattingResultEventが設定されていません");
        }
    }

    private void OnDisable()
    {
        if (_ballReleaseEvent != null)
        {
            _ballReleaseEvent.UnregisterListener(OnBallReleased);
        }

        if (_battingInputEvent != null)
        {
            _battingInputEvent.UnregisterListener(OnSwingInput);
        }

        if (_battingResultEvent != null)
        {
            _battingResultEvent.UnregisterListener(OnBattingResult);
        }
    }

    /// <summary>
    /// ボールリリース時（PitchBallReleaseEventから呼ばれる）
    /// </summary>
    private void OnBallReleased(PitchBallMove ball)
    {
        if (_currentState != BattingState.Idle)
        {
            Debug.LogWarning("[BattingAnimation] Idle状態以外でボールがリリースされました");
            return;
        }

        if (_enableDebugLogs)
        {
            Debug.Log($"[BattingAnimation] ボールリリース検知 → Preparation開始");
        }

        _currentState = BattingState.Preparing;
        _canSwing = false;
        _hasJudged = false;
        _isPreparationPaused = false;
        _isWaitingForResult = false;

        _animator.speed = 1f;
        _animator.SetTrigger(_preparationTrigger);

        if (_ikController != null)
        {
            _ikController.OnPreparationStarted();
        }
    }

    /// <summary>
    /// 準備完了（Animation Eventから呼ばれる）
    /// Preparationアニメーションの足が上がったタイミングに配置
    /// </summary>
    public void OnPreparationComplete()
    {
        if (_isPreparationPaused)
        {
            if (_enableDebugLogs)
            {
                Debug.LogWarning("[BattingAnimation] OnPreparationComplete: 既に停止済み");
            }
            return;
        }

        if (_enableDebugLogs)
        {
            Debug.Log("[BattingAnimation] 準備完了（足上げ完了） → アニメーション一時停止");
        }

        // アニメーションを一時停止
        _animator.speed = 0f;
        _isPreparationPaused = true;
        _canSwing = true;

        if (_ikController != null)
        {
            _ikController.OnPreparationCompleted();
        }

        if (_enableDebugLogs)
        {
            Debug.Log("[BattingAnimation] 入力待機中");
        }
    }

    /// <summary>
    /// スイング入力時（BattingInputEventから呼ばれる）
    /// </summary>
    private void OnSwingInput()
    {
        if (!_canSwing)
        {
            if (_enableDebugLogs)
            {
                Debug.LogWarning("[BattingAnimation] まだスイングできません");
            }
            return;
        }

        if (_currentState != BattingState.Preparing)
        {
            Debug.LogWarning("[BattingAnimation] Preparing状態以外でスイング入力がありました");
            return;
        }

        if (_enableDebugLogs)
        {
            Debug.Log("[BattingAnimation] スイング入力 → Preparation再開（停止位置から）");
        }

        // ✅ 停止していた位置からそのまま再開
        _currentState = BattingState.Swinging;
        _canSwing = false;
        _isPreparationPaused = false;
        _isWaitingForResult = true;

        // アニメーション再開
        _animator.speed = 1f;

        if (_ikController != null)
        {
            _ikController.OnSwingStarted();
        }
    }

    /// <summary>
    /// インパクトタイミング（Animation Eventから呼ばれる）
    /// Preparationアニメーションのインパクトタイミングに配置
    /// </summary>
    public void OnImpactTiming()
    {
        if (_enableDebugLogs)
        {
            Debug.Log("[BattingAnimation] インパクトタイミング（Animation Event） → 判定実行");
        }

        _currentState = BattingState.Impact;

        if (_ikController != null)
        {
            _ikController.OnImpactTiming();
        }

        StartBattingCalculate();
    }

    /// <summary>
    /// 打撃判定開始（SwingEventを発火）
    /// </summary>
    private void StartBattingCalculate()
    {
        if (_swingEvent != null)
        {
            if (_enableDebugLogs)
            {
                Debug.Log("[BattingAnimation] SwingEvent発火 → 判定実行");
            }

            _swingEvent.RaiseEvent();
        }
        else
        {
            Debug.LogError("[BattingAnimation] SwingEventが設定されていません");
        }
    }

    /// <summary>
    /// 打球結果を受け取る（BattingResultEventから呼ばれる）
    /// </summary>
    private void OnBattingResult(BattingBallResult result)
    {
        if (!_isWaitingForResult)
            return;

        if (_hasJudged)
        {
            Debug.LogWarning("[BattingAnimation] 既に判定済みです");
            return;
        }

        _hasJudged = true;
        _isWaitingForResult = false;
        _currentState = BattingState.FollowThrough;

        if (_enableDebugLogs)
        {
            Debug.Log($"[BattingAnimation] 打球結果: {result.BallType}, 飛距離: {result.Distance:F1}m");
        }

        SwitchToResultAnimation(result.BallType);

        if (_ikController != null)
        {
            _ikController.OnImpactCompleted(result.BallType);
        }
    }

    /// <summary>
    /// 結果アニメーションに切り替え
    /// </summary>
    private void SwitchToResultAnimation(BattingBallType ballType)
    {
        if (ballType == BattingBallType.Miss)
        {
            // ✅ 空振りの場合はそのまま Preparation を続行
            if (_enableDebugLogs)
            {
                Debug.Log($"[BattingAnimation] 空振り → {_preparationStateName} そのまま続行");
            }
            // 何もしない（既にPreparationアニメーション再生中）
        }
        else
        {
            // ✅ ヒット等の場合は途中から再生（一時停止してから開始）
            string stateName = GetResultStateName(ballType);

            if (_enableDebugLogs)
            {
                Debug.Log($"[BattingAnimation] {ballType} → {stateName} を途中から再生（normalized: {_resultStartNormalizedTime:F3}）");
            }

            StartCoroutine(PlayResultAnimationWithPause(stateName, _resultStartNormalizedTime));
        }

        _animator.SetInteger(_resultTypeParameter, (int)ballType);
    }

    /// <summary>
    /// 結果アニメーションを一時停止してから再生
    /// </summary>
    private IEnumerator PlayResultAnimationWithPause(string stateName, float normalizedTime)
    {
        // 指定位置にジャンプ
        _animator.Play(stateName, 0, normalizedTime);

        // 次のフレームまで待つ
        yield return null;

        // アニメーションを一時停止
        _animator.speed = 0f;

        if (_enableDebugLogs)
        {
            Debug.Log($"[BattingAnimation] {stateName} 一時停止（normalized: {normalizedTime:F3}）");
        }

        // 指定時間待機
        yield return new WaitForSeconds(_resultAnimationPauseTime);

        // アニメーション再開
        _animator.speed = 1f;

        if (_enableDebugLogs)
        {
            Debug.Log($"[BattingAnimation] {stateName} 再生開始");
        }
    }

    /// <summary>
    /// 結果タイプからState名を取得
    /// </summary>
    private string GetResultStateName(BattingBallType ballType)
    {
        switch (ballType)
        {
            case BattingBallType.Miss:
                return _preparationStateName;  // ✅ 空振りもPreparation
            case BattingBallType.Foul:
                return _foulStateName;
            case BattingBallType.GroundBall:
                return _groundBallStateName;
            case BattingBallType.Hit:
                return _hitStateName;
            case BattingBallType.HomeRun:
                return _homeRunStateName;
            default:
                Debug.LogError($"[BattingAnimation] 未対応の結果タイプ: {ballType}");
                return _preparationStateName;
        }
    }

    /// <summary>
    /// アニメーション完了（Animation Eventから呼ばれる）
    /// Preparationアニメーションと各結果アニメーションの最後に配置
    /// </summary>
    public void OnAnimationComplete()
    {
        if (_enableDebugLogs)
        {
            Debug.Log("[BattingAnimation] アニメーション完了 → Idle状態に戻る");
        }

        _currentState = BattingState.Idle;
        _canSwing = false;
        _hasJudged = false;
        _isPreparationPaused = false;
        _isWaitingForResult = false;

        _animator.speed = 1f;
        _animator.SetInteger(_resultTypeParameter, -1);

        if (_ikController != null)
        {
            _ikController.OnAnimationCompleted();
        }
    }

    public bool IsSwinging()
    {
        return _currentState == BattingState.Swinging || _currentState == BattingState.Impact;
    }

    public bool IsPreparing()
    {
        return _currentState == BattingState.Preparing;
    }
}

public enum BattingState
{
    Idle,
    Preparing,
    Swinging,
    Impact,
    FollowThrough
}