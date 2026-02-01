using UnityEngine;
using Cysharp.Threading.Tasks;
using System;
using System.Threading;

[RequireComponent(typeof(Animator))]
public class BattingAnimationController : MonoBehaviour, IInitializable
{
    [Header("参照")]
    [SerializeField] private Animator _animator;
    [SerializeField] private BattingIKController _ikController;
    [SerializeField] private Runner _runner;

    [Header("イベント")]
    [SerializeField] private OnPitchBallReleaseEvent _ballReleaseEvent;
    [SerializeField] private OnBattingResultEvent _battingResultEvent;
    [SerializeField] private OnBattingInputEvent _battingInputEvent;
    [SerializeField] private OnSwingEvent _swingEvent;
    [SerializeField] private OnBatterReadyForPitchEvent _onBatterReadyForPitchEvent;
    [SerializeField] private OnAtBatResetEvent _atBatResetEvent;

    [Header("アニメーションパラメータ名")]
    [SerializeField] private string _preparationTrigger = "Prepare";
    [SerializeField] private string _resultTypeParameter = "ResultType";

    [Header("アニメーション名")]
    [SerializeField] private string _preparationStateName = "Preparation";
    [SerializeField] private string _foulStateName = "Foul";
    [SerializeField] private string _groundBallStateName = "GroundBall";
    [SerializeField] private string _hitStateName = "Hit";
    [SerializeField] private string _homeRunStateName = "HomeRun";
    [SerializeField] private string _idleStateName = "Idle"; // 追加：Resetで使う

    [Header("途中再生設定")]
    [Range(0f, 1f)]
    [SerializeField] private float _resultStartNormalizedTime = 0.617f;
    [SerializeField] private float _resultAnimationPauseTime = 0.1f;

    [Header("デバッグ")]
    [SerializeField] private bool _enableDebugLogs = true;

    private BattingState _currentState = BattingState.Idle;

    private Vector3 _initialPos;
    private Vector3 _initialRote;
    private Vector3 _batterboxPos;

    private bool _isPreparationPaused;
    private bool _isWaitingForResult;
    private bool _hasJudged;

    // 「前の打席の遅延処理を無効化する」ための世代
    private int _sequenceId;
    private int _activeSequenceId;

    private CancellationTokenSource _resultPlaybackCts;

    public BattingState CurrentState => _currentState;
    public bool CanSwing => _currentState == BattingState.Preparing && _isPreparationPaused;

    private void Awake()
    {
        if (_animator == null) _animator = GetComponent<Animator>();

        _initialPos = transform.position;
        _initialRote = transform.eulerAngles;
        _batterboxPos = transform.position; // 念のため初期値を入れておく
    }

    private void OnEnable()
    {
        if (_ballReleaseEvent != null) _ballReleaseEvent.RegisterListener(OnBallReleased);
        else Debug.LogError("[BattingAnimation] PitchBallReleaseEventが設定されていません");

        if (_battingInputEvent != null) _battingInputEvent.RegisterListener(OnSwingInput);
        else Debug.LogError("[BattingAnimation] BattingInputEventが設定されていません");

        if (_battingResultEvent != null) _battingResultEvent.RegisterListener(OnBattingResult);
        else Debug.LogError("[BattingAnimation] BattingResultEventが設定されていません");

        if (_atBatResetEvent != null) _atBatResetEvent.RegisterListener(OnAtBatReset);
        else Debug.LogError("[BattingAnimation] AtBatResetEventが設定されていません");
    }

    private void OnDisable()
    {
        _ballReleaseEvent?.UnregisterListener(OnBallReleased);
        _battingInputEvent?.UnregisterListener(OnSwingInput);
        _battingResultEvent?.UnregisterListener(OnBattingResult);
        _atBatResetEvent?.UnregisterListener(OnAtBatReset);

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

    // 重要：ロジック状態・Animator状態・フラグをまとめて「安全なIdle」に戻す
    private void ResetToIdle(bool playIdleState)
    {
        CancelResultPlayback();

        _currentState = BattingState.Idle;
        _hasJudged = false;
        _isWaitingForResult = false;
        _isPreparationPaused = false;

        _animator.speed = 1f;

        // Trigger/Parameterの残りが次の遷移を壊すのを防ぐ
        if (!string.IsNullOrEmpty(_preparationTrigger))
            _animator.ResetTrigger(_preparationTrigger);

        _animator.SetInteger(_resultTypeParameter, -1);

        if (playIdleState)
            _animator.Play(_idleStateName, 0, 0f);

        _ikController?.OnAnimationCompleted();
    }

    // AtBatのリセット（イベントから呼ばれる）
    private void OnAtBatReset()
    {
        // ここで世代を進めて、残っている遅延/古いイベントを無効化する
        _sequenceId++;
        _activeSequenceId = _sequenceId;
        _animator.speed = 1f;

        transform.position = _batterboxPos;
        transform.eulerAngles = _initialRote;

        ResetToIdle(playIdleState: true);

        if (_enableDebugLogs)
            Debug.Log($"[BattingAnimation] OnAtBatReset (seq={_activeSequenceId})");
    }

    // アニメーションイベントから呼ばれる：バッターがボックスに入り切った合図
    public void BatterReady()
    {
        if (_enableDebugLogs)
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

    // PitchBallReleaseEventから呼ばれる
    private void OnBallReleased(PitchBallMove ball)
    {
        if (_currentState != BattingState.Idle)
        {
            if (_enableDebugLogs)
                Debug.LogWarning($"[BattingAnimation] Idle以外でボールがリリースされました state={_currentState}");
            return;
        }

        _sequenceId++;
        _activeSequenceId = _sequenceId;

        if (_enableDebugLogs)
            Debug.Log($"[BattingAnimation] ボールリリース検知 (seq={_activeSequenceId}) → Preparation開始");

        // 念のため、前回の残りを完全消去してから始める
        ResetToIdle(playIdleState: false);

        _currentState = BattingState.Preparing;
        _hasJudged = false;
        _isWaitingForResult = false;
        _isPreparationPaused = false;

        _animator.speed = 1f;
        _animator.SetInteger(_resultTypeParameter, -1);

        // Triggerは「前回の残り」を消してからセットする
        _animator.ResetTrigger(_preparationTrigger);
        _animator.SetTrigger(_preparationTrigger);

        _ikController?.OnPreparationStarted();
    }

    // Animation Event：準備完了
    public void OnPreparationComplete()
    {
        if (_currentState != BattingState.Preparing)
            return;

        if (_isPreparationPaused)
            return;

        if (_enableDebugLogs)
            Debug.Log($"[BattingAnimation] 準備完了 (seq={_activeSequenceId}) → 一時停止");

        _animator.speed = 0f;
        _isPreparationPaused = true;

        _ikController?.OnPreparationCompleted();
    }

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

        // ★ここ：スイング開始～インパクトまでIKをON
        _ikController?.OnSwingStarted();
    }

    // インパクトタイミング（Animation Event）内で呼ぶ
    public void OnImpactTiming()
    {
        if (_currentState != BattingState.Swinging)
        {
            if (_enableDebugLogs)
                Debug.LogWarning($"[BattingAnimation] OnImpactTiming: 想定外の状態({_currentState}) (seq={_activeSequenceId})");
            return;
        }

        if (_enableDebugLogs)
            Debug.Log($"[BattingAnimation] インパクトタイミング (seq={_activeSequenceId}) → 判定実行");

        _currentState = BattingState.Impact;

        // ★ここ：インパクトのこのフレームだけ使って直後にOFF
        _ikController?.OnImpactTiming();

        _ikController?.OnImpactTiming(); // 重複は不要。1回でOK。※ここは消して1回だけにしてね
        _swingEvent.RaiseEvent();
    }


    /// <summary>
    /// BattingResultEventから呼ばれる
    /// </summary>
    private void OnBattingResult(BattingBallResult result)
    {
        if (!_isWaitingForResult)
            return;

        if (_hasJudged)
            return;

        _hasJudged = true;
        _isWaitingForResult = false;

        if (_enableDebugLogs)
            Debug.Log($"[BattingAnimation] 打球結果 (seq={_activeSequenceId}): {result.BallType} dist={result.Distance:F1}");

        // 空振りだけは「結果アニメへ切替しない」挙動を維持
        if (result.BallType == BattingBallType.Miss)
        {
            // ここが重要：Missでも内部状態をPreparingに戻して「次の球で正常にPrepareできる」ようにする
            _currentState = BattingState.Preparing;
            // そのままアニメーションが進んでいる可能性があるので、確実に「準備待機」に戻すなら再トリガする
            // ただし挙動を変えたくない場合はコメントアウトしてもOK
            _animator.speed = 1f;
            // _animator.ResetTrigger(_preparationTrigger);
            // _animator.SetTrigger(_preparationTrigger);
            return;
        }

        _currentState = BattingState.FollowThrough;
        SwitchToResultAnimation(result.BallType);

        _ikController?.OnImpactCompleted(result.BallType);
    }

    // 結果アニメーションに切り替え（途中再生 + 一時停止 + 再開）
    private void SwitchToResultAnimation(BattingBallType ballType)
    {
        _animator.SetInteger(_resultTypeParameter, (int)ballType);

        string stateName = GetResultStateName(ballType);

        CancelResultPlayback();
        _resultPlaybackCts = new CancellationTokenSource();

        // linked CTSはDisposeしないと漏れるので、このメソッド内で作ってfinallyで破棄する
        PlayResultAnimationWithPauseAsync(
            stateName,
            _resultStartNormalizedTime,
            _resultAnimationPauseTime,
            _resultPlaybackCts.Token
        ).Forget();
    }

    /// <summary>
    /// 結果アニメーションを途中再生し、一時停止してから再開する
    /// </summary>
    private async UniTask PlayResultAnimationWithPauseAsync(
        string stateName,
        float normalizedTime,
        float pauseSeconds,
        CancellationToken ct)
    {
        // Destroy/Disableで止めたい
        using (var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, this.GetCancellationTokenOnDestroy()))
        {
            var token = linked.Token;

            if (token.IsCancellationRequested)
                return;

            _animator.speed = 1f;
            _animator.Play(stateName, 0, normalizedTime);

            await UniTask.Yield(PlayerLoopTiming.Update, token);

            if (token.IsCancellationRequested)
                return;

            _animator.speed = 0f;

            if (_enableDebugLogs)
                Debug.Log($"[BattingAnimation] {stateName} pause (normalized={normalizedTime:F3})");

            await UniTask.Delay(TimeSpan.FromSeconds(pauseSeconds), cancellationToken: token);

            if (token.IsCancellationRequested)
                return;

            _animator.speed = 1f;

            if (_enableDebugLogs)
                Debug.Log($"[BattingAnimation] {stateName} resume");
        }
    }

    /// <summary>
    /// 打球タイプに応じた結果アニメーションの状態名を取得
    /// </summary>
    private string GetResultStateName(BattingBallType ballType)
    {
        switch (ballType)
        {
            case BattingBallType.Foul:
                return _foulStateName;
            case BattingBallType.GroundBall:
                return _groundBallStateName;
            case BattingBallType.Hit:
                return _hitStateName;
            case BattingBallType.HomeRun:
                return _homeRunStateName;
            default:
                return _preparationStateName;
        }
    }

    public bool IsSwinging() => _currentState == BattingState.Swinging || _currentState == BattingState.Impact;
    public bool IsPreparing() => _currentState == BattingState.Preparing;

    public void OnInitialized(DefenseSituation situation)
    {
        transform.position = _initialPos;
        transform.eulerAngles = _initialRote;

        ResetToIdle(playIdleState: true);

        _animator.Rebind();
        _animator.Update(0f);

        // 既存挙動維持：BatterReadyを再生
        _animator.speed = 1f;
        _animator.Play("BatterReady", 0, 0f);
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
