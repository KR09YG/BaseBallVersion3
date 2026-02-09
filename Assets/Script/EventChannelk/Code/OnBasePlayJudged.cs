using UnityEngine;
using System;

[CreateAssetMenu(fileName = "OnBasePlayJudged", menuName = "Scriptable Objects/OnBasePlayJudged")]
public class OnBasePlayJudged : ScriptableObject
{
    private Action<BasePlayJudgement> _onJudged;

    public void RegisterListener(Action<BasePlayJudgement> listener)
    {
        _onJudged += listener;
    }

    public void UnregisterListener(Action<BasePlayJudgement> listener)
    {
        _onJudged -= listener;
    }

    public void RaiseEvent(BasePlayJudgement judgement)
    {
        _onJudged?.Invoke(judgement);
    }
}
