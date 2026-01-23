using System.Collections.Generic;
using UnityEngine;

public static class BaseCoverCalculator
{
    private static readonly List<BaseCoverAssign> _buffer = new List<BaseCoverAssign>(4);
    private static readonly HashSet<FielderController> _used = new HashSet<FielderController>(4);

    private static readonly List<BaseId> _basesToCover = new(4);

    public static List<BaseCoverAssign> BaseCoverCalculation(
        List<FielderController> fielders,
        DefenseSituation situation,
        CatchPlan catchPlan,
        BaseManager baseManager,
        BattingBallResult result)
    {
        _buffer.Clear();
        _used.Clear();
        _basesToCover.Clear();

        if (fielders == null || baseManager == null || situation == null)
            return _buffer;

        if (catchPlan.Catcher != null)
            _used.Add(catchPlan.Catcher);

        BuildBasesToCover(_basesToCover, situation);

        foreach (var baseId in _basesToCover)
        {
            AssignNearestToBase(baseId, fielders, baseManager);
        }

        Debug.Log($"[Cover] basesToCover={string.Join(",", _basesToCover)}");
        foreach (var a in _buffer)
            Debug.Log($"[CoverAssign] base={a.BaseId} fielder={a.Fielder.name} arrive={a.ArriveTime:F2}s");

        // Ç‡Çµ Third Ç™ä‹Ç‹ÇÍÇÈÇÃÇ… assign Ç™ñ≥Ç¢Ç»ÇÁÅAÇ±Ç±Ç≈ï™Ç©ÇÈ


        return _buffer;
    }

    private static void BuildBasesToCover(List<BaseId> bases, DefenseSituation s)
    {
        bases.Clear();

        bool on1 = s.OnFirstBase;
        bool on2 = s.OnSecondBase;
        bool on3 = s.OnThirdBase;

        // Ç»Çµ
        if (!on1 && !on2 && !on3)
        {
            bases.Add(BaseId.First);
            return;
        }

        // ñûó€
        if (on1 && on2 && on3)
        {
            bases.Add(BaseId.First);
            bases.Add(BaseId.Second);
            bases.Add(BaseId.Third);
            bases.Add(BaseId.Home);
            return;
        }

        // 1ÅE2ó€
        if (on1 && on2 && !on3)
        {
            bases.Add(BaseId.First);
            bases.Add(BaseId.Second);
            bases.Add(BaseId.Third);
            return;
        }

        // 1ÅE3ó€
        if (on1 && !on2 && on3)
        {
            bases.Add(BaseId.First);
            bases.Add(BaseId.Second);
            bases.Add(BaseId.Home);
            return;
        }

        // 2ÅE3ó€
        if (!on1 && on2 && on3)
        {
            bases.Add(BaseId.First);
            bases.Add(BaseId.Third);
            bases.Add(BaseId.Home);
            return;
        }

        // 1ó€
        if (on1 && !on2 && !on3)
        {
            bases.Add(BaseId.First);
            bases.Add(BaseId.Second);
            return;
        }

        // 2ó€
        if (!on1 && on2 && !on3)
        {
            bases.Add(BaseId.First);
            bases.Add(BaseId.Third);
            return;
        }

        // 3ó€
        bases.Add(BaseId.First);
        bases.Add(BaseId.Home);
    }

    private static void AssignNearestToBase(
    BaseId baseId,
    List<FielderController> fielders,
    BaseManager baseManager)
    {
        Vector3 basePos = baseManager.GetBasePosition(baseId);

        FielderController nearest = null;
        float bestDistSqr = float.MaxValue;

        foreach (var f in fielders)
        {
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
        float speed = Mathf.Max(0.01f, nearest.Data.MoveSpeed); // 0äÑñhé~
        float arriveTime = distance / speed;

        _buffer.Add(new BaseCoverAssign(baseId, nearest, arriveTime));

        if (nearest == null)
            Debug.LogWarning($"[CoverAssign] FAILED: base={baseId} (no available fielder). used={_used.Count} fielders={fielders.Count}");
    }
}
