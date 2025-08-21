using UnityEngine;
using UnityEngine.UIElements;

public class BallJudge : MonoBehaviour
{
    [SerializeField] private GameObject _ball;
    [SerializeField] private LayerMask _maetLayer;
    public bool IsStrike{get; private set;}
    private bool _isPitching;
    private Collider[] _colliders;

    
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
            _colliders = Physics.OverlapSphere(transform.position, _ball.transform.localScale.x, _maetLayer);
            if (_colliders.Length > 0)
            {
                IsStrike = true;
            }
        }
    }

    /// <summary>
    /// �ŋ����C���v���C�ɂȂ������̏������s��
    /// </summary>
    public void Hit()
    {
        ServiceLocator.BallCountManagerInstance.ResetCounts();
    }

    /// <summary>
    /// �t�@�[���{�[���̏������s��
    /// </summary>
    public void FoulBall()
    {
        ServiceLocator.BallCountManagerInstance.FoulEvent?.Invoke();
    }

    /// <summary>
    /// ���ݓ��������ǂ����𔻒肷��
    /// </summary>
    public void IsPitching()
    {
        _isPitching = !_isPitching;
    }

    public void SwingStrike()
    {
        IsStrike = true;
    }

    public void StrikeJudge()
    {
        if (IsStrike)
        {
            ServiceLocator.BallCountManagerInstance.StrikeEvent?.Invoke();
            IsStrike = false;
        }
        else
        {
            ServiceLocator.BallCountManagerInstance.BallEvent?.Invoke();
        }
    }
}
