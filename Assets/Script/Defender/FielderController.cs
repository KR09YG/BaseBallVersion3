using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

/// <summary>
/// 野手の制御（キャッチ・送球対応版）
/// </summary>
public class FielderController : MonoBehaviour
{
    [SerializeField] private FielderAnimationController _animationController;
    [SerializeField] private FielderData _data;
    [SerializeField] private OnDefenderCatchEvent _defenderCatchEvent;
    [SerializeField] private float _catchTime = 1f;
    public FielderData Data => _data;


    public FielderState State { get; private set; } = FielderState.Waiting;

    [Header("Catch Settings")]
    [SerializeField] private float _catchRadius = 1.5f;  // ボールを捕球できる範囲

    private bool _isWaitingForCatch;
    public bool _isWaitingForThrow;

    private void SetState(FielderState state)
    {
        State = state;
        _animationController?.PlayAnimation(state);
    }

    private const float YAW_OFFSET_CATCH = 90f; 
    /// <summary>
    /// 捕球地点まで移動して捕球待機
    /// </summary>
    public async UniTask MoveToCatchPointAsync(Vector3 catchPoint, float arrivalTime, GameObject ball, CancellationToken ct)
    {
        SetState(FielderState.MovingTo);

        Vector3 startPos = transform.position;
        catchPoint.y = startPos.y;
        transform.LookAt(catchPoint);

        float elapsed = 0f;
        bool isCatchAnimationStarted = false;

        while (elapsed < arrivalTime)
        {
            ct.ThrowIfCancellationRequested();

            float t = elapsed / arrivalTime;
            Vector3 newPos = Vector3.Lerp(startPos, catchPoint, t);
            Vector3 moveDir = catchPoint - transform.position;
            moveDir.y = 0f;
            if (moveDir.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(moveDir);
            }

            // 捕球のアニメーションを事前に開始
            if (elapsed >= arrivalTime - _catchTime && !isCatchAnimationStarted)
            {
                isCatchAnimationStarted = true;
                Vector3 ballPos = ball.transform.position;
                Vector3 dir = ballPos - transform.position;
                dir.y = 0f;
                float yawOffset = YAW_OFFSET_CATCH;
                Quaternion targetRot = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, yawOffset, 0f);
                transform.rotation = targetRot;
                SetState(FielderState.CatchingBall);
            }

            newPos.y = startPos.y;
            transform.position = newPos;

            elapsed += Time.deltaTime;
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }

        _defenderCatchEvent?.RaiseEvent(this);
        // 最終位置にセット
        transform.position = catchPoint;
    }

    /// <summary>
    /// ベース位置まで移動
    /// </summary>
    public async UniTask MoveToBaseAsync(Vector3 basePosition,GameObject ball, float arrivalTime, CancellationToken ct)
    {
        SetState(FielderState.MovingTo);

        Vector3 startPos = transform.position;
        basePosition.y = startPos.y;  // 高さを揃える

        float elapsed = 0f;

        while (elapsed < arrivalTime)
        {
            ct.ThrowIfCancellationRequested();

            float t = elapsed / arrivalTime;
            Vector3 newPos = Vector3.Lerp(startPos, basePosition, t);
            newPos.y = startPos.y;

            // 先に位置を更新
            transform.position = newPos;

            // 移動方向を向く（更新後の位置から計算）
            Vector3 moveDir = basePosition - transform.position;
            moveDir.y = 0f;

            if (moveDir.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(moveDir);
            }
            elapsed += Time.deltaTime;
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }

        // 最終位置に確実にセット
        transform.position = basePosition;
        // 到着したらベース方向を向く（または待機状態）
        SetState(FielderState.Waiting);
        Vector3 ballPos = ball.transform.position;
        ballPos.y = transform.position.y;
        transform.LookAt(ballPos);
    }

    private const float YAW_OFFSET_THROW = 180f;
    /// <summary>
    /// 送球準備（向きを変える）
    /// </summary>
    public async UniTask ThrowBallAsync(Vector3 targetPosition, GameObject ball, CancellationToken ct)
    {
        _isWaitingForThrow = true;
        Vector3 ballPos = ball.transform.position;
        Vector3 dir = ballPos - transform.position;
        dir.y = 0f;
        float yawOffset = YAW_OFFSET_THROW;
        Quaternion targetRot = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, yawOffset, 0f);
        transform.rotation = targetRot;
        SetState(FielderState.ThrowingBall);
        await UniTask.WaitUntil(() => !_isWaitingForThrow);
        await ball.GetComponent<FielderThrowBallMove>().
            ThrowToAsync(this.transform.position, targetPosition, Data.ThrowSpeed, ct);
    }

    public void ThrowBall()
    {
        _isWaitingForThrow = false;
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