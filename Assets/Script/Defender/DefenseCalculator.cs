using UnityEngine;
using System.Collections.Generic;

public static class DefenseCalculator
{
    public static CatchPlan CalculateCatchPlan(
    List<Vector3> trajectory,
    BattingBallResult result,
    float deltaTime,
    List<FielderController> fielders)
    {
        if (result.IsFoul)
        {
            Debug.Log("Foul Ball - No Defense Action");
            return new CatchPlan
            {
                CanCatch = false,
                CatchTime = float.MaxValue
            };
        }
        CatchPlan bestPlan = new CatchPlan
        {
            CanCatch = false,
            CatchTime = float.MaxValue
        };


        for (int i = 0; i < trajectory.Count; i++)
        {
            float t = i * deltaTime;
            Vector3 ballPos = trajectory[i];

            float bestMoveTime = float.MaxValue;
            FielderController bestFielder = null;

            foreach (var fielder in fielders)
            {
                var data = fielder.Data;
                if (ballPos.y > data.CatchHeight) continue;

                Vector3 fPos = fielder.transform.position; fPos.y = 0f;
                Vector3 bPos = ballPos; bPos.y = 0f;

                float moveTime = Vector3.Distance(fPos, bPos) / data.MoveSpeed + data.ReactionTime;

                if (moveTime <= t && moveTime < bestMoveTime)
                {
                    bestMoveTime = moveTime;
                    bestFielder = fielder;
                }
            }

            // この時刻tで捕球可能な人がいるなら、ここが最速時刻なので確定してbreak
            if (bestFielder != null)
            {
                bestPlan.CanCatch = true;
                bestPlan.Catcher = bestFielder;
                bestPlan.CatchPoint = ballPos;
                bestPlan.CatchTime = t;
                bestPlan.CatchTrajectoryIndex = i;
                break;
            }
        }

        // 終点までにキャッチできない場合の処理
        if (!bestPlan.CanCatch)
        {
            // 最終地点に最も早く到達できる野手を探す
            float bestArrivalTime = float.MaxValue;
            Vector3 finalPos = trajectory[trajectory.Count - 1];

            foreach (var fielder in fielders)
            {
                FielderData data = fielder.Data;
                Vector3 fielderPosXZ = fielder.transform.position;
                fielderPosXZ.y = 0f;

                Vector3 ballPosXZ = finalPos;
                ballPosXZ.y = 0f;

                float moveTime =
                    Vector3.Distance(fielderPosXZ, ballPosXZ)
                    / data.MoveSpeed
                    + data.ReactionTime;

                if (moveTime < bestArrivalTime)
                {
                    bestArrivalTime = moveTime;

                    bestPlan.Catcher = fielder;
                    bestPlan.CatchPoint = finalPos;
                    bestPlan.CatchTime = moveTime;
                    bestPlan.CatchTrajectoryIndex = trajectory.Count - 1;
                }
            }
        }

        return bestPlan;
    }


}
