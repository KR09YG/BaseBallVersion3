using System;
using UnityEngine;

[CreateAssetMenu(fileName = "OnDecidedCatchPlan", menuName = "Scriptable Objects/OnDecidedCatchPlan")]
public class OnDecidedCatchPlan : ScriptableObject
{
    private Action<CatchPlan> _onDecidedCanFly;

    public void Register(Action<CatchPlan> listener)
    {
        _onDecidedCanFly += listener;
    }

    public void Unregister(Action<CatchPlan> listener)
    {
        _onDecidedCanFly -= listener;
    }

    public void RaiseEvent(CatchPlan catchPlan)
    {
        _onDecidedCanFly?.Invoke(catchPlan);
    }
}
