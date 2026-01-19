using UnityEngine;
using DG.Tweening;
using TMPro.SpriteAssetUtilities;
using Cysharp.Threading.Tasks;
using System.Threading;

public class FielderController : MonoBehaviour
{
    [SerializeField] private FielderData _data;
    [SerializeField] private DefenderCatchEvent _defenderCatchEvent;
    public FielderData Data => _data;
    private FielderState _currentState = FielderState.Waiting;

    public void MoveToBase(Vector3 targetPos, float timeLimit)
    {
        _currentState = FielderState.MovingTo;
        Debug.Log($"{_data.Position} MoveTo {targetPos} in {timeLimit} sec");
        transform.DOMove(new Vector3(targetPos.x, transform.position.y, targetPos.z), timeLimit).
            SetEase(Ease.Linear);
    }

    public void MoveToCatchPoint(Vector3 targetPos,float timeLimit)
    {
        _currentState = FielderState.ReadyToCatch;
        transform.DOMove(new Vector3(targetPos.x, transform.position.y, targetPos.z), timeLimit).
            SetEase(Ease.Linear).OnComplete(CatchBall);
    }

    private void CatchBall()
    {
        _currentState = FielderState.CatchingBall;
        Collider[] colliders = Physics.OverlapSphere(transform.position, 1.0f);
        foreach (var collider in colliders)
        {
            if (collider.CompareTag("Ball"))
            {
                Debug.Log($"{_data.Position} caught the ball!");
                // ボールをキャッチしたときに地面に触れたかどうかを判定
                bool isFly = false;
                if (collider.TryGetComponent<BattingBallMove>(out var ballMove))
                {
                    isFly = !ballMove.HasLanded;
                }
                else
                {
                    Debug.LogWarning("Caught object is not a BattingBallMove.");
                }

                _defenderCatchEvent.RaiseEvent(this, isFly);
                return;
            }
        }
    }


    public async UniTask ExecuteThrowStepAsync(ThrowStep step, FielderThrowBallMove ball, CancellationToken ct)
    {        
        await UniTask.Delay(_data.ThrowDelay);
        _currentState = FielderState.ThrowingBall;
        // 投げる（ボール到達まで待つ）
        Vector3 from = transform.position;         // 投げ手は自分
        Vector3 to = step.TargetPosition;

        await ball.ThrowToAsync(from, to, step.ThrowSpeed, ct);

        // 必要なら「投げ終わり」イベントやアニメ完了待ちをここに足す
    }
}
