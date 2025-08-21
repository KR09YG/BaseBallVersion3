using UnityEngine;

public class PitchManager : MonoBehaviour
{
    [SerializeField] Animator _anim;
    Vector3 _pitchPos;

    private void Start()
    {
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
        ServiceLocator.RePlayManagerInstance.RePlayRelease();
    }


    public void Release()
    {
        Debug.Log("release");
        ServiceLocator.BallControlInstance.enabled = true;
        ServiceLocator.BallControlInstance.Pitching();
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
