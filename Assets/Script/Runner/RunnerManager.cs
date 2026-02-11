using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

public class RunnerManager : MonoBehaviour, IInitializable
{
    [SerializeField] private List<Runner> _runners;
    [SerializeField] private BaseManager _baseManager;

    [Header("Events")]
    [SerializeField] private OnAllRunnersStopped _onAllRunnersStopped;
    [SerializeField] private OnHomeRunRunnerCompleted _onHomeRunRunnerCompleted;
    [SerializeField] private OnRunnerReachedHomeThisPlay _onRunnerReachedHomeThisPlay;

    private Dictionary<RunnerType, Runner> _runnersByType;
    private DefenseSituation _situation;

    private HashSet<RunnerType> _reachedHomeNotified = new HashSet<RunnerType>();
    private Dictionary<Runner, BaseId> _startBase = new Dictionary<Runner, BaseId>();

    private bool _isHomerun = false;
    private int _scoreRunnersCount = 0;

    // ★今回のプレイで完了を待つ対象
    private readonly HashSet<RunnerType> _pending = new HashSet<RunnerType>();
    private bool _finishedNotified = false; // 多重通知防止

    public void OnInitialized(DefenseSituation situation)
    {
        _situation = situation;
        _isHomerun = false;

        _runnersByType = new Dictionary<RunnerType, Runner>();
        foreach (var runner in _runners)
        {
            if (runner?.Data != null)
                _runnersByType[runner.Data.Type] = runner;
        }

        ResetPlayState();
        SetupBaseRunners(situation);
    }

    private void ResetPlayState()
    {
        _reachedHomeNotified.Clear();
        _startBase.Clear();
        _pending.Clear();
        _finishedNotified = false;

        foreach (var runner in _runners)
        {
            if (runner == null) continue;
            runner.SetActive(false);
        }
    }

    private void SetupBaseRunners(DefenseSituation situation)
    {
        if (situation == null) return;

        SetupRunner(GetRunner(RunnerType.First), BaseId.First, situation.OnFirstBase);
        SetupRunner(GetRunner(RunnerType.Second), BaseId.Second, situation.OnSecondBase);
        SetupRunner(GetRunner(RunnerType.Third), BaseId.Third, situation.OnThirdBase);

        var batter = GetRunner(RunnerType.Batter);
        if (batter != null)
        {
            batter.SetActive(true);
            batter.SetCurrentBase(batter.transform.position, BaseId.None);
        }
    }

    private void SetupRunner(Runner runner, BaseId startBase, bool isActive)
    {
        if (runner == null) return;

        if (!isActive)
        {
            runner.SetActive(false);
            return;
        }

        runner.SetActive(true);
        Vector3 pos = _baseManager.GetBasePosition(startBase);
        runner.SetCurrentBase(pos, startBase);

        // Y固定でホーム方向へ（寝転がり防止）
        var look = _baseManager.GetBasePosition(BaseId.Home);
        look.y = runner.transform.position.y;
        runner.transform.LookAt(look);
    }

    public Runner GetRunner(RunnerType type)
    {
        _runnersByType.TryGetValue(type, out var runner);
        return runner;
    }

    public float GetRunnerSpeed(RunnerType type)
    {
        var runner = GetRunner(type);
        return runner.SecondsPerBase;
    }

    public void ExecuteRunningPlan(RunningPlan plan)
    {
        _scoreRunnersCount = 0;
        _isHomerun = plan.IsHomerun;

        _reachedHomeNotified.Clear();
        _pending.Clear();
        _finishedNotified = false;

        if (plan.RunnerActions == null || plan.RunnerActions.Count == 0)
        {
            _onAllRunnersStopped?.RaiseEvent(BuildRunningSummary());
            return;
        }

        if (_isHomerun)
        {
            _scoreRunnersCount = plan.RunnerActions.Count;
        }

        // ★今回待つ対象を登録（RunnerType重複もここで弾ける）
        foreach (var action in plan.RunnerActions)
        {
            _pending.Add(action.RunnerType);
        }

        foreach (var action in plan.RunnerActions)
        {
            var runner = GetRunner(action.RunnerType);
            if (runner == null)
            {
                Debug.LogError($"[RunnerManager] Runner not found: {action.RunnerType}");
                // 見つからないなら「完了扱い」にして詰まらないようにする
                MarkCompleted(action.RunnerType);
                continue;
            }

            _startBase[runner] = action.StartBase;

            if (action.RunnerType == RunnerType.Batter)
            {
                SetupBatterRun(runner, action);
            }
            else
            {
                ExecuteRunnerActionAsync(runner, action).Forget();
            }
        }

        // もし全員「即完了」だった場合（通常はないが安全）
        TryFinishIfAllCompleted();
    }

    private void SetupBatterRun(Runner batter, RunnerAction action)
    {
        bool isHomeRun = (action.TargetBase == BaseId.Home && action.StartBase == BaseId.None);

        batter.SetPlannedRun(
            action.TargetBase,
            action.StartDelay,
            isHomeRun,
            onCompleted: () =>
            {
                NotifyReachedHomeIfNeeded(batter);
                MarkCompleted(RunnerType.Batter);
            });
    }

    private async UniTaskVoid ExecuteRunnerActionAsync(Runner runner, RunnerAction action)
    {
        try
        {
            await runner.RunToBaseSequentiallyAsync(
                action.StartBase,
                action.TargetBase,
                destroyCancellationToken,
                action.StartDelay);

            NotifyReachedHomeIfNeeded(runner);
            MarkCompleted(runner.Type);
        }
        catch
        {
            // 「時間保険なし」方針でも、例外で詰まるのは避けたいので完了扱い
            MarkCompleted(runner.Type);
        }
    }

    private void MarkCompleted(RunnerType type)
    {
        if (!_pending.Remove(type)) return; // 既に完了してた
        TryFinishIfAllCompleted();
    }

    private void TryFinishIfAllCompleted()
    {
        if (_finishedNotified) return;
        if (_pending.Count != 0) return;

        _finishedNotified = true;

        _onAllRunnersStopped?.RaiseEvent(BuildRunningSummary());

        if (_isHomerun)
            _onHomeRunRunnerCompleted?.RaiseEvent(_scoreRunnersCount);
    }

    private void NotifyReachedHomeIfNeeded(Runner runner)
    {
        if (runner == null) return;

        RunnerType type = runner.Type;
        if (_reachedHomeNotified.Contains(type)) return;
        if (runner.CurrentBase != BaseId.Home) return;

        _reachedHomeNotified.Add(type);
        _onRunnerReachedHomeThisPlay?.RaiseEvent(type);
    }

    private RunningSummary BuildRunningSummary()
    {
        var list = new List<RunnerFinalState>();

        foreach (var runner in _runners)
        {
            if (runner == null || !runner.gameObject.activeInHierarchy) continue;

            BaseId startBase = _startBase.TryGetValue(runner, out var start) ? start : runner.CurrentBase;
            BaseId endBase = runner.CurrentBase;

            int advanced = CalcAdvanced(startBase, endBase);

            list.Add(new RunnerFinalState
            {
                RunnerType = runner.Type,
                StartBase = startBase,
                EndBase = endBase,
                AdvancedBases = advanced,
                ReachedHomeThisPlay = (endBase == BaseId.Home),
                IsOutThisPlay = false
            });
        }

        return new RunningSummary { States = list.ToArray() };
    }

    private static int CalcAdvanced(BaseId start, BaseId end)
    {
        int s = (start == BaseId.None) ? 0 : (int)start;
        int e = (end == BaseId.None) ? 0 : (int)end;
        return Mathf.Max(0, e - s);
    }
}
