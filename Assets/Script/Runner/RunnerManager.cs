using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ランナーの動きを管理
/// PlayJudgeから計画を受け取って、ランナーを走らせる
/// バッターはAnimationEvent待ち、既存ランナーは即実行
/// </summary>
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

    private int _runningCount = 0;
    private HashSet<RunnerType> _reachedHomeNotified = new HashSet<RunnerType>();
    private Dictionary<Runner, BaseId> _startBase = new Dictionary<Runner, BaseId>();
    private bool _isHomerun = false;
    private int _scoreRunnersCount = 0;

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
        _runningCount = 0;
        _reachedHomeNotified.Clear();
        _startBase.Clear();

        foreach (var runner in _runners)
        {
            if (runner == null) continue;
            runner.SetActive(false);
        }
    }

    // 初期配置
    private void SetupBaseRunners(DefenseSituation situation)
    {
        if (situation == null) return;

        // 各塁にランナーを配置
        if (situation.OnFirstBase)
        {
            var runner = GetRunner(RunnerType.First);
            if (runner != null)
            {
                runner.SetActive(true);
                runner.SetCurrentBase(_baseManager.GetBasePosition(BaseId.First), BaseId.First);
            }
        }

        if (situation.OnSecondBase)
        {
            var runner = GetRunner(RunnerType.Second);
            if (runner != null)
            {
                runner.SetActive(true);
                runner.SetCurrentBase(_baseManager.GetBasePosition(BaseId.Second), BaseId.Second);
            }
        }

        if (situation.OnThirdBase)
        {
            var runner = GetRunner(RunnerType.Third);
            if (runner != null)
            {
                runner.SetActive(true);
                runner.SetCurrentBase(_baseManager.GetBasePosition(BaseId.Third), BaseId.Third);
            }
        }

        // バッター
        var batter = GetRunner(RunnerType.Batter);
        if (batter != null)
        {
            batter.SetActive(true);
            batter.SetCurrentBase(batter.transform.position, BaseId.None);
        }
    }

    public Runner GetRunner(RunnerType type)
    {
        _runnersByType.TryGetValue(type, out var runner);
        return runner;
    }

    public float GetRunnerSpeed(RunnerType type)
    {
        var runner = GetRunner(type);
        return runner?.SecondsPerBase ?? 3.8f;
    }

    /// <summary>
    /// PlayJudgeから呼ばれる走塁実行
    /// </summary>
    public void ExecuteRunningPlan(RunningPlan plan)
    {
        _scoreRunnersCount = 0;
        _isHomerun = plan.IsHomerun;

        if (_isHomerun)
        {
            Debug.Log("[RunnerManager] ホームラン走塁計画");
            _scoreRunnersCount = plan.RunnerActions.Count;
        }

        if (plan.RunnerActions == null || plan.RunnerActions.Count == 0)
        {
            Debug.Log("[RunnerManager] 走塁なし");
            _onAllRunnersStopped?.RaiseEvent(BuildRunningSummary());
            return;
        }

        _reachedHomeNotified.Clear();

        foreach (var action in plan.RunnerActions)
        {
            var runner = GetRunner(action.RunnerType);
            if (runner == null)
            {
                Debug.LogError($"[RunnerManager] Runner not found: {action.RunnerType}");
                continue;
            }

            // 開始位置を記録
            _startBase[runner] = action.StartBase;

            // バッターの場合はAnimationEvent待ち
            if (action.RunnerType == RunnerType.Batter)
            {
                SetupBatterRun(runner, action);
            }
            else
            {
                // 既存ランナーは即実行
                ExecuteRunnerActionAsync(runner, action).Forget();
            }
        }
    }

    /// <summary>
    /// バッターの走塁計画をセット（AnimationEventで実行される）
    /// </summary>
    private void SetupBatterRun(Runner batter, RunnerAction action)
    {
        bool isHomeRun = (action.TargetBase == BaseId.Home && action.StartBase == BaseId.None);

        _runningCount++;

        batter.SetPlannedRun(
            action.TargetBase,
            action.StartDelay,
            isHomeRun,
            onCompleted: () =>
            {
                OnRunnerCompleted(batter, action.TargetBase);
            });

        Debug.Log($"[RunnerManager] バッター走塁計画セット: → {action.TargetBase}" +
                  $"{(isHomeRun ? " (ホームラン)" : "")} (AnimationEvent待ち)");
    }

    /// <summary>
    /// 既存ランナーの走塁実行
    /// </summary>
    private async UniTaskVoid ExecuteRunnerActionAsync(Runner runner, RunnerAction action)
    {
        _runningCount++;

        try
        {
            Debug.Log($"[RunnerManager] {runner.Type} 走塁開始: {action.StartBase} → {action.TargetBase}");

            await runner.RunToBaseSequentiallyAsync(
                action.StartBase,
                action.TargetBase,
                destroyCancellationToken,
                action.StartDelay);

            OnRunnerCompleted(runner, action.TargetBase);
        }
        catch (System.OperationCanceledException)
        {
            Debug.Log($"[RunnerManager] {runner.Type} 走塁キャンセル");
            _runningCount--;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[RunnerManager] {runner.Type} 走塁エラー: {ex.Message}");
            _runningCount--;
        }
    }

    private void OnRunnerCompleted(Runner runner, BaseId targetBase)
    {
        _runningCount--;
        if (_runningCount < 0) _runningCount = 0;

        Debug.Log($"[RunnerManager] {runner.Type} 走塁完了 → {runner.CurrentBase}");

        // ホーム到達通知
        NotifyReachedHomeIfNeeded(runner);

        // 全員完了チェック
        if (_runningCount == 0)
        {
            Debug.Log("[RunnerManager] 全ランナー走塁完了");
            _onAllRunnersStopped?.RaiseEvent(BuildRunningSummary());
            // ホームラン時の特別処理
            if (_isHomerun)
            {
                _onHomeRunRunnerCompleted?.RaiseEvent(_scoreRunnersCount);
                Debug.Log("[RunnerManager] ホームランランナー走塁完了通知");
            }
        }
    }

    private void NotifyReachedHomeIfNeeded(Runner runner)
    {
        if (runner == null) return;

        RunnerType type = runner.Type;

        if (_reachedHomeNotified.Contains(type)) return;
        if (runner.CurrentBase != BaseId.Home) return;

        _reachedHomeNotified.Add(type);
        _onRunnerReachedHomeThisPlay?.RaiseEvent(type);

        Debug.Log($"[RunnerManager] {type} がホームに到達");
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
            bool reachedHome = (endBase == BaseId.Home);

            list.Add(new RunnerFinalState
            {
                RunnerType = runner.Type,
                StartBase = startBase,
                EndBase = endBase,
                AdvancedBases = advanced,
                ReachedHomeThisPlay = reachedHome,
                IsOutThisPlay = false // TODO: アウト判定の実装
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