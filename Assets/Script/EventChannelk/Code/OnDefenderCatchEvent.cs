using System;
using UnityEngine;

[CreateAssetMenu(fileName = "DefenderCatchEvent", menuName = "Scriptable Objects/DefenderCatchEvent")]
public class OnDefenderCatchEvent : ScriptableObject
{
    private Action<FielderController> _defenderCatch;

    public void RegisterListener(Action<FielderController> listener)
    {
        _defenderCatch += listener;
    }

    public void UnregisterListener(Action<FielderController> listener)
    {
        _defenderCatch -= listener;
    }

    public void RaiseEvent(FielderController action)
    {
        _defenderCatch?.Invoke(action);
    }
}
