using System.Collections.Generic;
using UnityEngine;

public static class DefenseCalculator
{
    private const float GROUND_Y = 0f;
    public static CatchPlan CalculateCatchPlan(
        List<Vector3> trajectory,
        BattingBallResult result,
        float deltaTime,
        List<FielderController> fielders)
    {
        if (result.BallType == BattingBallType.Foul || result.BallType == BattingBallType.HomeRun)
        {
            return CreateNoDefensePlan();
        }

        CatchPlan bestPlan = CreateNoDefensePlan();

        if (trajectory == null || trajectory.Count == 0 || fielders == null || fielders.Count == 0)
            return bestPlan;


        // ここが肝：最初の接地indexを軌道から取る
        int landingIndex = FindFirstGroundContactIndex(trajectory, GROUND_Y);

        // landingIndexが見つからない場合だけLandingPositionにフォールバック
        if (landingIndex < 0)
            landingIndex = FindLandingIndexXZ(trajectory, result.LandingPosition);

        for (int i = 0; i < trajectory.Count; i++)
        {
            float t = i * deltaTime;
            Vector3 ballPos = trajectory[i];

            float bestMoveTime = float.MaxValue;
            FielderController bestFielder = null;

            for (int f = 0; f < fielders.Count; f++)
            {
                var fielder = fielders[f];
                if (fielder == null) continue;

                var data = fielder.Data;
                if (data == null) continue;

                // 捕球可能高さより高いなら無理
                if (ballPos.y > data.CatchHeight) continue;

                Vector3 fPos = fielder.transform.position; fPos.y = 0f;
                Vector3 bPos = ballPos; bPos.y = 0f;

                float moveTime =
                    Vector3.Distance(fPos, bPos) / Mathf.Max(0.01f, data.MoveSpeed)
                    + Mathf.Max(0f, data.ReactionTime);

                if (moveTime <= t && moveTime < bestMoveTime)
                {
                    bestMoveTime = moveTime;
                    bestFielder = fielder;
                }
            }

            if (bestFielder != null)
            {
                bestPlan.CanCatch = true;
                bestPlan.Catcher = bestFielder;
                bestPlan.CatchPoint = ballPos;
                bestPlan.CatchTime = t;
                bestPlan.CatchTrajectoryIndex = i;

                // ここも肝：接地より前に捕ったらフライ
                bestPlan.IsFly = (landingIndex >= 0) ? (i < landingIndex) : false;

                // 内外野判定（必要なら後でThrowTime計算に使う）
                bestPlan.IsOutfield =
                    bestFielder.Data != null &&
                    bestFielder.Data.PositionGroupType == PositionGroupType.Outfield;

                return bestPlan;
            }
        }

        // キャッチできない＝回収担当（最終点へ最短到達の野手）
        {
            float bestArrivalTime = float.MaxValue;
            Vector3 finalPos = trajectory[trajectory.Count - 1];

            for (int f = 0; f < fielders.Count; f++)
            {
                var fielder = fielders[f];
                if (fielder == null) continue;

                var data = fielder.Data;
                if (data == null) continue;

                Vector3 fielderPosXZ = fielder.transform.position; fielderPosXZ.y = 0f;
                Vector3 ballPosXZ = finalPos; ballPosXZ.y = 0f;

                float moveTime =
                    Vector3.Distance(fielderPosXZ, ballPosXZ) / Mathf.Max(0.01f, data.MoveSpeed)
                    + Mathf.Max(0f, data.ReactionTime);

                if (moveTime < bestArrivalTime)
                {
                    bestArrivalTime = moveTime;

                    bestPlan.CanCatch = false; // 回収なのでcatch扱いにしないならfalseのままでもOK
                    bestPlan.Catcher = fielder;
                    bestPlan.CatchPoint = finalPos;
                    bestPlan.CatchTime = moveTime;
                    bestPlan.CatchTrajectoryIndex = trajectory.Count - 1;
                    bestPlan.IsFly = false;
                    bestPlan.IsOutfield =
                        fielder.Data != null &&
                        fielder.Data.PositionGroupType == PositionGroupType.Outfield;
                }
            }
        }

        return bestPlan;
    }

    private static CatchPlan CreateNoDefensePlan()
    {
        return new CatchPlan
        {
            CanCatch = false,
            CatchTime = float.MaxValue,
            IsFly = false,
            IsOutfield = false,
            ThrowToFirstTime = float.MaxValue,
            ThrowToSecondTime = float.MaxValue,
            ThrowToThirdTime = float.MaxValue,
            ThrowToHomeTime = float.MaxValue
        };
    }

    // 最初に地面に触れた index（バウンドの始点）を返す。見つからなければ -1
    private static int FindFirstGroundContactIndex(List<Vector3> trajectory, float groundY)
    {
        // 軌道生成の都合でちょい浮いたりするので許容誤差を持つ
        const float eps = 0.01f;

        for (int i = 0; i < trajectory.Count; i++)
        {
            if (trajectory[i].y <= groundY + eps)
                return i;
        }
        return -1;
    }

    // フォールバック：LandingPositionに最も近い点（XZ）
    private static int FindLandingIndexXZ(List<Vector3> trajectory, Vector3 landingPosition)
    {
        int bestIndex = trajectory.Count - 1;
        float bestDistSq = float.MaxValue;

        Vector3 l = landingPosition; l.y = 0f;

        for (int i = 0; i < trajectory.Count; i++)
        {
            Vector3 p = trajectory[i]; p.y = 0f;
            float d = (p - l).sqrMagnitude;
            if (d < bestDistSq)
            {
                bestDistSq = d;
                bestIndex = i;
            }
        }

        return bestIndex;
    }
}
