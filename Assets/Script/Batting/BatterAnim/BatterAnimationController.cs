using UnityEngine;
using Cysharp.Threading.Tasks;
using System;
using System.Threading;

/// <summary>
/// バッティングアニメーション制御（Preparation統合版）
/// 改善点:
/// - 結果再生UniTaskの多重起動をキャンセルで抑止
/// - 打席シーケンスIDで遅延/二重イベントの誤適用を防止
/// - Animator.Playを行う前にパラメータ反映を優先
/// - フラグ乱立を整理し、状態を中心にガードする
/// </summary>
[RequireComponent(typeof(Animator))]
public class BattingAnimationController : MonoBehaviour, IInitializable
{
    [Header("参照")]
    [SerializeField] private Animator _animator;
    [SerializeField] private BattingIKController _ikController;

    [Header("イベント")]
    [SerializeField] private PitchBallReleaseEvent _ballReleaseEvent;
    [SerializeField] private BattingResultEvent _battingResultEvent;
    [SerializeField] private BattingInputEvent _battingInputEvent;
    [SerializeField] private SwingEvent _swingEvent;
    [SerializeField] private OnBatterReadyForPitchEvent _onBatterReadyForPitchEvent;

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

    private BattingState _currentState = BattingState.Idle;

    private Vector3 _initialPos;
    private Vector3 _initialRote;
    private Vector3 _batterboxPos;

    // Preparationの停止/再開のための状態
    private bool _isPreparationPaused;

    // 結果待ち（Impact後に結果イベントを受ける想定）
    private bool _isWaitingForResult;
    private bool _hasJudged;

    // 打席単位でのイベント整合性を取るためのシーケンスID
    private int _sequenceId;
    private int _activeSequenceId;

    // 結果再生UniTaskの多重起動を抑止するキャンセル
    private CancellationTokenSource _resultPlaybackCts;

    public BattingState CurrentState => _currentState;

    // 「入力可能」かは状態+停止状態から導出
    public bool CanSwing => _currentState == BattingState.Preparing && _isPreparationPaused;

    private void Awake()
    {
        if (_animator == null)
            _animator = GetComponent<Animator>();

        _initialPos = transform.position;
        _initialRote = transform.eulerAngles;
    }

    private void OnEnable()
    {
        if (_ballReleaseEvent != null) _ballReleaseEvent.RegisterListener(OnBallReleased);
        else Debug.LogError("[BattingAnimation] PitchBallReleaseEventが設定されていません");

        if (_battingInputEvent != null) _battingInputEvent.RegisterListener(OnSwingInput);
        else Debug.LogError("[BattingAnimation] BattingInputEventが設定されていません");

        if (_battingResultEvent != null) _battingResultEvent.RegisterListener(OnBattingResult);
        else Debug.LogError("[BattingAnimation] BattingResultEventが設定されていません");
    }

    private void OnDisable()
    {
        if (_ballReleaseEvent != null) _ballReleaseEvent.UnregisterListener(OnBallReleased);
        if (_battingInputEvent != null) _battingInputEvent.UnregisterListener(OnSwingInput);
        if (_battingResultEvent != null) _battingResultEvent.UnregisterListener(OnBattingResult);

        CancelResultPlayback();
    }

    private void OnDestroy()
    {
        CancelResultPlayback();
    }

    private void CancelResultPlayback()
    {
        if (_resultPlaybackCts != null)
        {
            _resultPlaybackCts.Cancel();
            _resultPlaybackCts.Dispose();
            _resultPlaybackCts = null;
        }
    }

    public void BatterReady()
    {
        Debug.Log("[BattingAnimation] バッター準備完了通知");
        if (_onBatterReadyForPitchEvent != null)
        {
            _batterboxPos = transform.position;
            _onBatterReadyForPitchEvent.RaiseEvent();
        }
        else
        {
            Debug.LogWarning("_onBatterReadyForPitchEventが未設定です");
        }
    }

    /// <summary>
    /// ボールリリース時（PitchBallReleaseEventから呼ばれる）
    /// </summary>
    private void OnBallReleased(PitchBallMove ball)
    {
        if (_currentState != BattingState.Idle)
        {
            if (_enableDebugLogs)
                Debug.LogWarning("[BattingAnimation] Idle状態以外でボールがリリースされました");
            return;
        }

        // 打席開始としてシーケンス更新
        _sequenceId++;
        _activeSequenceId = _sequenceId;

        if (_enableDebugLogs)
            Debug.Log($"[BattingAnimation] ボールリリース検知 (seq={_activeSequenceId}) → Preparation開始");

        CancelResultPlayback();

        _currentState = BattingState.Preparing;
        _hasJudged = false;
        _isWaitingForResult = false;
        _isPreparationPaused = false;

        _animator.speed = 1f;

        // 可能なら初期化（Controller構成によっては有効）
        _animator.ResetTrigger(_preparationTrigger);
        _animator.SetInteger(_resultTypeParameter, -1);

        _animator.SetTrigger(_preparationTrigger);

        _ikController?.OnPreparationStarted();
    }

    /// <summary>
    /// 準備完了（Animation Eventから呼ばれる）
    /// </summary>
    public void OnPreparationComplete()
    {
        if (_currentState != BattingState.Preparing)
            return;

        if (_isPreparationPaused)
        {
            if (_enableDebugLogs)
                Debug.LogWarning("[BattingAnimation] OnPreparationComplete: 既に停止済み");
            return;
        }

        if (_enableDebugLogs)
            Debug.Log($"[BattingAnimation] 準備完了 (seq={_activeSequenceId}) → アニメーション一時停止");

        // 注意: Animator.speed=0は全レイヤー停止になる
        // 演出拡張が増える場合は、待機ステート/ループ区間で止める設計も検討
        _animator.speed = 0f;
        _isPreparationPaused = true;

        _ikController?.OnPreparationCompleted();

        if (_enableDebugLogs)
            Debug.Log("[BattingAnimation] 入力待機中");
    }

    /// <summary>
    /// スイング入力時（BattingInputEventから呼ばれる）
    /// </summary>
    private void OnSwingInput()
    {
        if (!CanSwing)
        {
            if (_enableDebugLogs)
                Debug.LogWarning("[BattingAnimation] まだスイングできません");
            return;
        }

        if (_enableDebugLogs)
            Debug.Log($"[BattingAnimation] スイング入力 (seq={_activeSequenceId}) → Preparation再開");

        _currentState = BattingState.Swinging;
        _isPreparationPaused = false;
        _isWaitingForResult = true;

        _animator.speed = 1f;

        _ikController?.OnSwingStarted();
    }

    /// <summary>
    /// インパクトタイミング（Animation Eventから呼ばれる）
    /// </summary>
    public void OnImpactTiming()
    {
        if (_currentState != BattingState.Swinging)
        {
            // ここが複数回呼ばれた場合などの保険
            if (_enableDebugLogs)
                Debug.LogWarning($"[BattingAnimation] OnImpactTiming: 想定外の状態({_currentState}) (seq={_activeSequenceId})");
            return;
        }

        if (_enableDebugLogs)
            Debug.Log($"[BattingAnimation] インパクトタイミング (seq={_activeSequenceId}) → 判定実行");

        _currentState = BattingState.Impact;

        _ikController?.OnImpactTiming();

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
                Debug.Log($"[BattingAnimation] SwingEvent発火 (seq={_activeSequenceId}) → 判定実行");

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
        // 状態上、結果待ちでなければ無視
        if (!_isWaitingForResult)
            return;

        if (_hasJudged)
        {
            if (_enableDebugLogs)
                Debug.LogWarning($"[BattingAnimation] 既に判定済みです (seq={_activeSequenceId})");
            return;
        }

        _hasJudged = true;
        _isWaitingForResult = false;
        _currentState = BattingState.FollowThrough;

        if (_enableDebugLogs)
            Debug.Log($"[BattingAnimation] 打球結果 (seq={_activeSequenceId}): {result.BallType}, 距離: {result.Distance:F1}m");

        SwitchToResultAnimation(result.BallType);

        _ikController?.OnImpactCompleted(result.BallType);
    }

    /// <summary>
    /// 結果アニメーションに切り替え
    /// </summary>
    private void SwitchToResultAnimation(BattingBallType ballType)
    {
        // 先にパラメータを反映（Controller側の遷移で上書きされる事故を減らす）
        _animator.SetInteger(_resultTypeParameter, (int)ballType);

        if (ballType == BattingBallType.Miss)
        {
            if (_enableDebugLogs)
                Debug.Log($"[BattingAnimation] 空振り (seq={_activeSequenceId}) → Preparation続行");

            // 何もしない（既にPreparationアニメーション再生中）
            return;
        }

        string stateName = GetResultStateName(ballType);

        if (_enableDebugLogs)
            Debug.Log($"[BattingAnimation] {ballType} (seq={_activeSequenceId}) → {stateName} を途中から再生");

        // 前回の結果再生が残っていたら止める
        CancelResultPlayback();
        _resultPlaybackCts = new CancellationTokenSource();

        // Disable/Destroyで止めたいので破棄トークンとリンク
        CancellationToken linkedToken = CancellationTokenSource
            .CreateLinkedTokenSource(_resultPlaybackCts.Token, this.GetCancellationTokenOnDestroy())
            .Token;

        PlayResultAnimationWithPauseAsync(stateName, _resultStartNormalizedTime, _resultAnimationPauseTime, linkedToken).Forget();
    }

    /// <summary>
    /// UniTask版：結果アニメーションを一時停止してから再生
    /// </summary>
    private async UniTask PlayResultAnimationWithPauseAsync(
        string stateName,
        float normalizedTime,
        float pauseSeconds,
        CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
            return;

        _animator.Play(stateName, 0, normalizedTime);

        // Play直後は反映が次フレームになることがあるため1フレーム待つ
        await UniTask.Yield(PlayerLoopTiming.Update, ct);

        if (ct.IsCancellationRequested)
            return;

        _animator.speed = 0f;

        if (_enableDebugLogs)
            Debug.Log($"[BattingAnimation] {stateName} 一時停止（normalized: {normalizedTime:F3}）");

        await UniTask.Delay(TimeSpan.FromSeconds(pauseSeconds), cancellationToken: ct);

        if (ct.IsCancellationRequested)
            return;

        _animator.speed = 1f;

        if (_enableDebugLogs)
            Debug.Log($"[BattingAnimation] {stateName} 再生開始");
    }

    private string GetResultStateName(BattingBallType ballType)
    {
        switch (ballType)
        {
            case BattingBallType.Miss:
                return _preparationStateName;
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
    /// </summary>
    public void OnAnimationComplete()
    {
        if (_enableDebugLogs)
            Debug.Log($"[BattingAnimation] アニメーション完了 (seq={_activeSequenceId}) → Idleに戻る");

        CancelResultPlayback();

        _currentState = BattingState.Idle;
        _hasJudged = false;
        _isWaitingForResult = false;
        _isPreparationPaused = false;

        _animator.speed = 1f;
        _animator.SetInteger(_resultTypeParameter, -1);

        _ikController?.OnAnimationCompleted();
    }

    public bool IsSwinging()
    {
        return _currentState == BattingState.Swinging || _currentState == BattingState.Impact;
    }

    public bool IsPreparing()
    {
        return _currentState == BattingState.Preparing;
    }

    public void OnInitialized()
    {
        transform.position = _initialPos;
        transform.eulerAngles = _initialRote;
        _animator.Play("BatterReady");
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
