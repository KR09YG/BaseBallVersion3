using System;
using UnityEngine;

[CreateAssetMenu(fileName = "OnDefensePlanDecidedEvent", menuName = "Scriptable Objects/OnDefensePlanDecidedEvent")]
public class OnDefensePlanDecidedEvent : ScriptableObject
{
    private Action<DefensePlan> _defensePlanEvent;

    public void RegisterListener(Action<DefensePlan> listener)
    {
        _defensePlanEvent += listener;
    }

    public void UnregisterListener(Action<DefensePlan> listener)
    {
        _defensePlanEvent -= listener;
    }

    public void RaiseEvent(DefensePlan plan)
    {
        _defensePlanEvent?.Invoke(plan);
    }
}
