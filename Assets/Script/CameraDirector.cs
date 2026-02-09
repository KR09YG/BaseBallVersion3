using UnityEngine;
using UnityEngine.UI;
using Cinemachine;

public class CameraDirector : MonoBehaviour
{
    [Header("Events")]
    [SerializeField] private OnBattingResultEvent _battingResultEvent;
    [SerializeField] private OnAtBatResetEvent _atBatResetEvent;
    [SerializeField] private OnDefenseCompletedEvent _defensePlayFinishedEvent;
    [SerializeField] private OnAllRunnersStopped _allRunnersStoppedEvent;
    [SerializeField] private OnHomeRunRunnerCompleted _homeRunRunnerCompletedEvent;
    [SerializeField] private OnRunnerReachedHomeThisPlay _runnerReachedHomeThisPlayEvent;

    [Header("Main Camera (Cinemachine)")]
    [SerializeField] private CinemachineVirtualCamera _vcamBatterFollow;

    [Header("Main Camera Priority")]
    [SerializeField] private int _priorityNormal = 10;
    [SerializeField] private int _priorityOnHit = 50;

    [Header("Ball PIP")]
    [SerializeField] private Camera _pipCamera;
    [SerializeField] private RawImage _pipRawImage;

    [Header("Home PIP")]
    [SerializeField] private Camera _homePipCamera;
    [SerializeField] private RawImage _homePipRawImage;

    // -------------------------
    // 内部状態
    // -------------------------
    private bool _isPlayActive;
    private bool _hasDefenseFinished;
    private bool _hasRunnersStopped;

    private bool _pipRequested;
    private bool _homeRequested;

    // Foul用：守備/走塁完了を待たず、AtBatResetまでPIPを保持したい
    private bool _pipHoldUntilReset;

    private void Awake()
    {
        ApplyResetState();
    }

    private void OnEnable()
    {
        if (_battingResultEvent == null) Debug.LogError("OnBattingResultEvent が未設定");
        else _battingResultEvent?.RegisterListener(OnBattingResult);

        if (_atBatResetEvent == null) Debug.LogError("OnAtBatResetEvent が未設定");
        else _atBatResetEvent?.RegisterListener(OnAtBatReset);

        if (_defensePlayFinishedEvent == null) Debug.LogError("OnDefensePlayFinishedEvent が未設定");
        else _defensePlayFinishedEvent?.RegisterListener(OnDefenseFinished);

        if (_allRunnersStoppedEvent == null) Debug.LogError("OnAllRunnersStoppedEvent が未設定");
        else _allRunnersStoppedEvent?.RegisterListener(OnAllRunnersStopped);

        if (_runnerReachedHomeThisPlayEvent == null) Debug.LogError("OnRunnerReachedHomeThisPlayEvent が未設定");
        else _runnerReachedHomeThisPlayEvent?.RegisterListener(OnRunnerReachedHomeThisPlay);

        if (_homeRunRunnerCompletedEvent == null) Debug.LogError("OnHomeRunRunnerCompletedEvent が未設定");
        else _homeRunRunnerCompletedEvent?.RegisterListener(OnHomeRunCompleted);
    }

    private void OnDisable()
    {
        _battingResultEvent?.UnregisterListener(OnBattingResult);
        _atBatResetEvent?.UnregisterListener(OnAtBatReset);
        _defensePlayFinishedEvent?.UnregisterListener(OnDefenseFinished);
        _allRunnersStoppedEvent?.UnregisterListener(OnAllRunnersStopped);
        _runnerReachedHomeThisPlayEvent?.UnregisterListener(OnRunnerReachedHomeThisPlay);
        _homeRunRunnerCompletedEvent?.UnregisterListener(OnHomeRunCompleted);
    }

    // -------------------------------------------------
    // Reset / Init
    // -------------------------------------------------
    private void OnAtBatReset()
    {
        ApplyResetState();
    }

    private void ApplyResetState()
    {
        _isPlayActive = false;
        _hasDefenseFinished = false;
        _hasRunnersStopped = false;

        _pipRequested = false;
        _homeRequested = false;
        _pipHoldUntilReset = false;

        SetMainCameraPriority(_priorityNormal);

        ShowBallPip(false);
        ShowHomePip(false);
    }

    // -------------------------------------------------
    // Events
    // -------------------------------------------------
    private void OnBattingResult(BattingBallResult result)
    {
        if (result == null) return;

        // ミスはカメラ切替なし（必要なら外す）
        if (result.BallType == BattingBallType.Miss) return;

        // 打ったら（ファール含む）PIPは出す
        BeginPlayIfNeeded();

        // メインは「バッター追従を強制」したいなら、ファールでも上げてOK
        // （ファールで上げたくないなら、Foul分岐だけ外して）
        SetMainCameraPriority(_priorityOnHit);

        _pipRequested = true;
        ShowBallPip(true);

        // ファールは「守備完了＋走塁完了」が来ない可能性があるので Resetまで保持
        if (result.BallType == BattingBallType.Foul)
        {
            _pipHoldUntilReset = true;
            return;
        }

        _pipHoldUntilReset = false;
        TryEndPlayIfCompleted();
    }

    private void OnRunnerReachedHomeThisPlay(RunnerType runnerType)
    {
        BeginPlayIfNeeded();

        _homeRequested = true;
        ShowHomePip(true);

        TryEndPlayIfCompleted();
    }

    private void OnDefenseFinished()
    {
        if (!_isPlayActive) return;

        _hasDefenseFinished = true;
        TryEndPlayIfCompleted();
    }

    private void OnAllRunnersStopped(RunningSummary summary)
    {
        if (!_isPlayActive) return;

        _hasRunnersStopped = true;
        TryEndPlayIfCompleted();
    }

    // HRが「全員ホームに帰ってきた」通知
    // ここで「このプレイの演出を閉じる」リセット処理を入れる
    private void OnHomeRunCompleted(int runnerCount)
    {
        // HRは守備完了イベントが来ない/遅い/構成次第で変わるので、
        // ここで確実にPIP類を閉じて通常状態に戻す
        ForceEndPlayVisuals();
    }

    // -------------------------------------------------
    // Play gating
    // -------------------------------------------------
    private void BeginPlayIfNeeded()
    {
        if (_isPlayActive) return;

        _isPlayActive = true;
        _hasDefenseFinished = false;
        _hasRunnersStopped = false;
    }

    private void TryEndPlayIfCompleted()
    {
        if (!_isPlayActive) return;

        // ファールはResetまで保持したいので、ここでは閉じない
        if (_pipHoldUntilReset) return;

        if (!_hasDefenseFinished) return;
        if (!_hasRunnersStopped) return;

        ForceEndPlayVisuals();
    }

    // 守備/走塁待ちとは無関係に「表示物を閉じて通常に戻す」
    private void ForceEndPlayVisuals()
    {
        if (_pipRequested) ShowBallPip(false);
        if (_homeRequested) ShowHomePip(false);

        SetMainCameraPriority(_priorityNormal);

        _isPlayActive = false;
        _hasDefenseFinished = false;
        _hasRunnersStopped = false;

        _pipRequested = false;
        _homeRequested = false;
        _pipHoldUntilReset = false;
    }

    // -------------------------------------------------
    // Camera controls
    // -------------------------------------------------
    private void SetMainCameraPriority(int priority)
    {
        if (_vcamBatterFollow != null)
            _vcamBatterFollow.Priority = priority;
    }

    private void ShowBallPip(bool show)
    {
        if (_pipRawImage != null)
            _pipRawImage.gameObject.SetActive(show);

        if (_pipCamera != null)
            _pipCamera.enabled = show;
    }

    private void ShowHomePip(bool show)
    {
        if (_homePipRawImage != null)
            _homePipRawImage.gameObject.SetActive(show);

        if (_homePipCamera != null)
            _homePipCamera.enabled = show;
    }
}
