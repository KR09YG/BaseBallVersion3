using System;
using UnityEngine;

[CreateAssetMenu(fileName = "BattingInput", menuName = "Scriptable Objects/BattingInput")]
public class BattingInputEvent : ScriptableObject
{
    private Action _action;

    public void RegisterListener(Action listener)
    {
        _action += listener;
    }

    public void RaiseEvent()
    {
        _action?.Invoke();
    }

    public void UnregisterListener(Action listener)
    {
        _action -= listener;
    }
}
