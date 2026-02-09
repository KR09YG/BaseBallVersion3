using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 守備の送球判断（簡潔版）
/// </summary>
public static class DefenseThrowDecisionCalculator
{
    /// <summary>
    /// 最適な送球シーケンスを決定
    /// </summary>
    public static List<ThrowStep> DecideOptimalThrows(
        List<RunnerAction> runnerPlans,
        Dictionary<BaseId, float> throwTimes,
        CatchPlan catchPlan,
        BaseManager baseManager,
        Dictionary<PositionType, FielderController> fieldersByPosition,
        List<BaseCoverAssign> baseCovers,
        float throwSpeed)
    {
        var ctx = new ThrowContext(
            runnerPlans, throwTimes, catchPlan, 
            baseManager, fieldersByPosition, baseCovers, throwSpeed);

        // フライは送球なし
        if (catchPlan.IsFly)
            return new List<ThrowStep> { ctx.CreateReturn() };

        // 候補評価
        var candidates = EvaluateCandidates(ctx);
        if (candidates.Count == 0)
            return new List<ThrowStep> { ctx.CreateReturn() };

        // ゲッツー判定
        var doublePlay = TryDoublePlay(candidates, ctx);
        if (doublePlay != null)
            return doublePlay;

        // 単独アウト
        var best = candidates
            .Where(c => c.IsOut)
            .OrderByDescending(c => c.Priority)
            .FirstOrDefault();

        if (best.TargetBase == BaseId.None)
            return new List<ThrowStep> { ctx.CreateReturn() };

        var step = ctx.CreateThrow(best.TargetBase, best.BallArrival);
        return step != null 
            ? new List<ThrowStep> { step.Value } 
            : new List<ThrowStep> { ctx.CreateReturn() };
    }

    // ========== 候補評価 ==========

    private static List<ThrowCandidate> EvaluateCandidates(ThrowContext ctx)
    {
        var candidates = new List<ThrowCandidate>();

        foreach (var action in ctx.RunnerPlans)
        {
            if (action.TargetBase == BaseId.None) continue;

            float runnerTime = CalculateRunnerTime(action);
            float ballTime = ctx.ThrowTimes.GetValueOrDefault(action.TargetBase, float.MaxValue);

            if (ballTime == float.MaxValue) continue;

            bool isOut = ballTime < runnerTime;
            float margin = runnerTime - ballTime;

            candidates.Add(new ThrowCandidate
            {
                TargetBase = action.TargetBase,
                RunnerType = action.RunnerType,
                BallArrival = ballTime,
                IsOut = isOut,
                Margin = margin,
                Priority = CalculatePriority(action.TargetBase, isOut, margin)
            });
        }

        return candidates;
    }

    private static float CalculatePriority(BaseId baseId, bool isOut, float margin)
    {
        if (!isOut) return -1000f;

        float basePriority = baseId switch
        {
            BaseId.Home => 100f,
            BaseId.Third => 70f,
            BaseId.Second => 40f,
            BaseId.First => 10f,
            _ => 0f
        };

        return basePriority + Mathf.Clamp(margin * 10f, 0f, 30f);
    }

    private static float CalculateRunnerTime(RunnerAction action)
    {
        int bases = (int)action.TargetBase - (action.StartBase == BaseId.None ? 0 : (int)action.StartBase);
        return action.StartDelay + Mathf.Max(0, bases) * 3.8f;
    }

    // ========== ゲッツー判定 ==========

    private static List<ThrowStep> TryDoublePlay(List<ThrowCandidate> candidates, ThrowContext ctx)
    {
        var outs = candidates.Where(c => c.IsOut && c.Margin > 0.2f).ToList();
        if (outs.Count < 2) return null;

        var first = outs.FirstOrDefault(c => c.TargetBase == BaseId.First);
        var second = outs.FirstOrDefault(c => c.TargetBase == BaseId.Second);
        var third = outs.FirstOrDefault(c => c.TargetBase == BaseId.Third);
        var home = outs.FirstOrDefault(c => c.TargetBase == BaseId.Home);

        // パターン1: 1塁 → 2塁
        if (first.TargetBase != BaseId.None && second.TargetBase != BaseId.None)
        {
            var dp = TryDoublePlayPattern(BaseId.First, BaseId.Second, first.BallArrival, ctx);
            if (dp != null) return dp;
        }

        // パターン2: 2塁 → 1塁
        if (second.TargetBase != BaseId.None && first.TargetBase != BaseId.None && 
            second.Margin > first.Margin)
        {
            var dp = TryDoublePlayPattern(BaseId.Second, BaseId.First, second.BallArrival, ctx);
            if (dp != null) return dp;
        }

        // パターン3: ホーム → 1塁
        if (home.TargetBase != BaseId.None && first.TargetBase != BaseId.None)
        {
            var dp = TryDoublePlayPattern(BaseId.Home, BaseId.First, home.BallArrival, ctx);
            if (dp != null) return dp;
        }

        return null;
    }

    private static List<ThrowStep> TryDoublePlayPattern(
        BaseId firstBase, 
        BaseId secondBase, 
        float firstArrival, 
        ThrowContext ctx)
    {
        var step1 = ctx.CreateThrow(firstBase, firstArrival);
        if (!step1.HasValue) return null;

        float relayTime = CalculateRelayTime(firstBase, secondBase, firstArrival, ctx);
        var step2 = ctx.CreateRelayThrow(firstBase, secondBase, relayTime);
        if (!step2.HasValue) return null;

        return new List<ThrowStep> { step1.Value, step2.Value };
    }

    private static float CalculateRelayTime(
        BaseId from, 
        BaseId to, 
        float arrivalAtFrom, 
        ThrowContext ctx)
    {
        Vector3 fromPos = ctx.BaseManager.GetBasePosition(from);
        fromPos.y = 0f;
        Vector3 toPos = ctx.BaseManager.GetBasePosition(to);
        toPos.y = 0f;

        float distance = Vector3.Distance(fromPos, toPos);
        float flightTime = distance / Mathf.Max(0.01f, ctx.ThrowSpeed);
        float relayDelay = from == BaseId.Home ? 0.4f : 0.3f;

        return arrivalAtFrom + relayDelay + flightTime;
    }

    // ========== データ構造 ==========

    private struct ThrowCandidate
    {
        public BaseId TargetBase;
        public RunnerType RunnerType;
        public float BallArrival;
        public bool IsOut;
        public float Margin;
        public float Priority;
    }

    private class ThrowContext
    {
        public List<RunnerAction> RunnerPlans { get; }
        public Dictionary<BaseId, float> ThrowTimes { get; }
        public CatchPlan CatchPlan { get; }
        public BaseManager BaseManager { get; }
        public Dictionary<PositionType, FielderController> FieldersByPosition { get; }
        public List<BaseCoverAssign> BaseCovers { get; }
        public float ThrowSpeed { get; }

        public ThrowContext(
            List<RunnerAction> runnerPlans,
            Dictionary<BaseId, float> throwTimes,
            CatchPlan catchPlan,
            BaseManager baseManager,
            Dictionary<PositionType, FielderController> fieldersByPosition,
            List<BaseCoverAssign> baseCovers,
            float throwSpeed)
        {
            RunnerPlans = runnerPlans;
            ThrowTimes = throwTimes;
            CatchPlan = catchPlan;
            BaseManager = baseManager;
            FieldersByPosition = fieldersByPosition;
            BaseCovers = baseCovers;
            ThrowSpeed = throwSpeed;
        }

        public FielderController GetFielderAt(BaseId baseId)
        {
            BaseCoverAssign cover = BaseCovers.FirstOrDefault(bc => bc.BaseId == baseId);
            if (cover.Fielder != null)
                return cover.Fielder;

            return baseId switch
            {
                BaseId.First => FieldersByPosition.GetValueOrDefault(PositionType.FirstBase),
                BaseId.Second => FieldersByPosition.GetValueOrDefault(PositionType.SecondBase),
                BaseId.Third => FieldersByPosition.GetValueOrDefault(PositionType.ThirdBase),
                BaseId.Home => FieldersByPosition.GetValueOrDefault(PositionType.Catcher),
                _ => null
            };
        }

        public ThrowStep? CreateThrow(BaseId targetBase, float arriveTime)
        {
            var receiver = GetFielderAt(targetBase);
            if (receiver == null) return null;

            return new ThrowStep
            {
                Plan = ConvertToPlan(targetBase),
                TargetPosition = BaseManager.GetBasePosition(targetBase),
                ThrowerFielder = CatchPlan.Catcher,
                ReceiverFielder = receiver,
                ThrowSpeed = ThrowSpeed,
                ArriveTime = arriveTime
            };
        }

        public ThrowStep? CreateRelayThrow(BaseId fromBase, BaseId toBase, float arriveTime)
        {
            var thrower = GetFielderAt(fromBase);
            var receiver = GetFielderAt(toBase);
            if (thrower == null || receiver == null) return null;

            return new ThrowStep
            {
                Plan = ConvertToPlan(toBase),
                TargetPosition = BaseManager.GetBasePosition(toBase),
                ThrowerFielder = thrower,
                ReceiverFielder = receiver,
                ThrowSpeed = ThrowSpeed,
                ArriveTime = arriveTime
            };
        }

        public ThrowStep CreateReturn()
        {
            var pitcher = FieldersByPosition.GetValueOrDefault(PositionType.Pitcher);

            return new ThrowStep
            {
                Plan = ThrowPlan.Return,
                TargetPosition = pitcher?.transform.position ?? Vector3.zero,
                ThrowerFielder = CatchPlan.Catcher,
                ReceiverFielder = pitcher,
                ThrowSpeed = ThrowSpeed * 0.7f,
                ArriveTime = 0f
            };
        }

        private static ThrowPlan ConvertToPlan(BaseId baseId)
        {
            return baseId switch
            {
                BaseId.First => ThrowPlan.First,
                BaseId.Second => ThrowPlan.Second,
                BaseId.Third => ThrowPlan.Third,
                BaseId.Home => ThrowPlan.Home,
                _ => ThrowPlan.Return
            };
        }
    }
}