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
        CatchPlan bestPlan = new CatchPlan
        {
            CanCatch = false,
            CatchTime = float.MaxValue
        };

        for (int i = 0; i < trajectory.Count; i++)
        {
            // ボールがその座標に来る時間
            float t = i * deltaTime;
            Vector3 ballPos = trajectory[i];

            foreach (var fielder in fielders)
            {
                FielderData data = fielder.Data;

                // 野手の高さより高い位置にボールがある場合はスルー
                if (ballPos.y > data.CatchHeight)
                {
                    continue;
                }

                // 野手がボールに到達するのにかかる時間
                float moveTime =
                    Vector3.Distance(fielder.transform.position, ballPos)
                    / data.MoveSpeed
                    + data.ReactionTime;

                // ボールがその座標に来るまでに野手が到達できるか
                if (moveTime <= t && t < bestPlan.CatchTime)
                {
                    // キャッチ可能な最速プランを更新
                    bestPlan.CanCatch = true;
                    bestPlan.Catcher = fielder;
                    bestPlan.CatchPoint = ballPos;
                    bestPlan.CatchTime = t;
                    bestPlan.CatchTrajectoryIndex = i;
                    break;
                }
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

                float moveTime =
                    Vector3.Distance(fielder.transform.position, finalPos)
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
