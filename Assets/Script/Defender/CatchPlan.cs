using UnityEngine;

public struct CatchPlan
{
    public bool CanCatch;
    public bool IsFly;

    // ç”õ‘¤‚Å”»’è‚µ‚Ä“n‚·iRunner‘¤‚Å‹——£è‡’l‚ğ‚½‚È‚¢j
    public bool IsOutfield;

    public FielderController Catcher;
    public Vector3 CatchPoint;
    public float CatchTime;
    public int CatchTrajectoryIndex;

    public float ThrowToFirstTime;
    public float ThrowToSecondTime;
    public float ThrowToThirdTime;
    public float ThrowToHomeTime;
}
