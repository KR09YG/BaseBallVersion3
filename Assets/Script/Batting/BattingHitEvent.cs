using System;
using UnityEngine;

[CreateAssetMenu(fileName = "BattingContactEvent", menuName = "Scriptable Objects/BattingContactEvent")]
public class BattingHitEvent : ScriptableObject
{
    private Action<Ball> _onHitAttempt;

    public void RegisterListener(Action<Ball> callback)
    {
        _onHitAttempt += callback;
    }

    public void UnregisterListener(Action<Ball> callback)
    {
        _onHitAttempt -= callback;
    }

    public void RaiseEvent(Ball data)
    {
        _onHitAttempt?.Invoke(data);
    }
}
