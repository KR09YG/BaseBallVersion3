using System;
using UnityEngine;

[CreateAssetMenu(fileName = "OnHomeRunRunnerCompleted", menuName = "Scriptable Objects/OnHomeRunRunnerCompleted")]
public class OnHomeRunRunnerCompleted : ScriptableObject
{
    private Action<int> _onHomeRunRunnerCompleted;

    public void RegisterListener(Action<int> listener)
    {
        _onHomeRunRunnerCompleted += listener;
    }

    public void UnregisterListener(Action<int> listener)
    {
        _onHomeRunRunnerCompleted -= listener;
    }

    public void RaiseEvent(int runnerCount)
    {
        _onHomeRunRunnerCompleted?.Invoke(runnerCount);
    }
}
