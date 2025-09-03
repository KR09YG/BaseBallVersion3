using System;
using UnityEngine;

public class BallCountManager : MonoBehaviour
{
    private BallCountDisplay _ballCountDisplay;
    public int BallCount { get; private set; }
    public int StrikeCount { get; private set; }
    public int OutCount { get; private set; }

    public Action BallEvent;
    public Action StrikeEvent;
    public Action OutEvent;
    public Action FoulEvent;

    private void Awake()
    {
        ServiceLocator.Register(this);
    }

    private void Start()
    {
        _ballCountDisplay = ServiceLocator.Get<BallCountDisplay>();
        StrikeEvent += StrikeCalled;
        OutEvent += OutCalled;
        BallEvent += BallCalled;
        FoulEvent += FoulCalled;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log($"ボールカウント: {BallCount}, ストライクカウント: {StrikeCount}, アウトカウント: {OutCount}");
        }
    }

    public void ResetCounts()
    {
        BallCount = 0;
        StrikeCount = 0;
    }

    private void FoulCalled()
    {
        if (StrikeCount == 2)
        {
            // ファールの演出などをここに追加
        }
        else
        {
            StrikeCount++;
        }

        _ballCountDisplay.BallCountDisplayUpdate();
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
            BallCount = 0;
            OutCalled();
        }
        
        _ballCountDisplay.BallCountDisplayUpdate();
    }

    private void BallCalled()
    {
        if (BallCount < 3)
        {
            BallCount++;
        }
        else
        {
            BallCount = 0;
            StrikeCount = 0;
        }

        _ballCountDisplay.BallCountDisplayUpdate();
    }

    public void OutCalled()
    {
        if (OutCount < 2)
        {
            OutCount++;
        }
        else
        {
            OutCount = 0;
            // ここにチェンジの処理及び演出
        }

        _ballCountDisplay.BallCountDisplayUpdate();
    }

}
