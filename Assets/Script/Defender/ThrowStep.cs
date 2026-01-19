using UnityEngine;

public enum ThrowPlan
{
    First,
    Second,
    Third,
    Home,
    Cutoff,
    Return
}

public struct ThrowStep
{
    public ThrowPlan Plan;
    public Vector3 TargetPosition;
    public FielderController ThrowerFielder;
    public FielderController ReceiverFielder;
    public float ThrowSpeed;
    public float ArriveTime;
}
