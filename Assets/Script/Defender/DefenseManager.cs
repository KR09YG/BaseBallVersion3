using UnityEngine;
using System.Collections.Generic;

public class DefenseManager : MonoBehaviour
{
    [SerializeField] private List<FielderController> _fielders;
    [SerializeField] private BattingResultEvent _battingResultEvent;
    [SerializeField] private BattingBallTrajectoryEvent _battingBallTrajectoryEvent;
    [SerializeField] private DefenderCatchEvent _defenderCatchEvent;

    private BattingBallResult _result;
    private List<Vector3> _trajectory;

    private const float DELTA_TIME = 0.01f;

    private void OnEnable()
    {
        _battingResultEvent.RegisterListener(OnBattingResult);
        _battingBallTrajectoryEvent.RegisterListener(OnBattingBallTrajectory);
        _defenderCatchEvent.RegisterListener(OnDefenderCatchEvent);
    }

    private void OnDisable()
    {
        _battingResultEvent.UnregisterListener(OnBattingResult);
        _battingBallTrajectoryEvent.UnregisterListener(OnBattingBallTrajectory);
        _defenderCatchEvent.UnregisterListener(OnDefenderCatchEvent);
    }

    /// <summary>
    /// 打球の軌道情報を受け取る
    /// </summary>
    private void OnBattingBallTrajectory(List<Vector3> trajectory)
    {
        _trajectory = trajectory;
    }

    /// <summary>
    /// 打球の結果情報を受け取る
    /// </summary>
    private void OnBattingResult(BattingBallResult result)
    {
        _result = result;

        if (_trajectory != null)
        {
            OnBallHit(_trajectory, result);
            _trajectory = null;
        }
    }

    /// <summary>
    /// 守備側がキャッチした際の処理
    /// </summary>
    public void OnDefenderCatchEvent(FielderController catchDefender,bool isFly)
    {
        if (isFly) Debug.Log("Fly Ball Caught");
        BaseType baseType = DefenseThrowDecisionCalculator.ThrowDicision();
        catchDefender.ThrowBall(baseType);
    }

    /// <summary>
    /// 打球がヒットした際の処理
    /// </summary>
    public void OnBallHit(List<Vector3> trajectory, BattingBallResult result)
    {
        if (result.IsFoul)
        {
            Debug.Log("Foul Ball - No Defense Action");
            return;
        }

        CatchPlan catchPlan =
            DefenseCalculator.CalculateCatchPlan(
                trajectory,
                result,
                DELTA_TIME,
                _fielders);

        Debug.Log($"CanCatch: {catchPlan.CanCatch}");
        Debug.Log($"Catcher: {catchPlan.Catcher.Data.Position}");
        Debug.Log($"CatchPoint: {catchPlan.CatchPoint}");
        Debug.Log($"CatchTime: {catchPlan.CatchTime}");

        FielderController catcheFielder = catchPlan.Catcher;
        catcheFielder.MoveTo(catchPlan.CatchPoint,catchPlan.CatchTime);

        //DefenseRoles roles =
        //    DefenseRolePlanner.CreateRoles(
        //        catchPlan,
        //        _fielders);

        //IssueOrders(catchPlan, roles);
    }

    /// <summary>
    /// 守備指示を出す
    /// </summary>
    private void IssueOrders(CatchPlan catchPlan, DefenseRoles roles)
    {
        if (catchPlan.CanCatch)
        {
            catchPlan.Catcher
                .MoveTo(catchPlan.CatchPoint, catchPlan.CatchTime);
        }

        if (roles.CutoffMan != null)
        {
            roles.CutoffMan.MoveToCutoff();
        }
    }
}
