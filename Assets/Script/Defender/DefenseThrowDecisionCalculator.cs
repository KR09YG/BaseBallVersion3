using System.Collections.Generic;
using UnityEngine;

public static class DefenseThrowDecisionCalculator
{
    private const float THROW_SPEED = 30f;
    private static readonly List<RunnerETA> _etaBuffer = new(4);
    public static List<ThrowStep> ThrowDicision(
        FielderController catchFielder, bool isFly,
        Dictionary<PositionType, FielderController> fielders,
        BaseManager baseManager, DefenseSituation situation,
        RunnerManager runnerManager)
    {
        runnerManager.GetAllRunningETAs(_etaBuffer, true);

        foreach(var eta in _etaBuffer)
        {
            Debug.Log($"[DefenseThrowDecisionCalculator] Runner {eta.GetType()} ETA: {eta.Remaining:F2} sec");
        }

        if (catchFielder == null || fielders == null || fielders.Count == 0)
        {
            Debug.LogError("Catch fielder is null");
            return default;
        }

        List<ThrowStep> throwSteps = new List<ThrowStep>();

        // 外野手が捕球した場合、内野手へ送球し、さらに投手へ返球する
        if (catchFielder.Data.PositionGroupType == PositionGroupType.Outfield)
        {
            ThrowStep throwStep = CalculateCutoffThrowPlan(catchFielder, fielders);
            throwSteps.Add(throwStep);
            FielderController pitcher = fielders[PositionType.Pitcher];
            Debug.Assert(pitcher != null, "Pitcher not found. Defense system requires a pitcher.");
            throwSteps.Add(ThrowToFielder(throwStep.ReceiverFielder, pitcher, ThrowPlan.Return));
        }
        else if (catchFielder.Data.PositionGroupType == PositionGroupType.Infield)
        {
            throwSteps.Add(DetermineInfieldThrowPlan(catchFielder, fielders, baseManager, isFly, situation, runnerManager));
        }

        return throwSteps;
    }

    private static ThrowStep ThrowToFielder(FielderController thrower, FielderController catcher, ThrowPlan plan)
    {
        ThrowStep throwStep = new ThrowStep
        {
            Plan = plan,
            TargetPosition = catcher.transform.position,
            ThrowerFielder = thrower,
            ReceiverFielder = catcher,
            ThrowSpeed = THROW_SPEED,
            ArriveTime =
                (thrower.transform.position - catcher.transform.position).magnitude / THROW_SPEED
        };

        return throwStep;
    }

    /// <summary>
    /// 外野手からの送球プランを決定する
    /// </summary>
    private static ThrowStep CalculateCutoffThrowPlan(FielderController catchFielder, Dictionary<PositionType, FielderController> fielders)
    {
        ThrowStep throwStep = new ThrowStep();
        // 外野手が捕球した場合、内野手へ送球
        FielderController nearestInfielder = null; // 仮初め
        float nearestDistance = float.MaxValue;

        foreach (var key in fielders)
        {
            FielderController fielder = key.Value;

            if (fielder.Data.PositionGroupType == PositionGroupType.Infield)
            {
                float distance = (catchFielder.transform.position - fielder.transform.position).magnitude;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestInfielder = fielder;
                }
            }
        }

        throwStep.Plan = ThrowPlan.Cutoff;
        throwStep.TargetPosition = nearestInfielder.transform.position;
        throwStep.ThrowerFielder = catchFielder;
        throwStep.ReceiverFielder = nearestInfielder;
        throwStep.ThrowSpeed = THROW_SPEED;
        throwStep.ArriveTime = nearestDistance / THROW_SPEED;

        return throwStep;
    }

    /// <summary>
    /// 内野手からの送球プランを決定する
    /// </summary>
    private static ThrowStep DetermineInfieldThrowPlan(
        FielderController catchFielder,
        Dictionary<PositionType, FielderController> fielders,
        BaseManager baseManager, bool isFly, DefenseSituation situation,
        RunnerManager runnerManager)
    {
        ThrowStep throwStep = new ThrowStep();
        Vector3 firstPos = baseManager.GetBasePosition(BaseId.First);
        Vector3 secondPos = baseManager.GetBasePosition(BaseId.Second);
        Vector3 ThirdPos = baseManager.GetBasePosition(BaseId.Third);
        Vector3 HomePos = baseManager.GetBasePosition(BaseId.Home);

        // 内野フライの場合
        if (isFly)
        {
            // フライ捕球の場合、まずは投手へ返球するプラン
            FielderController pitcher = fielders[PositionType.Pitcher];
            throwStep = ThrowToFielder(catchFielder, pitcher, ThrowPlan.Return);
            return throwStep;
        }
        else
        {
            // 2アウトの場合、まずは1塁へ送球
            if (situation.OutCount == 2)
            {
                throwStep = ThrowToFielder(
                    catchFielder,
                    FindNearBaseFielder(fielders, catchFielder, firstPos),
                    ThrowPlan.First);
            }
            else
            {

            }
        }

        return throwStep;
    }

    private static FielderController FindNearBaseFielder(
        Dictionary<PositionType, FielderController> fielders, FielderController catcher, Vector3 BasePos)
    {
        float minDistance = float.MaxValue;
        FielderController nearFielder = null;

        foreach (var key in fielders)
        {
            // ボールを捕球したフィールダーは除外
            if (key.Value == catcher) continue;
            FielderController fielder = key.Value;
            float distance = (fielder.transform.position - BasePos).magnitude;
            if (distance < minDistance)
            {
                minDistance = distance;
                nearFielder = fielder;
            }
        }

        return nearFielder;
    }
}
