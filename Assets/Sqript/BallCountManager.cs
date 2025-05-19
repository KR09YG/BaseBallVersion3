using System;
using UnityEngine;

public class BallCountManager : MonoBehaviour
{
    public int BallCount {  get; private set; }
    public int StrikeCount {  get; private set; }
    public int OutCount {  get; private set; }

    public Action BallEvent;
    public Action StrikeEvent;
    public Action OutEvent;

    private void Start()
    {
        StrikeEvent += StrikeCalled;
        OutEvent += OutCalled;
        BallEvent += BallCalled;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            StrikeEvent.Invoke();
            Debug.Log(StrikeCount);
        }
    }
    private void StrikeCalled()
    {
        if (StrikeCount < 3)
        {
            StrikeCount++;
        }
        else
        {
            StrikeCount = 0;
            OutCalled();
        }
    }

    private void BallCalled()
    {
        if (BallCount < 4)
        {
            BallCount++;
        }
    }
    
    public void OutCalled()
    {
        if (OutCount < 3)
        {
            OutCount++;
        }
    }

}
