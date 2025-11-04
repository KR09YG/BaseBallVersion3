using UnityEngine;

public class PitchManager : MonoBehaviour
{
    [SerializeField] Animator _anim;
    Vector3 _pitchPos;
    private PitchBallData _pitchBall;
    [SerializeField] private PitcherAI _pitcherAI;
    public event System.Action OnRelease;

    private void Start()
    {
        _pitchPos = this.transform.position;
    }
    public void StartPitch(PitchBallData pitchBall)
    {
        Debug.Log("アニメーションスタート");        
        _anim.SetTrigger("isPitch");
        _pitchBall = pitchBall;
    }

    public void RePlayPitch(string animName)
    {
        Time.timeScale = 0.5f;
        Debug.Log("リプレイピッチ: " + animName);
        _anim.Play(animName);        
    }

    public void RePlayRelease()
    {
        Debug.Log("リプレイリリース");
    }


    public void Release()
    {
        Debug.Log("release");
        OnRelease?.Invoke();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.S))
        {
            this.transform.position = _pitchPos;
            _pitcherAI.PitchStart().Forget();
        }
    }
}
