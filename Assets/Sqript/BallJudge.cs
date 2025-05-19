using UnityEngine;

public class BallJudge : MonoBehaviour
{
    [SerializeField] private GameObject _ball;
    public bool IsStrike{get; private set;}
    [SerializeField] private BallCountManager _bcm;

    
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject == _ball)
        {
            IsStrike = true;
        }
    }

    public void StrikeJudge()
    {
        if (IsStrike)
        {
            _bcm.StrikeEvent.Invoke();
            IsStrike = false;
        }
        else
        {
            _bcm.BallEvent.Invoke();
        }
    }
}
