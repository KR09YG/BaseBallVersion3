using UnityEngine;
using System.Collections.Generic;

public class DefenseManager : MonoBehaviour
{
    [SerializeField] private List<FielderController> _fielders;
    [SerializeField] private BattingResultEvent _battingResultEvent;
    [SerializeField] private BattingBallTrajectoryEvent _battingBallTrajectoryEvent;

    private BattingBallResult _result;
    private List<Vector3> _trajectory;

    private const float DELTA_TIME = 0.01f;

    private void OnEnable()
    {
        _battingResultEvent.RegisterListener(OnBattingResult);
        _battingBallTrajectoryEvent.RegisterListener(OnBattingBallTrajectory);
    }

    private void OnDisable()
    {
        _battingResultEvent.UnregisterListener(OnBattingResult);
        _battingBallTrajectoryEvent.UnregisterListener(OnBattingBallTrajectory);
    }


    private void OnBattingBallTrajectory(List<Vector3> trajectory)
    {
        _trajectory = trajectory;
    }

    private void OnBattingResult(BattingBallResult result)
    {
        _result = result;

        if (_trajectory != null)
        {
            OnBallHit(_trajectory, result);
            _trajectory = null;
        }
    }

    public void OnBallHit(List<Vector3> trajectory, BattingBallResult result)
    {
        CatchPlan catchPlan =
            DefenseCalculator.CalculateCatchPlan(
                trajectory,
                DELTA_TIME,
                _fielders);

        Debug.Log($"CanCatch: {catchPlan.CanCatch}");
        Debug.Log($"Catcher: {catchPlan.Catcher.Data.Position}");
        Debug.Log($"CatchPoint: {catchPlan.CatchPoint}");
        Debug.Log($"CatchTime: {catchPlan.CatchTime}");

        //DefenseRoles roles =
        //    DefenseRolePlanner.CreateRoles(
        //        catchPlan,
        //        _fielders);

        //IssueOrders(catchPlan, roles);
    }

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
