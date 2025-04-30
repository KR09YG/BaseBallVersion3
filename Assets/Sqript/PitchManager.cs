using System;
using System.IO;
using UnityEngine;

public class PitchManager : MonoBehaviour
{
    [SerializeField] Animator _anim;
    [SerializeField] BallControl _ball;
    public void StartPitch()
    {
        _anim.SetTrigger("isPitch");
    }

    public void Release()
    {
        _ball.Pitching();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.S))
        {
            StartPitch();
        }
    }
}
