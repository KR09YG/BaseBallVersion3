using UnityEngine;
using System.Collections.Generic;

public static class DefenseCalculator
{
    public static CatchPlan CalculateCatchPlan(
        List<Vector3> trajectory,
        float totalFlightTime,
        List<FielderController> fielders)
    {
        CatchPlan bestPlan = new CatchPlan
        {
            CanCatch = false,
            CatchTime = float.MaxValue
        };

        int count = trajectory.Count;

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / (count - 1) * totalFlightTime;
            Vector3 ballPos = trajectory[i];

            foreach (var fielder in fielders)
            {
                FielderData data = fielder.Data;

                float moveTime =
                    Vector3.Distance(fielder.transform.position, ballPos)
                    / data.MoveSpeed
                    + data.ReactionTime;

                if (moveTime <= t && t < bestPlan.CatchTime)
                {
                    bestPlan.CanCatch = true;
                    bestPlan.Catcher = fielder;
                    bestPlan.CatchPoint = ballPos;
                    bestPlan.CatchTime = t;
                    bestPlan.CatchTrajectoryIndex = i;
                }
            }
        }

        return bestPlan;
    }
}
