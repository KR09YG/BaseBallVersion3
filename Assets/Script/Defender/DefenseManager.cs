using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// 守備側の動きを管理
/// PlayJudgeから計画を受け取って、野手を動かして送球を実行
/// </summary>
public class DefenseManager : MonoBehaviour, IInitializable
{
    [SerializeField] private List<FielderController> _fielders;
    [SerializeField] private BaseManager _baseManager;
    private FielderThrowBallMove _ball;

    [Header("Settings")]
    [SerializeField] private float _catchWaitTimeout = 5f;  // 捕球待機タイムアウト
    [SerializeField] private float _throwPrepareTime = 0.3f; // 送球準備時間（体の向き変更）
    [SerializeField] private RuntimeAnimatorController _fielderController;
    [SerializeField] private RuntimeAnimatorController _pitcherController;
    [SerializeField] private RuntimeAnimatorController _catcherController;

    [Header("Events")]
    [SerializeField] private OnDefenseCompletedEvent _onDefenseCompleted;
    [SerializeField] private OnBallSpawnedEvent _onBallSpawned;
    [SerializeField] private OnDefenderCatchEvent _onDefenderCatch;

    private Dictionary<PositionType, FielderController> _byPosition;
    private Dictionary<FielderController, (Vector3 position, FielderAnimationController controller)> _initialPositions =
        new Dictionary<FielderController, (Vector3, FielderAnimationController)>();
    private DefenseSituation _situation;
    private CancellationTokenSource _defenseCts;

    private bool _hasCatchedBall = false;

    public List<FielderController> GetAllFielders() => _fielders;

    private void Awake()
    {
        if (_onBallSpawned != null) _onBallSpawned.RegisterListener(OnBallSpawned);
        else Debug.LogError("OnBallSpawnedEvent が未設定");

        if (_onDefenderCatch != null) _onDefenderCatch.RegisterListener(OnBallCatchedFielder);
        else Debug.LogError("OnDefenderCatchEvent が未設定");

        foreach (var fielder in _fielders)
        {
            if (_initialPositions.ContainsKey(fielder)) continue;
            _initialPositions.Add(
                fielder, (fielder.transform.position, fielder.GetComponent<FielderAnimationController>()));
        }

        _byPosition = new Dictionary<PositionType, FielderController>();

        foreach (var fielder in _fielders)
        {
            if (fielder?.Data != null) _byPosition[fielder.Data.Position] = fielder;
        }

    }

    private void OnDestroy()
    {
        _onBallSpawned?.UnregisterListener(OnBallSpawned);
        _onDefenderCatch?.UnregisterListener(OnBallCatchedFielder);
        _defenseCts?.Cancel();
        _defenseCts?.Dispose();
    }

    public void OnInitialized(DefenseSituation situation)
    {
        _defenseCts?.Cancel();
        _defenseCts?.Dispose();
        _defenseCts = null;
        _situation = situation;
        _hasCatchedBall = false;
        // 野手を初期する
        foreach (var fielder in _fielders)
        {
            if (_initialPositions.TryGetValue(fielder, out var initData))
            {
                {
                    fielder.transform.position = initData.position;
                    if (fielder.Data.Position == PositionType.Pitcher)
                    {
                        fielder.GetComponent<Animator>().runtimeAnimatorController = _pitcherController;
                    }
                    else
                    {
                        if (fielder.Data.Position == PositionType.Catcher)
                        {
                            fielder.GetComponent<Animator>().runtimeAnimatorController = _catcherController;
                        }
                        else
                        {
                            initData.controller.PlayAnimation(FielderState.Waiting);
                        }
                        fielder.transform.LookAt(_baseManager.GetBasePosition(BaseId.Home));
                    }
                }
            }
        }
    }

    private void OnBallSpawned(GameObject ball)
    {
        _ball = ball.GetComponent<FielderThrowBallMove>();
    }

    private void OnBallCatchedFielder(FielderController fielder)
    {
        _hasCatchedBall = true;
    }

    /// <summary>
    /// PlayJudgeから呼ばれる守備実行
    /// </summary>
    public void ExecuteDefensePlan(DefensePlan plan)
    {
        _byPosition[PositionType.Catcher].GetComponent<Animator>().runtimeAnimatorController = _fielderController;
        _byPosition[PositionType.Pitcher].GetComponent<Animator>().runtimeAnimatorController = _fielderController;
        if (plan.CatchPlan.Catcher == null)
        {
            Debug.LogError("[DefenseManager] CatchPlan.Catcher is null!");
            return;
        }

        Debug.Log($"[DefenseManager] 捕球者: {plan.CatchPlan.Catcher.Data.Position}");
        Debug.Log($"[DefenseManager] 捕球地点: {plan.CatchPlan.CatchPoint}");
        Debug.Log($"[DefenseManager] 捕球時刻: {plan.CatchPlan.CatchTime}秒");
        Debug.Log($"[DefenseManager] ベースカバー数: {plan.BaseCovers.Count}");
        Debug.Log($"[DefenseManager] 送球シーケンス数: {plan.ThrowSequence.Count}");

        // 既存の守備をキャンセル
        _defenseCts?.Cancel();
        _defenseCts?.Dispose();
        _defenseCts = new CancellationTokenSource();

        // 非同期で守備を実行
        ExecuteDefenseAsync(plan, _defenseCts.Token).Forget();
    }

    private async UniTaskVoid ExecuteDefenseAsync(DefensePlan plan, CancellationToken ct)
    {
        try
        {
            // 1. 捕球者を捕球地点へ移動
            Debug.Log($"[DefenseManager] 捕球者 {plan.CatchPlan.Catcher.Data.Position} を移動開始");

            plan.CatchPlan.Catcher.MoveToCatchPointAsync(
                plan.CatchPlan.CatchPoint,
                plan.CatchPlan.CatchTime,
                _ball.gameObject,
                ct).Forget();

            // 2. ベースカバーを配置（並列実行）
            foreach (var cover in plan.BaseCovers)
            {
                if (cover.Fielder == null) continue;
                Vector3 v = _baseManager.GetBasePosition(cover.BaseId);
                Debug.Log($"[DefenseManager] ベースカバー　{(BaseId)cover.BaseId} {cover.Fielder.Data.Position}が{v}に移動開始");
                cover.Fielder.MoveToBaseAsync(
                    _baseManager.GetBasePosition(cover.BaseId),
                    _ball.gameObject,
                    cover.ArriveTime,
                    ct).Forget();
            }


            await UniTask.WaitUntil(() => _hasCatchedBall);

            Debug.Log($"[DefenseManager] ボール捕球成功");

            // 4. 送球シーケンスを順次実行
            for (int i = 0; i < plan.ThrowSequence.Count; i++)
            {
                var step = plan.ThrowSequence[i];

                if (step.ThrowerFielder == null)
                {
                    Debug.LogError("[DefenseManager] ThrowerFielder is null!");
                    break;
                }

                Debug.Log($"[DefenseManager] 送球 {i + 1}/{plan.ThrowSequence.Count}: " +
                    $"{step.ThrowerFielder.Data.Position} → {step.Plan}");

                await ExecuteThrowStepAsync(step, ct);
            }

            Debug.Log("[DefenseManager] 守備完了");

            // 守備完了を通知
            _onDefenseCompleted?.RaiseEvent();
        }
        catch (OperationCanceledException)
        {
            Debug.Log("[DefenseManager] 守備キャンセル");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DefenseManager] エラー: {ex.Message}\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// 1回の送球ステップを実行
    /// </summary>
    private async UniTask ExecuteThrowStepAsync(ThrowStep step, CancellationToken ct)
    {
        // 1. 送球準備（向きを変える）
        await step.ThrowerFielder.ThrowBallAsync(
            step.TargetPosition,
            _ball.gameObject,
            ct);

        // 2. 受け手が捕球（簡易実装：到着時点で自動捕球）
        if (step.ReceiverFielder != null)
        {
            Debug.Log($"[DefenseManager] {step.ReceiverFielder.Data.Position} が受球");

            if (_ball != null)
            {
                _ball.transform.position = step.ReceiverFielder.transform.position + Vector3.up;
            }
        }
    }
}