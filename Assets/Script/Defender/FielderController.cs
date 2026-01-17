using UnityEngine;
using DG.Tweening;
using TMPro.SpriteAssetUtilities;

public class FielderController : MonoBehaviour
{
    [SerializeField] private FielderData _data;
    [SerializeField] private DefenderCatchEvent _defenderCatchEvent;
    public FielderData Data => _data;

    public void MoveTo(Vector3 targetPos, float timeLimit)
    {
        Debug.Log($"{_data.Position} MoveTo {targetPos} in {timeLimit} sec");
        transform.DOMove(new Vector3(targetPos.x, transform.position.y, targetPos.z), timeLimit).
            SetEase(Ease.Linear);
    }

    public void MoveToCutoff()
    {
        // ’†ŒpˆÊ’u‚ÖˆÚ“®
    }

    public void CatchBall()
    {

    }

    public void ThrowBall(Vector3 target)
    {

    }

    public void ThrowBall(BaseType baseType)
    {
        // Še—Û‚Ö“Š‚°‚é
        Debug.Log($"{_data.Position} ThrowBall to {baseType}");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Ball"))
        {
            Debug.Log($"{_data.Position} caught the ball!");
            _defenderCatchEvent.RaiseEvent(this, other);
        }
    }
}
