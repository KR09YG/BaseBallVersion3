using System;
using UnityEngine;

[CreateAssetMenu(fileName = "OnAllRunnersStopped", menuName = "Scriptable Objects/OnAllRunnersStopped")]
public class OnAllRunnersStopped : ScriptableObject
{
    private Action<RunningSummary> _OnAllRunnersStopped;
    
    public void RegisterListener(Action<RunningSummary> listener)
    {
        _OnAllRunnersStopped += listener;
    }

    public void UnregisterListener(Action<RunningSummary> listener)
    {
        _OnAllRunnersStopped -= listener;
    }

    public void RaiseEvent(RunningSummary summary)
    {
        _OnAllRunnersStopped?.Invoke(summary);
    }
}
