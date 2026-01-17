using UnityEngine;
using System;

[CreateAssetMenu(fileName = "PitchBallReleaseEvent", menuName = "Scriptable Objects/PitchBallReleaseEvent")]
public class PitchBallReleaseEvent : ScriptableObject
{
    private Action<PitchBallMove> _onRelease;

    public void RaiseEvent(PitchBallMove ball)
    {
        _onRelease?.Invoke(ball);
    }

    public void RegisterListener(Action<PitchBallMove> listener)
    {
        _onRelease += listener;
    }

    public void UnregisterListener(Action<PitchBallMove> listener)
    {
        _onRelease -= listener;
    }
}
