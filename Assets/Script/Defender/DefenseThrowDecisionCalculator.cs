using System.Collections.Generic;
using UnityEngine;

public static class DefenseThrowDecisionCalculator
{
    private static readonly List<RunnerETA> _etaBuffer = new(8);

    // 優先順位（現状維持）
    private static readonly BaseId[] _priority =
    {
        BaseId.Home, BaseId.Third, BaseId.Second, BaseId.First
    };

    /// <summary>
    /// 送球判断
    /// </summary>
    /// <param name="catchFielder">捕球（回収）した野手</param>
    /// <param name="isFly">フライ捕球なら true</param>
    /// <param name="fielders">守備辞書</param>
    /// <param name="baseManager">塁座標</param>
    /// <param name="situation">状況（アウトカウント等）</param>
    /// <param name="runnerManager">RunnerETA取得用</param>
    /// <param name="throwSpeed">送球速度(m/s) ※守備調整値</param>
    /// <param name="catchTime">捕球完了の相対秒（catchPlan.CatchTime 等）</param>
    public static List<ThrowStep> ThrowDicision(
        FielderController catchFielder,
        bool isFly,
        Dictionary<PositionType, FielderController> fielders,
        BaseManager baseManager,
        DefenseSituation situation,
        RunnerManager runnerManager,
        float throwSpeed,
        float catchTime)
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
        if (baseManager == null)
        {
            Debug.LogError("[Decision] baseManager is null");
            return null;
        }

        // ETA取得
        _etaBuffer.Clear();
        runnerManager.GetAllRunningETAs(_etaBuffer, true);

        List<ThrowStep> throwSteps = new();

        // 外野：Cutoff → Pitcher返球（現状維持）
        if (catchFielder.Data.PositionGroupType == PositionGroupType.Outfield)
        {
            ThrowStep cutoff = CalculateCutoffThrowPlan(catchFielder, fielders, throwSpeed, catchTime);
            throwSteps.Add(cutoff);

            if (fielders.TryGetValue(PositionType.Pitcher, out var pitcher) && pitcher != null)
            {
                throwSteps.Add(ThrowToFielder(cutoff.ReceiverFielder, pitcher, ThrowPlan.Return, throwSpeed, cutoff.ArriveTime));
            }
            return throwSteps;
        }

        // 内野：投げるベースを決める
        throwSteps.Add(DetermineInfieldThrowPlan(
            catchFielder, fielders, baseManager, isFly, situation, _etaBuffer, throwSpeed, catchTime));

        return throwSteps;
    }

    private static ThrowStep DetermineInfieldThrowPlan(
        FielderController catchFielder,
        Dictionary<PositionType, FielderController> fielders,
        BaseManager baseManager,
        bool isFly,
        DefenseSituation situation,
        List<RunnerETA> etas,
        float throwSpeed,
        float catchTime)
    {
        // 内野フライ：投手へ返球（現状維持）
        if (isFly)
        {
            if (fielders.TryGetValue(PositionType.Pitcher, out var pitcher) && pitcher != null)
                return ThrowToFielder(catchFielder, pitcher, ThrowPlan.Return, throwSpeed, catchTime);

            return FallbackToFirst(catchFielder, fielders, baseManager, throwSpeed, catchTime);
        }

        // 2アウト：まず1塁（現状維持）
        if (situation != null && situation.OutCount == 2)
        {
            Vector3 firstPos = baseManager.GetBasePosition(BaseId.First);
            var rec = FindNearBaseFielder(fielders, catchFielder, firstPos);
            if (rec != null)
                return ThrowToFielder(catchFielder, rec, ThrowPlan.First, throwSpeed, catchTime);

            return FallbackToFirst(catchFielder, fielders, baseManager, throwSpeed, catchTime);
        }

        // 優先順位で「アウト取れるベース」を探す
        if (TryDecideThrowBase(
            catchFielder, fielders, baseManager, etas, throwSpeed, catchTime,
            out var decidedBase, out var receiver))
        {
            return ThrowToFielder(catchFielder, receiver, ToPlan(decidedBase), throwSpeed, catchTime);
        }

        // どこも間に合わない → とりあえず1塁
        return FallbackToFirst(catchFielder, fielders, baseManager, throwSpeed, catchTime);
    }

    private static ThrowStep FallbackToFirst(
        FielderController catchFielder,
        Dictionary<PositionType, FielderController> fielders,
        BaseManager baseManager,
        float throwSpeed,
        float catchTime)
    {
        Vector3 firstPos = baseManager.GetBasePosition(BaseId.First);
        var fallback = FindNearBaseFielder(fielders, catchFielder, firstPos);
        if (fallback != null)
            return ThrowToFielder(catchFielder, fallback, ThrowPlan.First, throwSpeed, catchTime);

        // 受け手が取れないなら Return 扱いにしておく
        if (fielders.TryGetValue(PositionType.Pitcher, out var pitcher) && pitcher != null)
            return ThrowToFielder(catchFielder, pitcher, ThrowPlan.Return, throwSpeed, catchTime);

        return new ThrowStep
        {
            Plan = ThrowPlan.Return,
            ThrowerFielder = catchFielder,
            ReceiverFielder = catchFielder,
            TargetPosition = catchFielder.transform.position,
            ThrowSpeed = throwSpeed,
            ArriveTime = float.MaxValue
        };
    }

    private static bool TryDecideThrowBase(
        FielderController catchFielder,
        Dictionary<PositionType, FielderController> fielders,
        BaseManager baseManager,
        List<RunnerETA> etas,
        float throwSpeed,
        float catchTime,
        out BaseId decidedBase,
        out FielderController receiver)
    {
        decidedBase = BaseId.First;
        receiver = null;

        for (int i = 0; i < _priority.Length; i++)
        {
            var baseId = _priority[i];

            float runnerEta = GetMinEtaTo(baseId, etas);
            if (runnerEta == float.MaxValue) continue;

            Vector3 basePos = baseManager.GetBasePosition(baseId);
            var rec = FindNearBaseFielder(fielders, catchFielder, basePos);
            if (rec == null) continue;

            float ballArrive = EstimateBallArriveTimeRelative(catchFielder, rec, throwSpeed, catchTime);

            if (ballArrive < runnerEta)
            {
                decidedBase = baseId;
                receiver = rec;
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

    // いまの基準(相対秒)で「何秒後に到達するか」
    private static float EstimateBallArriveTimeRelative(
        FielderController thrower,
        FielderController receiver,
        float throwSpeed,
        float catchTime)
    {
        float delaySec = 0f;
        if (thrower != null && thrower.Data != null)
            delaySec = Mathf.Max(0, thrower.Data.ThrowDelay) / 1000f;

        float dist = Vector3.Distance(thrower.transform.position, receiver.transform.position);
        float flight = dist / Mathf.Max(0.01f, throwSpeed);

        // catchTime は「捕球完了」の相対秒（フライなら捕球、ゴロ回収なら回収）
        // そこから送球モーション(ThrowDelay)→飛行時間
        return Mathf.Max(0f, catchTime) + delaySec + flight;
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

    private static ThrowStep ThrowToFielder(
        FielderController thrower,
        FielderController catcher,
        ThrowPlan plan,
        float throwSpeed,
        float startTime)
    {
        float dist = Vector3.Distance(thrower.transform.position, catcher.transform.position);
        float flight = dist / Mathf.Max(0.01f, throwSpeed);

        float delaySec = 0f;
        if (thrower != null && thrower.Data != null)
            delaySec = Mathf.Max(0, thrower.Data.ThrowDelay) / 1000f;

        return new ThrowStep
        {
            Plan = plan,
            TargetPosition = catcher.transform.position,
            ThrowerFielder = thrower,
            ReceiverFielder = catcher,
            ThrowSpeed = throwSpeed,
            // 「相対秒」で統一
            ArriveTime = Mathf.Max(0f, startTime) + delaySec + flight
        };
    }

    private static ThrowStep CalculateCutoffThrowPlan(
        FielderController catchFielder,
        Dictionary<PositionType, FielderController> fielders,
        float throwSpeed,
        float catchTime)
    {
        FielderController nearestInfielder = null;
        float nearestDistance = float.MaxValue;

        foreach (var key in fielders)
        {
            FielderController fielder = key.Value;
            if (fielder == null) continue;
            if (fielder.Data == null) continue;
            if (fielder.Data.PositionGroupType != PositionGroupType.Infield) continue;

            float distance = Vector3.Distance(catchFielder.transform.position, fielder.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestInfielder = fielder;
            }
        }

        if (nearestInfielder == null)
        {
            // 内野が取れないなら投手へ
            if (fielders.TryGetValue(PositionType.Pitcher, out var pitcher) && pitcher != null)
                nearestInfielder = pitcher;
        }

        return ThrowToFielder(catchFielder, nearestInfielder, ThrowPlan.Cutoff, throwSpeed, catchTime);
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
            var fielder = key.Value;
            if (fielder == null) continue;
            if (fielder == catcher) continue;

            float distance = Vector3.Distance(fielder.transform.position, basePos);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearFielder = fielder;
            }
        }

        return nearFielder;
    }
}
