using UnityEngine;
using System.Collections.Generic;
using System;
public readonly struct RunnerETA
{
    public readonly Runner Runner;
    public readonly BaseId TargetBase;
    public readonly float Remaining;

    public RunnerETA(Runner runner, BaseId targetBase, float remaining)
    {
        Runner = runner;
        TargetBase = targetBase;
        Remaining = remaining;
    }
}

public class RunnerManager : MonoBehaviour , IRunnerRunStartListener,IInitializable
{
    [SerializeField] private BaseManager _baseManager;

    [SerializeField] private OnBattingResultEvent _resultEvent;

    [Header("Runner refs (optional)")]
    [SerializeField] private Runner _batter;
    [SerializeField] private Runner _firstRunner;
    [SerializeField] private Runner _secondRunner;
    [SerializeField] private Runner _thirdRunner;

    private readonly Dictionary<RunnerType, Runner> _runners = new();
    private List<Runner> _activeRunners = new();

    private void Awake()
    {
        if (_resultEvent != null) _resultEvent.RegisterListener(OnBattingResult);
        else Debug.LogError("[RunnerManager] BattingResultEventが設定されていません！");

        // 必ず4人分を用意（参照があれば使い、なければ生成）
        EnsureRunnerExists(RunnerType.First, _firstRunner, "Runner_First");
        EnsureRunnerExists(RunnerType.Second, _secondRunner, "Runner_Second");
        EnsureRunnerExists(RunnerType.Third, _thirdRunner, "Runner_Third");
    }

    private void OnDestroy()
    {
        if (_resultEvent != null)
        {
            _resultEvent.UnregisterListener(OnBattingResult);
        }
    }

    private void EnsureRunnerExists(RunnerType type, Runner provided, string defaultName)
    {
        if (_runners.ContainsKey(type)) return;

        Runner runner = provided;
        if (runner == null)
        {
            var go = new GameObject(defaultName);
            go.transform.SetParent(transform);
            runner = go.AddComponent<Runner>();
        }

        _runners.Add(type, runner);
    }

    /// <summary>
    /// 現在の DefenseSituation に合わせて走者を有効化する
    /// </summary>
    public void OnInitialized(DefenseSituation situation)
    {
        if (situation == null)
        {
            Debug.LogError("Cannot initialize runners: DefenseSituation is null.");
            return;
        }
        _activeRunners.Clear();
        // 塁上走者は situation に応じて有効化
        SetRunnerActive(RunnerType.First, situation.OnFirstBase);
        SetRunnerActive(RunnerType.Second, situation.OnSecondBase);
        SetRunnerActive(RunnerType.Third, situation.OnThirdBase);

        _runners[RunnerType.First].SetCurrentBase(_baseManager.GetBasePosition(BaseId.First), BaseId.First);
        _runners[RunnerType.Second].SetCurrentBase(_baseManager.GetBasePosition(BaseId.Second), BaseId.Second);
        _runners[RunnerType.Third].SetCurrentBase(_baseManager.GetBasePosition(BaseId.Third), BaseId.Third);

        Debug.Log($"[RunnerManager] Init: 1B={situation.OnFirstBase}, 2B={situation.OnSecondBase}, 3B={situation.OnThirdBase}, Outs={situation.OutCount}");
    }

    private void SetRunnerActive(RunnerType type, bool active)
    {
        if (!_runners.TryGetValue(type, out var runner) || runner == null)
        {
            Debug.LogError($"Runner not found: {type}");
            return;
        }

        runner.SetActive(active);
        if (active) _activeRunners.Add(runner);
    }

    private void OnBattingResult(BattingBallResult result)
    {
        if (!result.IsFoul && result.IsHit)
        {
            // 打球が有効なヒットの場合、全走者を次の塁へ進める
            foreach (var runner in _activeRunners)
            {
                Action action = () =>
                {
                    Debug.Log($"[RunnerManager] Runner {runner.name} reached base {(BaseId)runner.CurrentBase + 1}");
                };
                Vector3 nextBase = _baseManager.GetBasePosition((BaseId)runner.CurrentBase + 1);
                runner.StartRunToNextBase(nextBase, (BaseId)runner.CurrentBase + 1, action);
            }
        }
    }

    /// <summary>
    /// 全走者のETAを到達時間が近い順に取得する
    /// </summary>
    public int GetAllRunningETAs(List<RunnerETA> buffer, bool sortByRemaining = true)
    {
        buffer.Clear();
        Debug.Log("[RunnerManager] Getting all running ETAs:");
        foreach (var r in _activeRunners)
        {
            if (!r.IsRunning) continue;

            buffer.Add(new RunnerETA(r, r.TargetBase, r.RemainingTimeToTarget));Debug.Log($"[RunnerManager] Runner {r.name} TargetBase: {r.TargetBase}, RemainingTime: {r.RemainingTimeToTarget:F2} sec");
        }

        if (sortByRemaining)
            buffer.Sort((a, b) => a.Remaining.CompareTo(b.Remaining));

        return buffer.Count;
    }

    public Runner GetRunner(RunnerType type) => _runners[type];

    public void OnRunnerStartRunning(Runner runner)
    {
        if (!_activeRunners.Contains(runner))
        {
            _activeRunners.Add(runner);
        }
        
        runner.StartRunToNextBase(_baseManager.GetBasePosition(BaseId.First), BaseId.First, () =>
        {
            Debug.Log($"[RunnerManager] Batter reached first base.");
        });
    }
}
