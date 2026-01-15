using System.Collections.Generic;
using UnityEngine;

public static class DefenseRolePlanner
{
    public static DefenseRoles CreateRoles(
        CatchPlan catchPlan,
        List<FielderController> fielders)
    {
        DefenseRoles roles = new DefenseRoles
        {
            BaseCovers = new Dictionary<BaseType, FielderController>()
        };

        if (!catchPlan.CanCatch)
            return roles;

        // íÜåpÅiÇ∆ÇËÇ†Ç¶Ç∏ì‡ñÏéËÇ≈ç≈Ç‡ãﬂÇ¢êlÅj
        float minDist = float.MaxValue;

        foreach (var fielder in fielders)
        {
            if (fielder == catchPlan.Catcher)
                continue;

            if (!IsInfielder(fielder.Data.Position))
                continue;

            float dist = Vector3.Distance(
                fielder.transform.position,
                catchPlan.CatchPoint);

            if (dist < minDist)
            {
                minDist = dist;
                roles.CutoffMan = fielder;
            }
        }

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
