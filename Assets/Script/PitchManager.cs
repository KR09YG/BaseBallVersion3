using UnityEngine;

public class PitchManager : MonoBehaviour
{
    [SerializeField] Animator _anim;
    Vector3 _pitchPos;


    private void Start()
    {
        ServiceLocator.Register(this);
        _pitchPos = this.transform.position;
    }
    public void StartPitch()
    {
        Debug.Log("�A�j���[�V�����X�^�[�g");        
        _anim.SetTrigger("isPitch");
    }

    public void RePlayPitch(string animName)
    {
        Time.timeScale = 0.5f;
        Debug.Log("���v���C�s�b�`: " + animName);
        _anim.Play(animName);        
    }

    public void RePlayRelease()
    {
        Debug.Log("���v���C�����[�X");
        ServiceLocator.Get<RePlayManager>().RePlayRelease();
    }


    public void Release()
    {
        Debug.Log("release");
        ServiceLocator.Get<BallControl>().enabled = true;
        ServiceLocator.Get<BallControl>().Pitching();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.S))
        {
            StartPitch();
            this.transform.position = _pitchPos;
        }
    }
}
