using UnityEngine;

public class BallJudge : MonoBehaviour
{
    [SerializeField] private GameObject _ball;
    [SerializeField] private LayerMask _maetLayer;
    public bool IsStrike{get; private set;}
    private bool _isPitching;
    private Collider[] _colliders;


    private void Start()
    {
        ServiceLocator.Register(this);
    }

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
        ServiceLocator.Get<BallCountManager>().ResetCounts();
    }

    /// <summary>
    /// �t�@�[���{�[���̏������s��
    /// </summary>
    public void FoulBall()
    {
        ServiceLocator.Get<BallCountManager>().FoulEvent?.Invoke();
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
            ServiceLocator.Get<BallCountManager>().StrikeEvent?.Invoke();
            IsStrike = false;
        }
        else
        {
            ServiceLocator.Get<BallCountManager>().BallEvent?.Invoke();
        }
    }
}
