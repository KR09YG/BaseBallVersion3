using System;
using UnityEngine;

[CreateAssetMenu(fileName = "OnRunnerReachedHomeThisPlay", menuName = "Scriptable Objects/OnRunnerReachedHomeThisPlay")]
public class OnRunnerReachedHomeThisPlay : ScriptableObject
{
    private Action<RunnerType> _onRunnerReachedHome;

    public void RegisterListener(Action<RunnerType> onRunnerReachedHome)
    {
        _onRunnerReachedHome += onRunnerReachedHome;
    }

    public void UnregisterListener(Action<RunnerType> onRunnerReachedHome)
    {
        _onRunnerReachedHome -= onRunnerReachedHome;
    }

    public void RaiseEvent(RunnerType type)
    {
        _onRunnerReachedHome.Invoke(type);
    }
}
