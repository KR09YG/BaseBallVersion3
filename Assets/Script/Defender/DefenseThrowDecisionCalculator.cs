using System.Collections.Generic;
using UnityEngine;

public static class DefenseThrowDecisionCalculator
{
    private const float THROW_SPEED = 30f;
    private static readonly List<RunnerETA> _etaBuffer = new(4);

    private static readonly BaseId[] _priority =
    {
        BaseId.Home, BaseId.Third, BaseId.Second, BaseId.First
    };

    public static List<ThrowStep> ThrowDicision(
        FielderController catchFielder, bool isFly,
        Dictionary<PositionType, FielderController> fielders,
        BaseManager baseManager, DefenseSituation situation,
        RunnerManager runnerManager)
    {
        if (catchFielder == null || fielders == null || fielders.Count == 0)
        {
            Debug.LogError("[Decision] catchFielder null / fielders empty");
            return null;
        }
        if (runnerManager == null)
        {
            Debug.LogError("[Decision] runnerManager is null");
            return null;
        }

        runnerManager.GetAllRunningETAs(_etaBuffer, true);

        foreach (var eta in _etaBuffer)
        {
            Debug.Log($"[Decision ETA] Runner={eta.Runner.name} Target={eta.TargetBase} ETA={eta.Remaining:F2}s");
        }

        List<ThrowStep> throwSteps = new();

        // 外野：Cutoff → Return（現状維持）
        if (catchFielder.Data.PositionGroupType == PositionGroupType.Outfield)
        {
            ThrowStep cutoff = CalculateCutoffThrowPlan(catchFielder, fielders);
            throwSteps.Add(cutoff);

            FielderController pitcher = fielders[PositionType.Pitcher];
            Debug.Assert(pitcher != null, "Pitcher not found.");
            throwSteps.Add(ThrowToFielder(cutoff.ReceiverFielder, pitcher, ThrowPlan.Return));
            return throwSteps;
        }

        // 内野：投げるベースを決める
        throwSteps.Add(DetermineInfieldThrowPlan(
            catchFielder, fielders, baseManager, isFly, situation, _etaBuffer));

        return throwSteps;
    }

    private static ThrowStep DetermineInfieldThrowPlan(
        FielderController catchFielder,
        Dictionary<PositionType, FielderController> fielders,
        BaseManager baseManager,
        bool isFly,
        DefenseSituation situation,
        List<RunnerETA> etas)
    {
        // 内野フライ：投手へ返球（現状維持）
        if (isFly)
        {
            FielderController pitcher = fielders[PositionType.Pitcher];
            return ThrowToFielder(catchFielder, pitcher, ThrowPlan.Return);
        }

        // 2アウト：まず1塁（現状維持）
        if (situation.OutCount == 2)
        {
            Vector3 firstPos = baseManager.GetBasePosition(BaseId.First);
            var rec = FindNearBaseFielder(fielders, catchFielder, firstPos);
            return ThrowToFielder(catchFielder, rec, ThrowPlan.First);
        }

        // ★ここがメイン：優先順位で「アウト取れるベース」を探す
        if (TryDecideThrowBase(catchFielder, fielders, baseManager, etas, out var decidedBase, out var receiver))
        {
            Debug.Log($"[Decision] Throw to {decidedBase} receiver={receiver.name}");
            return ThrowToFielder(catchFielder, receiver, ToPlan(decidedBase));
        }

        // どこも間に合わない → とりあえず1塁
        Vector3 fallbackPos = baseManager.GetBasePosition(BaseId.First);
        var fallback = FindNearBaseFielder(fielders, catchFielder, fallbackPos);
        Debug.Log("[Decision] No base can be out. Fallback to First.");
        return ThrowToFielder(catchFielder, fallback, ThrowPlan.First);
    }

    private static bool TryDecideThrowBase(
        FielderController catchFielder,
        Dictionary<PositionType, FielderController> fielders,
        BaseManager baseManager,
        List<RunnerETA> etas,
        out BaseId decidedBase,
        out FielderController receiver)
    {
        decidedBase = BaseId.First;
        receiver = null;

        for (int i = 0; i < _priority.Length; i++)
        {
            var baseId = _priority[i];

            float runnerEta = GetMinEtaTo(baseId, etas);
            if (runnerEta == float.MaxValue) continue; // その塁に向かう走者がいない

            Vector3 basePos = baseManager.GetBasePosition(baseId);
            var rec = FindNearBaseFielder(fielders, catchFielder, basePos);
            if (rec == null) continue;

            float ballTime = EstimateBallArriveTime(catchFielder.transform.position, rec.transform.position);

            // 最小アウト判定：ボールが先に着くならアウト可能
            if (ballTime < runnerEta)
            {
                decidedBase = baseId;
                receiver = rec;
                Debug.Log($"[Decision] Choose {baseId} (ball={ballTime:F2}s < runner={runnerEta:F2}s) receiver={rec.name}");
                return true;
            }
        }

        return false;
    }

    private static float GetMinEtaTo(BaseId baseId, List<RunnerETA> etas)
    {
        float best = float.MaxValue;
        for (int i = 0; i < etas.Count; i++)
        {
            if (etas[i].TargetBase != baseId) continue;
            if (etas[i].Remaining < best) best = etas[i].Remaining;
        }
        return best;
    }

    private static float EstimateBallArriveTime(Vector3 from, Vector3 to)
    {
        return Vector3.Distance(from, to) / THROW_SPEED;
    }

    private static ThrowPlan ToPlan(BaseId baseId)
    {
        return baseId switch
        {
            BaseId.First => ThrowPlan.First,
            BaseId.Second => ThrowPlan.Second,
            BaseId.Third => ThrowPlan.Third,
            BaseId.Home => ThrowPlan.Home,
            _ => ThrowPlan.First
        };
    }

    private static ThrowStep ThrowToFielder(FielderController thrower, FielderController catcher, ThrowPlan plan)
    {
        return new ThrowStep
        {
            Plan = plan,
            TargetPosition = catcher.transform.position,
            ThrowerFielder = thrower,
            ReceiverFielder = catcher,
            ThrowSpeed = THROW_SPEED,
            ArriveTime = (thrower.transform.position - catcher.transform.position).magnitude / THROW_SPEED
        };
    }

    /// <summary>
    /// 外野手→内野の中継（最寄り内野手）
    /// </summary>
    private static ThrowStep CalculateCutoffThrowPlan(FielderController catchFielder, Dictionary<PositionType, FielderController> fielders)
    {
        FielderController nearestInfielder = null;
        float nearestDistance = float.MaxValue;

        foreach (var key in fielders)
        {
            FielderController fielder = key.Value;
            if (fielder.Data.PositionGroupType != PositionGroupType.Infield) continue;

            float distance = (catchFielder.transform.position - fielder.transform.position).magnitude;
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestInfielder = fielder;
            }
        }

        return new ThrowStep
        {
            Plan = ThrowPlan.Cutoff,
            TargetPosition = nearestInfielder.transform.position,
            ThrowerFielder = catchFielder,
            ReceiverFielder = nearestInfielder,
            ThrowSpeed = THROW_SPEED,
            ArriveTime = nearestDistance / THROW_SPEED
        };
    }

    private static FielderController FindNearBaseFielder(
        Dictionary<PositionType, FielderController> fielders,
        FielderController catcher,
        Vector3 basePos)
    {
        float minDistance = float.MaxValue;
        FielderController nearFielder = null;

        foreach (var key in fielders)
        {
            if (key.Value == catcher) continue;

            FielderController fielder = key.Value;
            float distance = (fielder.transform.position - basePos).magnitude;
            if (distance < minDistance)
            {
                minDistance = distance;
                nearFielder = fielder;
            }
        }

        return nearFielder;
    }
}
