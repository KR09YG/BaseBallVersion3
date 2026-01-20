using System;
using UnityEngine;

public class Runner : MonoBehaviour
{
    [SerializeField] private RunnerData _data;

    public BaseId CurrentBase { get; private set; }
    public BaseId TargetBase { get; private set; } = BaseId.None;

    public bool IsRunning { get; private set; }
    public float RemainingTimeToTarget { get; private set; }
    public float TotalTimeToTarget { get; private set; }

    [SerializeField] private MonoBehaviour _runStartListener;
    private IRunnerRunStartListener _listener;

    private Vector3 _startPos;
    private Vector3 _targetPos;
    private float _delayRemaining;
    private Action _onArrive;

    private void Awake()
    {
        if (_data.Type == RunnerType.Batter)
        {
            _listener = _runStartListener as IRunnerRunStartListener;
        }
    }

    /// <summary>
    /// バッターのみバッティングアニメ終了時にActiveRunnerへ登録するため
    /// </summary>
    public void AnimEvent_NotifyStartRunning()
    {
        _listener?.OnRunnerStartRunning(this);
    }

    public void SetActive(bool active) => gameObject.SetActive(active);

    public void SetCurrentBase(Vector3 basePos, BaseId baseId)
    {
        CurrentBase = baseId;
        TargetBase = BaseId.None;
        IsRunning = false;

        RemainingTimeToTarget = 0f;
        TotalTimeToTarget = 0f;
        _delayRemaining = 0f;

        transform.position = basePos;
        _onArrive = null;
    }

    /// <summary>
    /// 次塁へ走り始める（バッターのアニメ後開始はdelaySecondsで対応可）
    /// </summary>
    public void StartRunToNextBase(Vector3 nextBasePos, BaseId nextBaseId, Action onArrive, float delaySeconds = 0f)
    {
        nextBasePos.y = 0f;

        // すでに走ってるなら無視（仕様次第で上書きでもOK）
        if (IsRunning) return;

        _startPos = transform.position;
        _targetPos = nextBasePos;
        TargetBase = nextBaseId;

        TotalTimeToTarget = Mathf.Max(0.01f, _data.SecondsPerBase);
        RemainingTimeToTarget = TotalTimeToTarget;
        _delayRemaining = Mathf.Max(0f, delaySeconds);

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

        // 到達確定
        IsRunning = false;
        transform.position = _targetPos;

        CurrentBase = TargetBase;
        TargetBase = BaseId.None;

        _onArrive?.Invoke();
        _onArrive = null;
    }
}
