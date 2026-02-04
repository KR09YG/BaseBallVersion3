using UnityEngine;

public struct CatchPlan
{
    public bool CanCatch;
    public bool IsFly;

    // 守備側で判定して渡す（Runner側で距離閾値を持たない）
    public bool IsOutfield;

    public FielderController Catcher;
    public Vector3 CatchPoint;
    public float CatchTime;
    public int CatchTrajectoryIndex;

    // 追加：この打球に対して「その塁に送球が到達する予測秒（相対）」を詰めて渡す
    // 未計算・意味なしの場合は float.MaxValue を入れる
    public float ThrowToFirstTime;
    public float ThrowToSecondTime;
    public float ThrowToThirdTime;
    public float ThrowToHomeTime;

    public float GetThrowArrivalTime(BaseId baseId)
    {
        switch (baseId)
        {
            case BaseId.First: return ThrowToFirstTime;
            case BaseId.Second: return ThrowToSecondTime;
            case BaseId.Third: return ThrowToThirdTime;
            case BaseId.Home: return ThrowToHomeTime;
            default: return float.MaxValue;
        }
    }
}
