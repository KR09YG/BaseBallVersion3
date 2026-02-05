using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

public class Runner : MonoBehaviour
{
    [SerializeField] private RunnerData _data;
    [SerializeField] private BaseManager _baseManager;

    public RunnerData Data => _data;
    public RunnerType Type => _data != null ? _data.Type : RunnerType.Batter;
    public float SecondsPerBase => _data != null ? _data.SecondsPerBase : 1.0f;

    public BaseId CurrentBase { get; private set; }
    public BaseId TargetBase { get; private set; } = BaseId.None;

    public bool IsRunning { get; private set; }
    public float RemainingTimeToTarget { get; private set; }
    public float TotalTimeToTarget { get; private set; }

    [SerializeField] private MonoBehaviour _runStartListener;
    private IRunnerRunStartListener _listener;

    private Vector3 _startPos;
    private Vector3 _targetPos;
    private Action _onArrive;

    private CancellationTokenSource _runCts;

    private void Awake()
    {
        if (_data != null && _data.Type == RunnerType.Batter)
        {
            _listener = _runStartListener as IRunnerRunStartListener;
            CurrentBase = BaseId.None;
        }
    }

    // ---- Animation Event 用 ----

    /// <summary>
    /// Animation Clip から呼ぶ用（引数なし）
    /// HR開始は RunnerManager 経由で行う（完了コールバックが必要）
    /// </summary>
    public void AnimEvent_StartHomeRunBatter()
    {
        _listener?.OnRunnerStartRunning(this);

        if (_listener == null)
        {
            Debug.LogWarning("[Runner] AnimEvent_StartHomeRunBatter fired but listener is null. Fallback internal start.");
            OnHomerunBatterInternal(null);
        }
    }

    /// <summary>
    /// 既存：走り始め通知（Animation Clip から呼ぶ）
    /// </summary>
    public void AnimEvent_NotifyStartRunning()
    {
        _listener?.OnRunnerStartRunning(this);
    }

    // ---- 外部（RunnerManager）から使う用 ----

    public void StartHomeRunBatter(Action onCompleted)
    {
        OnHomerunBatterInternal(onCompleted);
    }

    private void OnHomerunBatterInternal(Action onCompleted)
    {
        CancelRun();
        _ = RunHomeRunAsync(onCompleted);
    }

    public void SetActive(bool active) => gameObject.SetActive(active);

    public void SetCurrentBase(Vector3 basePos, BaseId baseId)
    {
        CancelRun();

        CurrentBase = baseId;
        TargetBase = BaseId.None;
        IsRunning = false;

        RemainingTimeToTarget = 0f;
        TotalTimeToTarget = 0f;

        transform.position = basePos;
        _onArrive = null;
    }

    public void CancelRun()
    {
        if (_runCts != null)
        {
            _runCts.Cancel();
            _runCts.Dispose();
            _runCts = null;
        }

        IsRunning = false;
        TargetBase = BaseId.None;
        _onArrive = null;
    }

    public async UniTask RunHomeRunAsync(Action onCompleted = null)
    {
        if (_baseManager == null)
        {
            Debug.LogError("[Runner] BaseManager is null.");
            onCompleted?.Invoke();
            return;
        }

        CancelRun();
        _runCts = new CancellationTokenSource();
        var ct = _runCts.Token;

        try
        {
            if (CurrentBase == BaseId.None)
                CurrentBase = BaseId.Home;

            for (int i = 0; i < 4; i++)
            {
                ct.ThrowIfCancellationRequested();

                BaseId next = NextBaseOfForHomerun(CurrentBase);
                Vector3 nextPos = _baseManager.GetBasePosition(next);

                StartRunToNextBase(nextPos, next, null);

                float timeout = Mathf.Max(0.3f, SecondsPerBase * 2f);
                float t = 0f;

                while (IsRunning && t < timeout)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    t += Time.deltaTime;
                }

                if (IsRunning)
                {
                    IsRunning = false;
                    transform.position = nextPos;
                    CurrentBase = next;
                    TargetBase = BaseId.None;
                }

                if (CurrentBase == BaseId.Home) break;
            }

            onCompleted?.Invoke();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    public async UniTask RunToBaseAsync(BaseId targetBase, Action onCompleted = null, float startDelaySeconds = 0f)
    {
        if (_baseManager == null)
        {
            Debug.LogError("[Runner] BaseManager is null.");
            onCompleted?.Invoke();
            return;
        }

        if (targetBase == BaseId.None)
        {
            onCompleted?.Invoke();
            return;
        }

        CancelRun();
        _runCts = new CancellationTokenSource();
        var ct = _runCts.Token;

        try
        {
            if (startDelaySeconds > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(startDelaySeconds), cancellationToken: ct);

            if (CurrentBase == targetBase)
            {
                onCompleted?.Invoke();
                return;
            }

            int start = (int)CurrentBase;
            int goal = (int)targetBase;
            if (CurrentBase == BaseId.None) start = 0;

            for (int b = start; b < goal; b++)
            {
                ct.ThrowIfCancellationRequested();

                BaseId next = (BaseId)(b + 1);
                Vector3 nextPos = _baseManager.GetBasePosition(next);

                StartRunToNextBase(nextPos, next, null);

                float timeout = Mathf.Max(0.3f, SecondsPerBase * 2f);
                float t = 0f;

                while (IsRunning && t < timeout)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    t += Time.deltaTime;
                }

                if (IsRunning)
                {
                    IsRunning = false;
                    transform.position = nextPos;
                    CurrentBase = next;
                    TargetBase = BaseId.None;
                }
            }

            onCompleted?.Invoke();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    public void StartRunToNextBase(Vector3 nextBasePos, BaseId nextBaseId, Action onArrive)
    {
        if (IsRunning) return;

        nextBasePos.y = transform.position.y;

        _startPos = transform.position;
        _targetPos = nextBasePos;

        TargetBase = nextBaseId;

        TotalTimeToTarget = Mathf.Max(0.01f, SecondsPerBase);
        RemainingTimeToTarget = TotalTimeToTarget;

        _onArrive = onArrive;
        IsRunning = true;
    }

    private void Update()
    {
        if (!IsRunning) return;

        float elapsed = TotalTimeToTarget - RemainingTimeToTarget;
        elapsed += Time.deltaTime;

        RemainingTimeToTarget = Mathf.Max(0f, TotalTimeToTarget - elapsed);

        float t = Mathf.Clamp01(elapsed / TotalTimeToTarget);
        transform.position = Vector3.Lerp(_startPos, _targetPos, t);

        if (RemainingTimeToTarget > 0f) return;

        IsRunning = false;
        transform.position = _targetPos;

        CurrentBase = TargetBase;
        TargetBase = BaseId.None;

        _onArrive?.Invoke();
        _onArrive = null;
    }

    private static BaseId NextBaseOfForHomerun(BaseId baseId)
    {
        return baseId switch
        {
            BaseId.Home => BaseId.First,
            BaseId.First => BaseId.Second,
            BaseId.Second => BaseId.Third,
            BaseId.Third => BaseId.Home,
            BaseId.None => BaseId.First,
            _ => BaseId.First
        };
    }
}
