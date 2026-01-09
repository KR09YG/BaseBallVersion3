using System;
using UnityEngine;

[CreateAssetMenu(fileName = "BallReachedTargetEvent", menuName = "Scriptable Objects/NewScriptableObjectScript")]
public class BallReachedTargetEvent : ScriptableObject
{
    private Action<Ball> _reachedAction;

    public void RegisterListener(Action<Ball> listener)
    {
        _reachedAction += listener;
    }

    public void UnregisterListener(Action<Ball> listener)
    {
        _reachedAction -= listener;
    }

    public void RaiseEvent(Ball ball)
    {
        _reachedAction?.Invoke(ball);
    }
}
