using System;
using UnityEngine;
using static DefenseManager;

[CreateAssetMenu(fileName = "OnDefensePlayJudged", menuName = "Scriptable Objects/OnDefensePlayJudged")]
public class OnDefensePlayJudged : ScriptableObject
{
    private Action<DefensePlayOutcome> _onDefensePlayJudgedEvent;

    public void RegisterListener(Action<DefensePlayOutcome> listener)
    {
        _onDefensePlayJudgedEvent += listener;
    }

    public void UnregisterListener(Action<DefensePlayOutcome> listener)
    {
        _onDefensePlayJudgedEvent -= listener;
    }

    public void RaiseEvent(DefensePlayOutcome defensePlayOutcome)
    {
        _onDefensePlayJudgedEvent?.Invoke(defensePlayOutcome);
    }
}
