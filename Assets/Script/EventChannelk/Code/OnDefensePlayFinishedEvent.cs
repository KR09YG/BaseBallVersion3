using System;
using UnityEngine;

[CreateAssetMenu(fileName = "OnDefensePlayFinished", menuName = "Scriptable Objects/OnDefensePlayFinished")]
public class OnDefensePlayFinishedEvent : ScriptableObject
{
    private Action<DefensePlayOutcome> _onDefensePlayFinished;
    public void RegisterListener(Action<DefensePlayOutcome> listener)
    {
        _onDefensePlayFinished += listener;
    }

    public void UnregisterListener(Action<DefensePlayOutcome> listener)
    {
        _onDefensePlayFinished -= listener;
    }

    public void RaiseEvent(DefensePlayOutcome outcome)
    {
        _onDefensePlayFinished?.Invoke(outcome);
    }
}
