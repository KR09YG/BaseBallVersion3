using UnityEngine;
using UnityEngine.UI;

public class BallCountDisplay : MonoBehaviour
{
    private BallCountManager _ballCountManager;
    [SerializeField, Header("�X�g���C�N�J�E���g�̃C���[�W")] Image[] _strikeImages;
    [SerializeField, Header("�{�[���J�E���g�̃C���[�W")] Image[] _ballImages;
    [SerializeField, Header("�A�E�g�J�E���g�̃C���[�W")] Image[] _outImages;

    private void Awake()
    {
        ServiceLocator.Register(this);
    }

    private void Start()
    {
        _ballCountManager = ServiceLocator.Get<BallCountManager>();
    }

    public void BallCountDisplayUpdate()
    {
        for (int i = 0; i < _strikeImages.Length; i++)
        {
            if (i + 1 <= _ballCountManager.StrikeCount)            
                _strikeImages[i].color = Color.yellow;
            else _strikeImages[i].color = Color.white;
        }

        for (int i = 0; i < _ballCountManager.BallCount; i++)
        {
            if (i + 1 <= _ballCountManager.BallCount)
                _ballImages[i].color = Color.green;
            else
                _ballImages[i].color = Color.white;
        }

        for (int i = 0; i < _ballCountManager.OutCount; i++)
        {
            if (i + 1 <= _ballCountManager.OutCount)
                _outImages[i].color = Color.red;
            else
                _outImages[i].color = Color.white;
        }
    }
}
