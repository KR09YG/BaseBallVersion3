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
    /// リプレイの優先度が一番高い
    /// </summary>
    private const int PlayPriority = 15;
    /// <summary>
    /// 通常の優先度
    /// </summary>
    private const int NormalPriority = 10;

    private void Start()
    {
        RePlayFin.AddListener(() =>
        {
            Time.timeScale = 1f;
            ResetPriority(_ballLookCamera);
        });
    }

    public void ClickTimeSet(float t)
    {
        _clickTiming = t;
    }

    public void RePlayRelease()
    {
        Debug.Log("Release");
        StartCoroutine(SwingStart());
    }

    IEnumerator SwingStart()
    {
        yield return new WaitForSeconds(_clickTiming);
        SetPriority(_batterCamera);
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

    }
}
