using System.Collections.Generic;
using UnityEngine;

public static class BaseCoverCalculator
{
    private static readonly List<BaseCoverAssign> _buffer = new List<BaseCoverAssign>(4);
    private static readonly HashSet<FielderController> _used = new HashSet<FielderController>(4);
    private static readonly List<BaseId> _basesToCover = new List<BaseId>(4);

    public static List<BaseCoverAssign> BaseCoverCalculation(
        List<FielderController> fielders,
        DefenseSituation situation,
        CatchPlan catchPlan,
        BaseManager baseManager)
    {
        _buffer.Clear();
        _used.Clear();
        _basesToCover.Clear();

        if (fielders == null || baseManager == null || situation == null)
            return _buffer;

        if (catchPlan.Catcher != null)
            _used.Add(catchPlan.Catcher);

        BuildBasesToCover(_basesToCover, situation);

        for (int i = 0; i < _basesToCover.Count; i++)
        {
            AssignNearestToBase(_basesToCover[i], fielders, baseManager);
        }

        return _buffer;
    }

    private static void BuildBasesToCover(List<BaseId> bases, DefenseSituation s)
    {
        bases.Clear();

        bool on1 = s.OnFirstBase;
        bool on2 = s.OnSecondBase;
        bool on3 = s.OnThirdBase;

        if (!on1 && !on2 && !on3)
        {
            bases.Add(BaseId.First);
            return;
        }

        if (on1 && on2 && on3)
        {
            bases.Add(BaseId.First);
            bases.Add(BaseId.Second);
            bases.Add(BaseId.Third);
            bases.Add(BaseId.Home);
            return;
        }

        if (on1 && on2 && !on3)
        {
            bases.Add(BaseId.First);
            bases.Add(BaseId.Second);
            bases.Add(BaseId.Third);
            return;
        }

        if (on1 && !on2 && on3)
        {
            bases.Add(BaseId.First);
            bases.Add(BaseId.Second);
            bases.Add(BaseId.Home);
            return;
        }

        if (!on1 && on2 && on3)
        {
            bases.Add(BaseId.First);
            bases.Add(BaseId.Third);
            bases.Add(BaseId.Home);
            return;
        }

        if (on1 && !on2 && !on3)
        {
            bases.Add(BaseId.First);
            bases.Add(BaseId.Second);
            return;
        }

        if (!on1 && on2 && !on3)
        {
            bases.Add(BaseId.First);
            bases.Add(BaseId.Third);
            return;
        }

        bases.Add(BaseId.First);
        bases.Add(BaseId.Home);
    }

    private static void AssignNearestToBase(BaseId baseId, List<FielderController> fielders, BaseManager baseManager)
    {
        Vector3 basePos = baseManager.GetBasePosition(baseId);

        FielderController nearest = null;
        float bestDistSqr = float.MaxValue;

        for (int i = 0; i < fielders.Count; i++)
        {
            var f = fielders[i];
            if (f == null) continue;
            if (_used.Contains(f)) continue;

            float d = (f.transform.position - basePos).sqrMagnitude;
            if (d < bestDistSqr)
            {
                bestDistSqr = d;
                nearest = f;
            }
        }

        if (nearest == null) return;

        _used.Add(nearest);

        float distance = Mathf.Sqrt(bestDistSqr);
        float speed = Mathf.Max(0.01f, nearest.Data.MoveSpeed);
        float arriveTime = distance / speed;

        _buffer.Add(new BaseCoverAssign(baseId, nearest, arriveTime));
    }
}
