using UnityEngine;
using System;

[CreateAssetMenu(fileName = "OnDefenseCompletedEvent", menuName = "Scriptable Objects/OnDefenseCompletedEvent")]
public class OnDefenseCompletedEvent : ScriptableObject
{
    private Action _OndefenseCompleted;

    public void RaiseEvent()
    {
        _OndefenseCompleted?.Invoke();
    }

    public void RegisterListener(Action listener)
    {
        _OndefenseCompleted += listener;
    }

    public void UnregisterListener(Action listener)
    {
        _OndefenseCompleted -= listener;
    }   
}
