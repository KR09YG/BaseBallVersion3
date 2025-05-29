using UnityEngine;

public class PitchManager : MonoBehaviour
{
    [SerializeField] Animator _anim;
    [SerializeField] BallControl _ball;
    Vector3 _pitchPos;

    private void Start()
    {
        _pitchPos = this.transform.position;
    }
    public void StartPitch()
    {
        Debug.Log("アニメーションスタート");        
        _anim.SetTrigger("isPitch");
    }

    public void Release()
    {
        Debug.Log("release");
        _ball.enabled = true;
        _ball.Pitching();
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
