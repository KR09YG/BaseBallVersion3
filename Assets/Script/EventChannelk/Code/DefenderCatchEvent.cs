using System;
using UnityEngine;

[CreateAssetMenu(fileName = "DefenderCatchEvent", menuName = "Scriptable Objects/DefenderCatchEvent")]
public class DefenderCatchEvent : ScriptableObject
{
    private Action<FielderController, bool> _defenderCatch;

    public void RegisterListener(Action<FielderController, bool> listener)
    {
        _defenderCatch += listener;
    }

    public void UnregisterListener(Action<FielderController, bool> listener)
    {
        _defenderCatch -= listener;
    }

    public void RaiseEvent(FielderController action, bool isFly)
    {
        _defenderCatch?.Invoke(action, isFly);
    }
}
