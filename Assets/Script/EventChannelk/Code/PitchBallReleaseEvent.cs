using UnityEngine;
using System;

[CreateAssetMenu(fileName = "PitchBallReleaseEvent", menuName = "Scriptable Objects/PitchBallReleaseEvent")]
public class PitchBallReleaseEvent : ScriptableObject
{
    private Action<Ball> _onRelease;

    public void RaiseEvent(Ball ball)
    {
        _onRelease?.Invoke(ball);
    }

    public void RegisterListener(Action<Ball> listener)
    {
        _onRelease += listener;
    }

    public void UnregisterListener(Action<Ball> listener)
    {
        _onRelease -= listener;
    }
}
