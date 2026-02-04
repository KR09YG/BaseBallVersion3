using System.Collections.Generic;
using UnityEngine;

public static class DefenseRolePlanner
{
    public static DefenseRoles CreateRoles(CatchPlan catchPlan, List<FielderController> fielders)
    {
        DefenseRoles roles = new DefenseRoles
        {
            BaseCovers = new Dictionary<BaseType, FielderController>()
        };

        if (fielders == null || fielders.Count == 0)
            return roles;

        float minDist = float.MaxValue;
        FielderController cutoff = null;

        for (int i = 0; i < fielders.Count; i++)
        {
            var fielder = fielders[i];
            if (fielder == null) continue;
            if (fielder == catchPlan.Catcher) continue;

            if (!IsInfielder(fielder.Data.Position)) continue;

            float dist = Vector3.Distance(fielder.transform.position, catchPlan.CatchPoint);
            if (dist < minDist)
            {
                minDist = dist;
                cutoff = fielder;
            }
        }

        roles.CutoffMan = cutoff;
        return roles;
    }

    private static bool IsInfielder(PositionType pos)
    {
        return pos == PositionType.FirstBase
            || pos == PositionType.SecondBase
            || pos == PositionType.ThirdBase
            || pos == PositionType.ShortStop
            || pos == PositionType.Pitcher;
    }
}
