using System;
using UnityEngine;

[CreateAssetMenu(fileName = "OnFinMissSwingEvent", menuName = "Scriptable Objects/OnFinMissSwingEvent")]
public class OnFinMissSwingEvent : ScriptableObject
{
    private Action _finSwing;

    public void RegisterListener(Action listener)
    {
        _finSwing += listener;
    }

    public void UnregisterListener(Action listener)
    {
        _finSwing -= listener;
    }

    public void RaiseEvent()
    {
        _finSwing?.Invoke();
    }
}
