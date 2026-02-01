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

public class RunnerManager : MonoBehaviour, IRunnerRunStartListener, IInitializable
{
    [SerializeField] private BaseManager _baseManager;
    [SerializeField] private OnBattingResultEvent _resultEvent;
    [SerializeField] private OnAtBatResetEvent _atBatResetEvent;

    [Header("Runner refs (optional)")]
    [SerializeField] private Runner _batter;
    [SerializeField] private Runner _firstRunner;
    [SerializeField] private Runner _secondRunner;
    [SerializeField] private Runner _thirdRunner;

    private readonly Dictionary<RunnerType, Runner> _runners = new();
    private readonly List<Runner> _activeBaseRunners = new(); // 塁上走者のみ

    // 打席ごとにリセットする
    private bool _batterStartedThisPlay;

    private DefenseSituation _currentSituation;

    private void Awake()
    {
        if (_resultEvent != null) _resultEvent.RegisterListener(OnBattingResult);
        else Debug.LogError("[RunnerManager] BattingResultEventが設定されていません！");

        if (_atBatResetEvent != null)
        {
            _atBatResetEvent.RegisterListener(OnAtBatReset);
        }
        else
        {
            Debug.LogError("[RunnerManager] AtBatResetEventが設定されていません！");
        }

        EnsureRunnerExists(RunnerType.Batter, _batter, "Runner_Batter");
        EnsureRunnerExists(RunnerType.First, _firstRunner, "Runner_First");
        EnsureRunnerExists(RunnerType.Second, _secondRunner, "Runner_Second");
        EnsureRunnerExists(RunnerType.Third, _thirdRunner, "Runner_Third");
    }

    private void OnDestroy()
    {
        _resultEvent?.UnregisterListener(OnBattingResult);
        _atBatResetEvent?.UnregisterListener(OnAtBatReset);
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
        if (situation == null)
        {
            Debug.LogError("Cannot initialize runners: DefenseSituation is null.");
            return;
        }

        _currentSituation = situation;
        _batterStartedThisPlay = false;

        _activeBaseRunners.Clear();

        // 塁上走者のみ有効化（バッターはここでは塁上にいない想定）
        SetBaseRunnerActive(RunnerType.First, situation.OnFirstBase, BaseId.First);
        SetBaseRunnerActive(RunnerType.Second, situation.OnSecondBase, BaseId.Second);
        SetBaseRunnerActive(RunnerType.Third, situation.OnThirdBase, BaseId.Third);

        // バッターはホーム付近に待機させたいならここで
        // var batterRunner = _runners[RunnerType.Batter];
        // batterRunner.SetCurrentBase(_baseManager.GetBasePosition(BaseId.Home), BaseId.Home);
        // batterRunner.SetActive(true);
    }

    private void OnAtBatReset()
    {
        OnInitialized(_currentSituation);
    }

    private void SetBaseRunnerActive(RunnerType type, bool active, BaseId baseId)
    {
        if (!_runners.TryGetValue(type, out var runner) || runner == null)
        {
            Debug.LogError($"Runner not found: {type}");
            return;
        }

        runner.SetActive(active);

        if (active)
        {
            runner.SetCurrentBase(_baseManager.GetBasePosition(baseId), baseId);
            _activeBaseRunners.Add(runner);
        }
    }

    private void OnBattingResult(BattingBallResult result)
    {
        // ファール / ミスはインプレー扱いにしない
        if (result.IsFoul) return;
        if (result.BallType == BattingBallType.Miss) return;

        // 1) 事前判定しない方針：インプレーになったらバッターだけは必ず走る（フライでも走る）
        StartBatterRunToFirstIfNeeded();

        // 2) 塁上走者は「ヒット確定」のときだけ進塁
        if (result.IsHit)
        {
            foreach (var runner in _activeBaseRunners)
            {
                var nextBase = (BaseId)runner.CurrentBase + 1;
                if (nextBase > BaseId.Home) continue;

                Vector3 nextBasePos = _baseManager.GetBasePosition(nextBase);
                runner.StartRunToNextBase(nextBasePos, nextBase, null);
            }
        }
    }

    private void StartBatterRunToFirstIfNeeded()
    {
        if (_batterStartedThisPlay) return;

        if (!_runners.TryGetValue(RunnerType.Batter, out var batter) || batter == null)
        {
            Debug.LogError("[RunnerManager] Batter runner not found.");
            return;
        }

        _batterStartedThisPlay = true;

        batter.SetActive(true);
        _activeBaseRunners.Add(batter);

        Vector3 firstPos = _baseManager.GetBasePosition(BaseId.First);
        batter.StartRunToNextBase(firstPos, BaseId.First, () =>
        {
            Debug.Log("[RunnerManager] Batter reached first base.");
        });
    }

    // もし別イベントから「バッター走り開始」を通知する場合は、ここはバッター専用にする
    public void OnRunnerStartRunning(Runner runner)
    {
        // 以前のコードは “通知が来たら強制で一塁へ” だったので、
        // ここはバッター専用の入口として扱うのが安全
        if (runner == _runners[RunnerType.Batter])
        {
            StartBatterRunToFirstIfNeeded();
        }
    }

    /// <summary>
    /// 全走者のETAを到達時間が近い順に取得する
    /// </summary>
    public int GetAllRunningETAs(List<RunnerETA> buffer, bool sortByRemaining = true)
    {
        if (buffer == null) return 0;

        buffer.Clear();

        // _activeRunners に「現在有効な走者」が入っている前提
        for (int i = 0; i < _activeBaseRunners.Count; i++)
        {
            var r = _activeBaseRunners[i];
            if (r == null) continue;
            if (!r.IsRunning) continue;

            buffer.Add(new RunnerETA(r, r.TargetBase, r.RemainingTimeToTarget));
        }

        if (sortByRemaining)
            buffer.Sort((a, b) => a.Remaining.CompareTo(b.Remaining));

        return buffer.Count;
    }

    public Runner GetRunner(RunnerType type) => _runners[type];
}
