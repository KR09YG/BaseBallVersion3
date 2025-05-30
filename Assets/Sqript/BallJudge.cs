using UnityEngine;

public class BallJudge : MonoBehaviour
{
    [SerializeField] private GameObject _ball;
    public bool IsStrike{get; private set;}
    [SerializeField] private BallCountManager _bcm;
    private bool _isPitching;

    
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject == _ball)
        {
            IsStrike = true;
        }
    }

    private void Update()
    {
        if (_isPitching)
        {
            if (Input.GetMouseButtonDown(0))
            {
                IsStrike = true;
            }
        }
    }

    /// <summary>
    /// 現在投球中かどうかを判定する
    /// </summary>
    public void IsPitching()
    {
        _isPitching = !_isPitching;
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
