using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

public class PitcherAI : MonoBehaviour
{
    [SerializeField] private List<PitchBallData> _PitchBalls;
    [SerializeField] private int _pitchDelayTime;
    [SerializeField] private BallControl _ballControl;
    [SerializeField] private PitchManager _pitchManager;

    private void Start()
    {
        PitchStart().Forget();
    }

    public async UniTaskVoid PitchStart()
    {
        await UniTask.Delay(_pitchDelayTime);
        PitchBallData pitchBall = _PitchBalls[Random.Range(0, _PitchBalls.Count)];
        _pitchManager.StartPitch(pitchBall);
    }
}
