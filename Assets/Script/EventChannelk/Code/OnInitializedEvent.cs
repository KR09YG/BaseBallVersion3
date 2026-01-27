using System;
using UnityEngine;

[CreateAssetMenu(fileName = "InitializedEvent", menuName = "Scriptable Objects/InitializedEvent")]
public class OnInitializedEvent : ScriptableObject
{
    private Action _initializedEvent;

    public void Raise()
    {
        _initializedEvent?.Invoke();
    }

    public void RegisterListener(Action listener)
    {
        _initializedEvent += listener;
    }

    public void UnregisterListener(Action listener)
    {
        _initializedEvent -= listener;
    }
}
