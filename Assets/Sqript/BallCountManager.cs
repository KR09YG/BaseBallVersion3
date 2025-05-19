using System;
using UnityEngine;

public class BallCountManager : MonoBehaviour
{
    public int BallCount { get; private set; }
    public int StrikeCount { get; private set; }
    public int OutCount { get; private set; }

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
        if (StrikeCount < 2)
        {
            StrikeCount++;
        }
        else
        {
            StrikeCount = 0;
            OutCalled();
        }
        Debug.Log($"Strike {StrikeCount}");
    }

    private void BallCalled()
    {
        if (BallCount < 3)
        {
            BallCount++;
        }
        Debug.Log($"Ball {BallCount}");
    }

    public void OutCalled()
    {
        if (OutCount < 2)
        {
            OutCount++;
        }
        Debug.Log($"Out {OutCount}");
    }

}
