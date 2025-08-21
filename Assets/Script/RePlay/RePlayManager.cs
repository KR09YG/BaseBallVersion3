using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class RePlayManager : MonoBehaviour
{
    private float _clickTiming;

    public UnityEvent RePlayFin;

    private void Start()
    {
        RePlayFin.AddListener(() => Time.timeScale = 1f);
    }

    public void ClickTimeSet(float t)
    {
        _clickTiming = t;
    }

    public void RePlayRelease()
    {
        Debug.Log("Release");
        ServiceLocator.BallControlInstance.RePlayPitching();
        StartCoroutine(SwingStart());
    }

    IEnumerator SwingStart()
    {
        yield return new WaitForSeconds(_clickTiming);
        ServiceLocator.BatterAnimationControlInstance.StartAnim("HomerunReplay");
    }

    public void RePlayHomerunBall()
    {
        StartCoroutine(ServiceLocator.BatterAnimationControlInstance.PositionReset());
        ServiceLocator.BatterAnimationControlInstance.AimIKReCall();
        ServiceLocator.BattingInputManagerInstance.RePlayHomeRunBall();
    }
}
