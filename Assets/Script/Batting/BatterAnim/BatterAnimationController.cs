using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Animator))]
public class BattingAnimationController : MonoBehaviour, IInitializable
{
    [Header("参照")]
    [SerializeField] private Animator _animator;
    [SerializeField] private BattingIKController _ikController;
    [SerializeField] private RuntimeAnimatorController _battingAnimController;

    [Header("イベント")]
    [SerializeField] private OnPitchBallReleaseEvent _ballReleaseEvent;
    [SerializeField] private OnBattingResultEvent _battingResultEvent;
    [SerializeField] private OnBattingInputEvent _battingInputEvent;
    [SerializeField] private OnSwingEvent _swingEvent;
    [SerializeField] private OnBatterReadyForPitchEvent _onBatterReadyForPitchEvent;
    [SerializeField] private OnAtBatResetEvent _atBatResetEvent;
    [SerializeField] private OnFinMissSwingEvent _finMissSwingEvent;

    [Header("アニメーション名")]
    [SerializeField] private string _swingBaseStateName = "SwingBase";
    [SerializeField] private string _idleStateName = "Idle";
    [SerializeField] private string _batterReadyStateName = "BatterReady";

    [Header("結果クリップ（Overrideで差し替え）")]
    [SerializeField] private AnimationClip _resultMissClip;
    [SerializeField] private AnimationClip _resultFoulClip;
    [SerializeField] private AnimationClip _resultGroundClip;
    [SerializeField] private AnimationClip _resultHitClip;
    [SerializeField] private AnimationClip _resultHomeRunClip;

    [Header("速度")]
    [SerializeField] private float _preContactSpeed = 2f; // 足上げ〜コンタクトまでの速度
    [SerializeField] private float _postInputSpeed = 1f;   // 入力後〜コンタクトまで

    [Header("結果クリップのコンタクトフレーム設定（Inspectorで入力）")]
    [SerializeField] private int _contactFrameHit = 0;
    [SerializeField] private int _contactFrameHomeRun = 0;
    [SerializeField] private int _contactFrameGround = 0;
    [SerializeField] private int _contactFrameFoul = 0;

    [Header("開始位置オフセット（コンタクト基準、フレーム単位）")]
    [SerializeField] private int _resultStartOffsetFrames = 0;

    [Header("開始位置Clamp（任意）")]
    [SerializeField] private bool _clampStartNormalized = true;
    [Range(0f, 1f)]
    [SerializeField] private float _minStartNormalized = 0f;
    [Range(0f, 1f)]
    [SerializeField] private float _maxStartNormalized = 0.98f;

    [Header("デバッグ")]
    [SerializeField] private bool _enableDebugLogs = true;

    private BattingState _state = BattingState.Idle;

    private bool _hasBatterBoxPose;
    private Vector3 _batterBoxPos;
    private Vector3 _batterBoxRot;

    private Vector3 _initialPos;
    private Vector3 _initialRot;

    private bool _isPausedForInput;
    private bool _waitingResult;
    private bool _hasJudged;

    private int _sequenceId;
    private int _activeSeq;

    private AnimatorOverrideController _overrideController;
    private RuntimeAnimatorController _baseController;

    // SwingBase の「元クリップ」を固定で保持
    private AnimationClip _baseSwingClip;

    private void Awake()
    {
        if (_animator == null) _animator = GetComponent<Animator>();

        _initialPos = transform.position;
        _initialRot = transform.eulerAngles;

        _baseController = _animator.runtimeAnimatorController;
        if (_baseController == null)
        {
            Debug.LogError("[BattingAnimation] RuntimeAnimatorController が Animator に設定されていません。");
            return;
        }

        // すでに Override なら、その元を採用
        if (_baseController is AnimatorOverrideController aoc && aoc.runtimeAnimatorController != null)
            _baseController = aoc.runtimeAnimatorController;

        _overrideController = new AnimatorOverrideController(_baseController);
        _animator.runtimeAnimatorController = _overrideController;

        _baseSwingClip = FindBaseSwingClipOnce();
        if (_baseSwingClip == null)
        {
            Debug.LogError("[BattingAnimation] SwingBaseの元クリップを特定できません。Override差し替えが安定しません。");
        }
    }

    private void OnEnable()
    {
        _ballReleaseEvent?.RegisterListener(OnBallReleased);
        _battingInputEvent?.RegisterListener(OnSwingInput);
        _battingResultEvent?.RegisterListener(OnBattingResult);
        _atBatResetEvent?.RegisterListener(OnAtBatReset);
    }

    private void OnDisable()
    {
        _ballReleaseEvent?.UnregisterListener(OnBallReleased);
        _battingInputEvent?.UnregisterListener(OnSwingInput);
        _battingResultEvent?.UnregisterListener(OnBattingResult);
        _atBatResetEvent?.UnregisterListener(OnAtBatReset);

        ResetRuntimeFlags();
    }

    // Timeline側から呼ぶ
    public void BatterReady()
    {
        _hasBatterBoxPose = true;
        _batterBoxPos = transform.position;
        _batterBoxRot = transform.eulerAngles;

        _onBatterReadyForPitchEvent?.RaiseEvent();
    }

    public void OnInitialized(DefenseSituation situation)
    {
        if (_enableDebugLogs) Debug.Log("[BattingAnimation] OnInitialized");

        _animator.applyRootMotion = true;
        _animator.runtimeAnimatorController = _overrideController;
        transform.position = _initialPos;
        transform.eulerAngles = _initialRot;

        FullResetToIdle(playBatterReady: true);
    }

    private void OnAtBatReset()
    {
        _sequenceId++;
        _activeSeq = _sequenceId;

        _animator.applyRootMotion = true;
        _animator.runtimeAnimatorController = _overrideController;

        if (_enableDebugLogs) Debug.Log($"[BattingAnimation] OnAtBatReset (seq={_activeSeq})");

        if (_hasBatterBoxPose)
        {
            transform.position = _batterBoxPos;
            transform.eulerAngles = _batterBoxRot;
        }

        FullResetToIdle(playBatterReady: false);
        _ikController?.OnAnimationCompleted();
    }

    private void OnBallReleased(PitchBallMove ball)
    {
        // Idle以外なら無視（前打席が戻っていない合図）
        if (_state != BattingState.Idle)
        {
            if (_enableDebugLogs)
                Debug.LogWarning($"[BattingAnimation] OnBallReleased ignored. state={_state}");
            return;
        }

        _sequenceId++;
        _activeSeq = _sequenceId;

        if (_enableDebugLogs)
            Debug.Log($"[BattingAnimation] OnBallReleased (seq={_activeSeq}) -> SwingBase start");

        // 次打席開始時に、前回の結果差し替えを必ず戻す
        RestoreSwingBaseOverride();

        _waitingResult = false;
        _hasJudged = false;
        _isPausedForInput = false;

        _animator.Rebind();
        _animator.Update(0f);

        _animator.speed = _preContactSpeed;
        _animator.Play(_swingBaseStateName, 0, 0f);

        _state = BattingState.SwingBase;

        _ikController?.OnPreparationStarted();
    }

    // SwingBase内：足上げ完了で止める（AnimationEvent）
    public void OnPreparationComplete()
    {
        if (_state != BattingState.SwingBase) return;

        if (_enableDebugLogs)
            Debug.Log($"[BattingAnimation] OnPreparationComplete (seq={_activeSeq}) -> pause & wait input");

        _animator.speed = 0f;
        _isPausedForInput = true;

        _ikController?.OnPreparationCompleted();
    }

    private void OnSwingInput()
    {
        if (_state != BattingState.SwingBase) return;
        if (!_isPausedForInput) return;

        if (_enableDebugLogs)
            Debug.Log($"[BattingAnimation] OnSwingInput (seq={_activeSeq}) -> resume to contact");

        _animator.speed = _postInputSpeed;
        _isPausedForInput = false;

        _waitingResult = true;
        _hasJudged = false;

        _ikController?.OnSwingStarted();
    }

    // SwingBase内：コンタクトフレーム（AnimationEvent）
    public void OnImpactTiming()
    {
        if (_state != BattingState.SwingBase) return;

        if (_enableDebugLogs)
            Debug.Log($"[BattingAnimation] OnImpactTiming (seq={_activeSeq}) Raise SwingEvent");

        _ikController?.OnImpactTiming();
        _swingEvent?.RaiseEvent();
    }

    private void OnBattingResult(BattingBallResult result)
    {
        if (!_waitingResult) return;
        if (_hasJudged) return;

        _hasJudged = true;
        _waitingResult = false;

        if (_enableDebugLogs)
            Debug.Log($"[BattingAnimation] OnBattingResult: {result.BallType}");

        if (result.BallType == BattingBallType.Miss)
        {
            // ミスはそのままSwingBaseを最後まで流す
            if (_enableDebugLogs)
                Debug.Log("[BattingAnimation] Missed swing -> continue SwingBase to the end");
            return;
        }

        PlayResultFromContactAligned(result.BallType);
        _ikController?.OnImpactCompleted(result.BallType);
    }

    private void PlayResultFromContactAligned(BattingBallType ballType)
    {
        if (_overrideController == null || _baseSwingClip == null) return;

        var resultClip = GetResultClip(ballType);
        if (resultClip == null) return;

        // SwingBaseの元クリップを結果に差し替え
        _overrideController[_baseSwingClip] = resultClip;

        int contactFrame = GetContactFrame(ballType);
        float startNormalized = CalcStartNormalizedTime(resultClip, contactFrame, _resultStartOffsetFrames);

        if (_enableDebugLogs)
            Debug.Log($"[BattingAnimation] PlayResult clip={resultClip.name} type={ballType} contactFrame={contactFrame} startN={startNormalized:0.000}");

        // 同一ステートを指定位置から再生
        _animator.speed = 1f;
        _animator.Play(_swingBaseStateName, 0, startNormalized);
        _animator.Update(0f);

        // 結果クリップ末尾に AnimationEvent で OnResultEnd を置く想定
    }

    private int GetContactFrame(BattingBallType type)
    {
        switch (type)
        {
            case BattingBallType.Hit: return _contactFrameHit;
            case BattingBallType.HomeRun: return _contactFrameHomeRun;
            case BattingBallType.GroundBall: return _contactFrameGround;
            case BattingBallType.Foul: return _contactFrameFoul;
            default: return 0;
        }
    }

    private float CalcStartNormalizedTime(AnimationClip clip, int contactFrame, int offsetFrames)
    {
        if (clip == null) return 0f;

        float fps = clip.frameRate;
        if (fps <= 0f) fps = 60f;

        // 総フレーム数（端数込み）
        float totalFramesF = clip.length * fps;
        int totalFrames = Mathf.Max(1, Mathf.RoundToInt(totalFramesF));

        int startFrame = contactFrame + offsetFrames;
        startFrame = Mathf.Clamp(startFrame, 0, totalFrames - 1);

        float normalized = (float)startFrame / (float)totalFrames;

        if (_clampStartNormalized)
            normalized = Mathf.Clamp(normalized, _minStartNormalized, _maxStartNormalized);

        return normalized;
    }

    // 結果クリップ末尾に置く AnimationEvent
    public void OnResultEnd()
    {
        if (_enableDebugLogs)
            Debug.Log("[BattingAnimation] OnResultEnd -> back to Idle");

        FullResetToIdle(playBatterReady: false);
    }

    public void OnFinSwing()
    {
        if (_finMissSwingEvent != null) _finMissSwingEvent.RaiseEvent();
        else Debug.LogError("[BattingAnimation] FinMissSwingEventが設定されていません");
    }

    private void FullResetToIdle(bool playBatterReady)
    {
        RestoreSwingBaseOverride();
        ResetRuntimeFlags();

        _animator.Rebind();
        _animator.Update(0f);
        _animator.speed = 1f;

        if (playBatterReady && !string.IsNullOrEmpty(_batterReadyStateName))
            _animator.Play(_batterReadyStateName, 0, 0f);
        else if (!string.IsNullOrEmpty(_idleStateName))
            _animator.Play(_idleStateName, 0, 0f);

        _state = BattingState.Idle;
    }

    private void RestoreSwingBaseOverride()
    {
        if (_overrideController == null || _baseSwingClip == null) return;

        // 元に戻す（ここをやらないと次打席が勝手に結果クリップで流れる）
        _overrideController[_baseSwingClip] = _baseSwingClip;
    }

    private AnimationClip FindBaseSwingClipOnce()
    {
        var list = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        _overrideController.GetOverrides(list);

        // 「Key」が差し替え元（ベースクリップ）
        for (int i = 0; i < list.Count; i++)
        {
            var key = list[i].Key;
            if (key == null) continue;

            // 命名頼りの暫定。最終的にはInspectorで直指定が一番安全
            if (key.name.Contains("SwingBase") || key.name.Contains("Swing"))
                return key;
        }

        // fallback: 最初のキー
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].Key != null) return list[i].Key;
        }

        return null;
    }

    private AnimationClip GetResultClip(BattingBallType type)
    {
        switch (type)
        {
            case BattingBallType.Miss: return _resultMissClip;
            case BattingBallType.Foul: return _resultFoulClip;
            case BattingBallType.GroundBall: return _resultGroundClip;
            case BattingBallType.Hit: return _resultHitClip;
            case BattingBallType.HomeRun: return _resultHomeRunClip;
            default: return _resultMissClip;
        }
    }

    private void ResetRuntimeFlags()
    {
        _animator.speed = 1f;

        _state = BattingState.Idle;
        _isPausedForInput = false;
        _waitingResult = false;
        _hasJudged = false;
    }
}

public enum BattingState
{
    Idle,
    SwingBase
}