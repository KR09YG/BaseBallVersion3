public readonly struct BaseCoverAssign
{
    public readonly BaseId BaseId;
    public readonly FielderController Fielder;
    public readonly float ArriveTime;   // 追加：ベース到達までの秒数

    public BaseCoverAssign(BaseId baseId, FielderController fielder, float arriveTime)
    {
        BaseId = baseId;
        Fielder = fielder;
        ArriveTime = arriveTime;
    }
}