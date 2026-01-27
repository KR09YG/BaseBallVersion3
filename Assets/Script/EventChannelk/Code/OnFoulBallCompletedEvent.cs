using System;
using UnityEngine;

[CreateAssetMenu(fileName = "OnFoulBallCompleted", menuName = "Scriptable Objects/OnFoulBallCompleted")]
public class OnFoulBallCompletedEvent : ScriptableObject
{
    private Action _onFoulBallCompleted;

    public void RegisterListener(Action listener)
    {
        _onFoulBallCompleted += listener;
    }

    public void UnregisterListener(Action listener)
    {
        _onFoulBallCompleted -= listener;
    }

    public void RaiseEvent()
    {
        _onFoulBallCompleted?.Invoke();
    }
}
