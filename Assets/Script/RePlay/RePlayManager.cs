using Cinemachine;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class RePlayManager : MonoBehaviour
{
    private float _clickTiming;

    public UnityEvent RePlayFin;

    [SerializeField] private CinemachineVirtualCamera _batterCamera;
    [SerializeField] private CinemachineVirtualCamera _ballLookCamera;

    public CinemachineVirtualCamera BatterCamera => _batterCamera;
    public CinemachineVirtualCamera BallLookCamera => _ballLookCamera;

    /// <summary>
    /// ���v���C�̗D��x����ԍ���
    /// </summary>
    private const int PlayPriority = 15;
    /// <summary>
    /// �ʏ�̗D��x
    /// </summary>
    private const int NormalPriority = 10;

    private void Start()
    {
        ServiceLocator.Register(this);
        RePlayFin.AddListener(() =>
        {
            Time.timeScale = 1f;
            ResetPriority(_ballLookCamera);
            ServiceLocator.Get<BallControl>().SetBallState(false);
        });
    }

    public void ClickTimeSet(float t)
    {
        _clickTiming = t;
    }

    public void RePlayRelease()
    {
        Debug.Log("Release");
        ServiceLocator.Get<BallControl>().RePlayPitching();
        StartCoroutine(SwingStart());
    }

    IEnumerator SwingStart()
    {
        yield return new WaitForSeconds(_clickTiming);
        ServiceLocator.Get<BattingBallMove>().StopMovie();
        SetPriority(_batterCamera);
        ServiceLocator.Get<BatterAnimationControl>().StartAnim("HomerunReplay");
    }

    public void SetPriority(CinemachineVirtualCamera camera)
    {
        camera.Priority = PlayPriority;
    }

    public void ResetPriority(CinemachineVirtualCamera camera)
    {
        camera.Priority = NormalPriority;
    }

    public void RePlayHomerunBall()
    {
        StartCoroutine(ServiceLocator.Get<BatterAnimationControl>().PositionReset());
        ServiceLocator.Get<BatterAnimationControl>().AimIKReCall();
        ServiceLocator.Get<BattingInputManager>()._startReplay?.Invoke();
    }
}
