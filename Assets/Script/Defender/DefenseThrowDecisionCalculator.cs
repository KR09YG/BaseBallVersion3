using System.Collections.Generic;
using UnityEngine;

public static class DefenseThrowDecisionCalculator
{
    private const float THROW_SPEED = 30f;
    public static List<ThrowStep> ThrowDicision(
        FielderController catchFielder,
        bool isFly,
        List<FielderController> fielders,
        BaseManager baseManager)
    {
        if (catchFielder == null || fielders == null || fielders.Count == 0)
        {
            Debug.LogError("Catch fielder is null");
            return default;
        }

        List<ThrowStep> throwSteps = new List<ThrowStep>();

        if (catchFielder.Data.PositionGroupType == PositionGroupType.Outfield)
        {
            ThrowStep throwStep = CalculateCutoffThrowPlan(catchFielder, fielders);
            throwSteps.Add(throwStep);
            FielderController pitcher = fielders.Find(f => f.Data.Position == PositionType.Pitcher);
            Debug.Assert(pitcher != null, "Pitcher not found. Defense system requires a pitcher.");
            throwSteps.Add(ThrowToFielder(throwStep.ReceiverFielder, pitcher, ThrowPlan.Return));
        }
        else if (catchFielder.Data.PositionGroupType == PositionGroupType.Infield)
        {
            throwSteps.Add(DetermineInfieldThrowPlan(catchFielder, fielders, baseManager));
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
    private static ThrowStep CalculateCutoffThrowPlan(FielderController catchFielder, List<FielderController> fielders)
    {
        ThrowStep throwStep = new ThrowStep();
        // 外野手が捕球した場合、内野手へ送球
        FielderController nearestInfielder = null; // 仮初め
        float nearestDistance = float.MaxValue;

        foreach (var fielder in fielders)
        {
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
        List<FielderController> fielders,
        BaseManager baseManager)
    {
        ThrowStep throwStep = new ThrowStep();
        // 内野手が捕球した場合、とりあえず今は一塁へ送球するプラン
        Vector3 firstPos = baseManager.GetBasePosition(BaseId.First);
        float minDistance = float.MaxValue;
        // 一塁に一番近くにいる内野手を探す
        foreach (var fielder in fielders)
        {
            if (catchFielder == fielder) continue;


            if (fielder.Data.PositionGroupType == PositionGroupType.Infield)
            {
                float distance = (fielder.transform.position - firstPos).magnitude;
                if (distance < minDistance)
                {
                    minDistance = distance;
                    throwStep.Plan = ThrowPlan.First;
                    throwStep.TargetPosition = firstPos;
                    throwStep.ThrowerFielder = catchFielder;
                    throwStep.ReceiverFielder = fielder;
                    throwStep.ThrowSpeed = THROW_SPEED;
                    throwStep.ArriveTime = (catchFielder.transform.position - firstPos).magnitude / THROW_SPEED;
                }
            }
        }
        return throwStep;
    }
}
