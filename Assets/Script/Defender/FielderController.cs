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
    private bool _isThrowing = false;

    public void MoveTo(Vector3 targetPos, float timeLimit)
    {
        Debug.Log($"{_data.Position} MoveTo {targetPos} in {timeLimit} sec");
        transform.DOMove(new Vector3(targetPos.x, transform.position.y, targetPos.z), timeLimit).
            SetEase(Ease.Linear);
    }

    public void MoveToCutoff()
    {
        // 中継位置へ移動
    }

    public void CatchBall()
    {

    }

    public async UniTask ExecuteThrowStepAsync(ThrowStep step, FielderThrowBallMove ball, CancellationToken ct)
    {
        // 受け手をカバーに動かす（待つ/待たないは好み）
        // Day4は待たないでもOK
        step.ReceiverFielder.MoveTo(step.TargetPosition, 0.6f);

        // 投げる（ボール到達まで待つ）
        Vector3 from = transform.position;         // 投げ手は自分
        Vector3 to = step.TargetPosition;

        await ball.ThrowToAsync(from, to, step.ThrowSpeed, ct);

        // 必要なら「投げ終わり」イベントやアニメ完了待ちをここに足す
    }

    public void ThrowBall(BaseType baseType)
    {
        // 各塁へ投げる
        Debug.Log($"{_data.Position} ThrowBall to {baseType}");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_isThrowing) return;

        if (other.CompareTag("Ball"))
        {
            Debug.Log($"{_data.Position} caught the ball!");
            // ボールをキャッチしたときに地面に触れたかどうかを判定
            bool isFly = false;
            if (other.TryGetComponent<BattingBallMove>(out var ballMove))
            {
                isFly = !ballMove.HasLanded;
            }
            else
            {
                Debug.LogWarning("Caught object is not a BattingBallMove.");
            }

            _isThrowing = true;
            _defenderCatchEvent.RaiseEvent(this, isFly);
        }
    }
}
