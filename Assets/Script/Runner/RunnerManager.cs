using UnityEngine;
using System;
using System.Collections.Generic;

public class RunnerManager : MonoBehaviour, IRunnerRunStartListener, IInitializable
{
    [SerializeField] private BaseManager _baseManager;
    [SerializeField] private OnBattingResultEvent _resultEvent;
    [SerializeField] private OnAtBatResetEvent _atBatResetEvent;
    [SerializeField] private OnDecidedCatchPlan _decidedCatchPlanEvent;

    [Header("Notify")]
    [SerializeField] private OnAllRunnersStopped _onAllRunnersStopped;

    [Header("Notify (HomeRun)")]
    [SerializeField] private OnHomeRunRunnerCompleted _onHomeRunRunnerCompleted;

    [Header("Notify (Reached Home This Play)")]
    [SerializeField] private OnRunnerReachedHomeThisPlay _onRunnerReachedHomeThisPlay;

    [Header("Runner refs (optional)")]
    [SerializeField] private Runner _batter;
    [SerializeField] private Runner _firstRunner;
    [SerializeField] private Runner _secondRunner;
    [SerializeField] private Runner _thirdRunner;

    private readonly Dictionary<RunnerType, Runner> _runners = new();
    private readonly List<Runner> _activeBaseRunners = new();
    private readonly Dictionary<Runner, BaseId> _startBase = new();
    private readonly Dictionary<Runner, float> _startTime = new();

    private DefenseSituation _currentSituation;

    private bool _inPlay;
    private bool _batterStartedThisPlay;

    private bool _batterOutOnFinish;

    private int _runningCount;

    private CatchPlan _lastPlan;
    private bool _hasPlan;

    private BaseId _pendingBatterTarget = BaseId.First;

    private bool _homeRunWaitingBatterAnim;

    private bool _isHomeRunPlay;
    private readonly HashSet<RunnerType> _homeRunParticipants = new();

    private int _homeRunTotal;
    private int _homeRunFinished;
    private bool _homeRunNotified;

    private readonly HashSet<RunnerType> _reachedHomeNotified = new();

    // -----------------------
    // token: イニング/プレイ境界を跨ぐ遅延コールバックを無視するためのトークン
    // -----------------------
    private int _playToken = 0;

    private void Awake()
    {
        EnsureRunnerExists(RunnerType.Batter, _batter, "Runner_Batter");
        EnsureRunnerExists(RunnerType.First, _firstRunner, "Runner_First");
        EnsureRunnerExists(RunnerType.Second, _secondRunner, "Runner_Second");
        EnsureRunnerExists(RunnerType.Third, _thirdRunner, "Runner_Third");
    }

    private void OnEnable()
    {
        _resultEvent?.UnregisterListener(OnBattingResult);
        _resultEvent?.RegisterListener(OnBattingResult);

        _atBatResetEvent?.UnregisterListener(OnAtBatReset);
        _atBatResetEvent?.RegisterListener(OnAtBatReset);

        _decidedCatchPlanEvent?.Unregister(OnDecidedCatchPlan);
        _decidedCatchPlanEvent?.Register(OnDecidedCatchPlan);
    }

    private void OnDisable()
    {
        _resultEvent?.UnregisterListener(OnBattingResult);
        _atBatResetEvent?.UnregisterListener(OnAtBatReset);
        _decidedCatchPlanEvent?.Unregister(OnDecidedCatchPlan);
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

    public void OnInitialized(DefenseSituation situation)
    {
        _currentSituation = situation;

        // token: イニング/初期化境界。古いコールバックを全破棄
        _playToken++;

        ResetAtBatState();

        _activeBaseRunners.Clear();
        _startBase.Clear();
        _startTime.Clear();

        if (situation != null)
        {
            SetBaseRunnerActive(RunnerType.First, situation.OnFirstBase, BaseId.First);
            SetBaseRunnerActive(RunnerType.Second, situation.OnSecondBase, BaseId.Second);
            SetBaseRunnerActive(RunnerType.Third, situation.OnThirdBase, BaseId.Third);
        }

        var batter = GetRunnerSafe(RunnerType.Batter);
        if (batter != null)
        {
            batter.SetActive(true);
            batter.SetCurrentBase(batter.transform.position, BaseId.None);
        }
    }

    private void OnAtBatReset()
    {
        // token: リセット境界。古いコールバックを全破棄
        _playToken++;

        OnInitialized(_currentSituation);
    }

    private void ResetAtBatState()
    {
        _inPlay = false;
        _batterStartedThisPlay = false;
        _batterOutOnFinish = false;
        _runningCount = 0;

        _hasPlan = false;
        _lastPlan = default;
        _pendingBatterTarget = BaseId.First;

        _homeRunWaitingBatterAnim = false;

        _isHomeRunPlay = false;
        _homeRunParticipants.Clear();

        _homeRunTotal = 0;
        _homeRunFinished = 0;
        _homeRunNotified = false;

        _reachedHomeNotified.Clear();

        foreach (var r in _runners.Values)
        {
            if (r == null) continue;
            r.CancelRun();
        }
    }

    private void SetBaseRunnerActive(RunnerType type, bool active, BaseId baseId)
    {
        var runner = GetRunnerSafe(type);
        if (runner == null) return;

        runner.SetActive(active);
        if (!active) return;

        runner.SetCurrentBase(_baseManager.GetBasePosition(baseId), baseId);

        if (!_activeBaseRunners.Contains(runner))
            _activeBaseRunners.Add(runner);
    }

    private void OnBattingResult(BattingBallResult result)
    {
        if (result == null) return;

        if (result.BallType == BattingBallType.Foul) return;
        if (result.BallType == BattingBallType.Miss) return;

        // token: 「打った」= 新しいプレイ境界（前プレイ遅延を切る）
        _playToken++;

        if (result.BallType == BattingBallType.HomeRun)
        {
            StartHomeRunForAll();
            return;
        }

        _inPlay = true;
    }

    private void OnDecidedCatchPlan(CatchPlan plan)
    {
        if (!_inPlay) return;

        _lastPlan = plan;
        _hasPlan = true;

        if (plan.IsFly)
        {
            _batterOutOnFinish = true;
            _pendingBatterTarget = BaseId.First;
            return;
        }

        StartBaseRunnersByPlan(plan);
        _pendingBatterTarget = DecideBatterTargetBase(plan);
    }

    public void OnRunnerStartRunning(Runner runner)
    {
        var batter = GetRunnerSafe(RunnerType.Batter);
        if (batter == null) return;
        if (runner != batter) return;

        if (_batterStartedThisPlay) return;
        _batterStartedThisPlay = true;

        if (!_activeBaseRunners.Contains(batter))
            _activeBaseRunners.Add(batter);

        BeginRunnerTrack(batter);

        // token: この開始時点のトークンをキャプチャ
        int token = _playToken;

        if (_homeRunWaitingBatterAnim)
        {
            _homeRunWaitingBatterAnim = false;

            _runningCount++;

            batter.StartHomeRunBatter(() =>
            {
                // token: 前イニング/前プレイなら無視
                if (token != _playToken) return;

                NotifyReachedHomeIfNeeded(batter);

                OnHomeRunRunnerFinished();

                _runningCount--;
                if (_runningCount < 0) _runningCount = 0;
                TryRaiseAllRunnersStopped();
            });

            return;
        }

        BaseId target = _hasPlan ? _pendingBatterTarget : BaseId.First;
        if (target == BaseId.None) target = BaseId.First;

        StartRunnerAsync(batter, target, null);
    }

    private void StartBaseRunnersByPlan(CatchPlan plan)
    {
        for (int i = 0; i < _activeBaseRunners.Count; i++)
        {
            var r = _activeBaseRunners[i];
            if (r == null) continue;
            if (!r.gameObject.activeInHierarchy) continue;

            if (r == GetRunnerSafe(RunnerType.Batter)) continue;

            BeginRunnerTrack(r);

            BaseId target = DecideTargetBaseForRunner(r, plan);
            if (target == BaseId.None) continue;

            StartRunnerAsync(r, target, null);
        }
    }

    private BaseId DecideTargetBaseForRunner(Runner r, CatchPlan plan)
    {
        BaseId goal1 = NextBaseOf(r.CurrentBase);
        if (goal1 == BaseId.None) return BaseId.None;

        BaseId goal2 = NextBaseOf(goal1);
        if (goal2 != BaseId.None && CanBeatThrowTo(goal2, r, plan))
        {
            BaseId goal3 = NextBaseOf(goal2);
            if (goal3 != BaseId.None && CanBeatThrowTo(goal3, r, plan))
                return goal3;

            return goal2;
        }

        return goal1;
    }

    private BaseId DecideBatterTargetBase(CatchPlan plan)
    {
        var batter = GetRunnerSafe(RunnerType.Batter);
        if (batter == null) return BaseId.First;

        if (CanBeatThrowTo(BaseId.Second, batter, plan))
        {
            if (CanBeatThrowTo(BaseId.Third, batter, plan))
            {
                if (CanBeatThrowTo(BaseId.Home, batter, plan))
                    return BaseId.Home;

                return BaseId.Third;
            }

            return BaseId.Second;
        }

        return BaseId.First;
    }

    private bool CanBeatThrowTo(BaseId baseId, Runner runner, CatchPlan plan)
    {
        if (runner == null) return false;

        float throwTime = GetThrowTimeTo(baseId, plan);
        if (throwTime == float.MaxValue) return false;

        int basesToAdvance = CalcAdvanced(runner.CurrentBase, baseId);
        if (basesToAdvance <= 0) basesToAdvance = 1;

        float runTime = runner.SecondsPerBase * basesToAdvance;

        const float margin = 0.15f;
        return runTime + margin < throwTime;
    }

    private float GetThrowTimeTo(BaseId baseId, CatchPlan plan)
    {
        return baseId switch
        {
            BaseId.First => plan.ThrowToFirstTime,
            BaseId.Second => plan.ThrowToSecondTime,
            BaseId.Third => plan.ThrowToThirdTime,
            BaseId.Home => plan.ThrowToHomeTime,
            _ => float.MaxValue
        };
    }

    private void StartHomeRunForAll()
    {
        _inPlay = true;
        _batterOutOnFinish = false;
        _hasPlan = false;

        _isHomeRunPlay = true;
        _homeRunParticipants.Clear();

        _homeRunTotal = 0;
        _homeRunFinished = 0;
        _homeRunNotified = false;

        var batter = GetRunnerSafe(RunnerType.Batter);

        var toRun = new List<Runner>(8);

        for (int i = 0; i < _activeBaseRunners.Count; i++)
        {
            var r = _activeBaseRunners[i];
            if (r == null) continue;
            if (!r.gameObject.activeInHierarchy) continue;
            if (r == batter) continue;

            toRun.Add(r);
            _homeRunParticipants.Add(r.Type);
        }

        if (batter != null && batter.gameObject.activeInHierarchy)
        {
            if (!_activeBaseRunners.Contains(batter))
                _activeBaseRunners.Add(batter);

            BeginRunnerTrack(batter);

            _homeRunParticipants.Add(RunnerType.Batter);

            _homeRunWaitingBatterAnim = true;
            _batterStartedThisPlay = false;
        }
        else
        {
            _homeRunWaitingBatterAnim = false;
        }

        _homeRunTotal = _homeRunParticipants.Count;

        // token: このHRプレイのトークンをキャプチャ
        int token = _playToken;

        for (int i = 0; i < toRun.Count; i++)
        {
            var r = toRun[i];
            BeginRunnerTrack(r);

            _runningCount++;

            _ = r.RunHomeRunAsync(() =>
            {
                // token: 前イニング/前プレイなら無視
                if (token != _playToken) return;

                NotifyReachedHomeIfNeeded(r);

                OnHomeRunRunnerFinished();

                _runningCount--;
                if (_runningCount < 0) _runningCount = 0;
                TryRaiseAllRunnersStopped();
            });
        }

        if (_runningCount == 0 && !_homeRunWaitingBatterAnim)
        {
            if (_isHomeRunPlay && !_homeRunNotified)
            {
                _homeRunNotified = true;
                _onHomeRunRunnerCompleted?.RaiseEvent(_homeRunTotal);
            }

            TryRaiseAllRunnersStopped();
        }
    }

    private void OnHomeRunRunnerFinished()
    {
        if (!_isHomeRunPlay) return;

        _homeRunFinished++;

        if (_homeRunNotified) return;
        if (_homeRunTotal <= 0) return;
        if (_homeRunFinished < _homeRunTotal) return;

        _homeRunNotified = true;
        _onHomeRunRunnerCompleted?.RaiseEvent(_homeRunTotal);
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

    private void BeginRunnerTrack(Runner r)
    {
        if (r == null) return;
        if (!_startBase.ContainsKey(r)) _startBase[r] = r.CurrentBase;
        if (!_startTime.ContainsKey(r)) _startTime[r] = Time.time;
    }

    private void StartRunnerAsync(Runner r, BaseId target, Action onCompletedExtra)
    {
        if (r == null) return;

        // token: この走塁開始時のトークンをキャプチャ
        int token = _playToken;

        _runningCount++;

        _ = r.RunToBaseAsync(target, onCompleted: () =>
        {
            // token: 前イニング/前プレイなら無視
            if (token != _playToken) return;

            NotifyReachedHomeIfNeeded(r);

            onCompletedExtra?.Invoke();

            _runningCount--;
            if (_runningCount < 0) _runningCount = 0;

            TryRaiseAllRunnersStopped();
        });
    }

    private void TryRaiseAllRunnersStopped()
    {
        if (!_inPlay) return;
        if (_runningCount != 0) return;

        if (_homeRunWaitingBatterAnim) return;

        var summary = BuildRunningSummary();
        _onAllRunnersStopped?.RaiseEvent(summary);

        _inPlay = false;
        _batterOutOnFinish = false;
        _startBase.Clear();
        _startTime.Clear();

        _isHomeRunPlay = false;
        _homeRunParticipants.Clear();

        _homeRunTotal = 0;
        _homeRunFinished = 0;
        _homeRunNotified = false;

        _reachedHomeNotified.Clear();
    }

    private RunningSummary BuildRunningSummary()
    {
        var list = new List<RunnerFinalState>(4);

        foreach (var kv in _runners)
        {
            var type = kv.Key;
            var r = kv.Value;
            if (r == null) continue;
            if (_activeBaseRunners.Contains(r) == false) continue;

            BaseId start = _startBase.TryGetValue(r, out var s) ? s : r.CurrentBase;
            BaseId end = r.CurrentBase;

            int advanced = CalcAdvanced(start, end);

            bool reachedHome = (end == BaseId.Home);

            bool isOut = (_batterOutOnFinish && type == RunnerType.Batter);

            if (isOut) reachedHome = false;

            list.Add(new RunnerFinalState
            {
                RunnerType = type,
                StartBase = start,
                EndBase = end,
                AdvancedBases = advanced,
                ReachedHomeThisPlay = reachedHome,
                IsOutThisPlay = isOut
            });
        }

        return new RunningSummary { States = list.ToArray() };
    }

    private static int CalcAdvanced(BaseId start, BaseId end)
    {
        int s = (int)start;
        int e = (int)end;

        if (start == BaseId.None) s = 0;
        if (end == BaseId.None) e = 0;

        int adv = e - s;
        return Mathf.Max(0, adv);
    }

    public int GetAllRunningETAs(List<RunnerETA> buffer, bool sortByRemaining = true)
    {
        if (buffer == null) return 0;
        buffer.Clear();

        for (int i = 0; i < _activeBaseRunners.Count; i++)
        {
            var r = _activeBaseRunners[i];
            if (r == null) continue;
            if (!r.gameObject.activeInHierarchy) continue;
            if (!r.IsRunning) continue;

            buffer.Add(new RunnerETA(r, r.TargetBase, r.RemainingTimeToTarget));
        }

        if (sortByRemaining)
            buffer.Sort((a, b) => a.Remaining.CompareTo(b.Remaining));

        return buffer.Count;
    }

    private Runner GetRunnerSafe(RunnerType type)
    {
        _runners.TryGetValue(type, out var r);
        return r;
    }

    public Runner GetRunner(RunnerType type) => _runners[type];

    private static BaseId NextBaseOf(BaseId baseId)
    {
        return baseId switch
        {
            BaseId.None => BaseId.First,
            BaseId.First => BaseId.Second,
            BaseId.Second => BaseId.Third,
            BaseId.Third => BaseId.Home,
            _ => BaseId.None
        };
    }
}
