using NUnit.Framework;
using System;
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "BattingTrajectoryEvent", menuName = "Scriptable Objects/BattingTrajectoryEvent")]
public class BattingBallTrajectoryEvent : ScriptableObject
{
    private Action<List<Vector3>> _ballResultAction;

    public void RegisterListener(Action<List<Vector3>> listener)
    {
        _ballResultAction += listener;
    }

    public void UnregisterListener(Action<List<Vector3>> listener)
    {
        _ballResultAction -= listener;
    }

    public void RaiseEvent(List<Vector3> result)
    {
        _ballResultAction?.Invoke(result);
    }
}
