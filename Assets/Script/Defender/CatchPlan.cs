using UnityEngine;

public struct CatchPlan
{
    public bool CanCatch;

    public int CatchTrajectoryIndex;
    public Vector3 CatchPoint;
    public float CatchTime;

    public FielderController Catcher;
}
