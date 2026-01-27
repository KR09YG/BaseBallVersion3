using System;
using UnityEngine;

[CreateAssetMenu(fileName = "OnStrikeJudge", menuName = "Scriptable Objects/OnStrikeJudge")]
public class OnStrikeJudgeEvent : ScriptableObject
{
    private Action<bool,Vector3> _onStrikeJudge;

    public void RegisterListener(Action<bool, Vector3> listener)
    {
        _onStrikeJudge += listener;
    }

    public void UnregisterListener(Action<bool, Vector3> listener)
    {
        _onStrikeJudge -= listener;
    }

    public void RaiseEvent(bool isStrike, Vector3 crossPos)
    {
        _onStrikeJudge?.Invoke(isStrike, crossPos);
    }
}
