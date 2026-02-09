using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

/// <summary>
/// 野手の制御（キャッチ・送球対応版）
/// </summary>
public class FielderController : MonoBehaviour
{
    [SerializeField] private FielderData _data;
    [SerializeField] private OnDefenderCatchEvent _defenderCatchEvent;
    public FielderData Data => _data;

    public FielderState State { get; private set; } = FielderState.Waiting;

    [Header("Catch Settings")]
    [SerializeField] private float _catchRadius = 1.5f;  // ボールを捕球できる範囲

    private bool _isWaitingForCatch;
    private Action _onCatchCompleted;

    /// <summary>
    /// 捕球地点まで移動して捕球待機
    /// </summary>
    public async UniTask MoveToCatchPointAsync(Vector3 catchPoint, float arrivalTime, CancellationToken ct)
    {
        State = FielderState.MovingTo;

        Vector3 startPos = transform.position;
        catchPoint.y = startPos.y;

        float elapsed = 0f;

        while (elapsed < arrivalTime)
        {
            ct.ThrowIfCancellationRequested();

            float t = elapsed / arrivalTime;
            Vector3 newPos = Vector3.Lerp(startPos, catchPoint, t);
            newPos.y = startPos.y;
            transform.position = newPos;

            // 目標方向を向く
            Vector3 direction = catchPoint - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }

            elapsed += Time.deltaTime;
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }

        // 最終位置にセット
        transform.position = catchPoint;
        State = FielderState.ReadyToCatch;


    }

    /// <summary>
    /// ベース位置まで移動
    /// </summary>
    public async UniTask MoveToBaseAsync(Vector3 basePosition, float arrivalTime, CancellationToken ct)
    {
        State = FielderState.MovingTo;

        Vector3 startPos = transform.position;
        basePosition.y = startPos.y;

        float elapsed = 0f;

        while (elapsed < arrivalTime)
        {
            ct.ThrowIfCancellationRequested();

            float t = elapsed / arrivalTime;
            Vector3 newPos = Vector3.Lerp(startPos, basePosition, t);
            newPos.y = startPos.y;
            transform.position = newPos;

            // 目標方向を向く
            Vector3 direction = basePosition - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }

            elapsed += Time.deltaTime;
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }

        transform.position = basePosition;
        State = FielderState.Waiting;

        _defenderCatchEvent?.RaiseEvent(this);
    }

    /// <summary>
    /// ボール捕球待機開始（OnTriggerEnter用のフラグ設定）
    /// </summary>
    public void StartWaitingForCatch(Action onCatchCompleted)
    {
        _isWaitingForCatch = true;
        _onCatchCompleted = onCatchCompleted;
        State = FielderState.ReadyToCatch;

        Debug.Log($"[{Data.Position}] 捕球待機中");
    }

    /// <summary>
    /// 捕球処理（ボールが範囲内に入ったら呼ばれる）
    /// </summary>
    public bool TryCatchBall(Vector3 ballPosition)
    {
        if (!_isWaitingForCatch) return false;
        if (State != FielderState.ReadyToCatch) return false;

        // 捕球範囲チェック
        float distance = Vector3.Distance(transform.position, ballPosition);

        if (distance <= _catchRadius)
        {
            PerformCatch();
            return true;
        }

        return false;
    }

    /// <summary>
    /// 捕球実行
    /// </summary>
    private void PerformCatch()
    {
        _isWaitingForCatch = false;
        State = FielderState.CatchingBall;

        Debug.Log($"[{Data.Position}] ボール捕球！");

        // 捕球完了コールバック
        _onCatchCompleted?.Invoke();
        _onCatchCompleted = null;
    }

    /// <summary>
    /// 送球準備（向きを変える）
    /// </summary>
    public async UniTask PrepareThrowAsync(Vector3 targetPosition, float prepareTime, CancellationToken ct)
    {
        State = FielderState.PreparingThrow;

        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            float elapsed = 0f;
            Quaternion startRotation = transform.rotation;

            while (elapsed < prepareTime)
            {
                ct.ThrowIfCancellationRequested();

                float t = elapsed / prepareTime;
                transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);

                elapsed += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            transform.rotation = targetRotation;
        }

        State = FielderState.ThrowingBall;
    }

    private void OnDrawGizmosSelected()
    {
        // 捕球範囲を可視化
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, _catchRadius);
    }
}

public enum FielderState
{
    Waiting,
    MovingTo,
    ReadyToCatch,
    CatchingBall,
    PreparingThrow,
    ThrowingBall
}