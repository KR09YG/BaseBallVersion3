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
        Debug.Log("アニメーションスタート");        
        _anim.SetTrigger("isPitch");
    }

    public void Release()
    {
        Debug.Log("release");
        SceneSingleton.BallControlInstance.enabled = true;
        SceneSingleton.BallControlInstance.Pitching();
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
