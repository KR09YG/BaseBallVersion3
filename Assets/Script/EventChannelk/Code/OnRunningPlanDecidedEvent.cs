using System;
using UnityEngine;

[CreateAssetMenu(fileName = "OnRunningPlanDecidedEvent", menuName = "Scriptable Objects/OnRunningPlanDecidedEvent")]
public class OnRunningPlanDecidedEvent : ScriptableObject
{
    private Action<RunningPlan> _runningPlanEvent;


    public void RegisterListener(Action<RunningPlan> listener)
    {
        _runningPlanEvent += listener;
    }

    public void UnregisterListener(Action<RunningPlan> listener)
    {
        _runningPlanEvent -= listener;
    }
    public void RaiseEvent(RunningPlan plan)
    {
        _runningPlanEvent?.Invoke(plan);
    }
}
