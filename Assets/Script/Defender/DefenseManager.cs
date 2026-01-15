using UnityEngine;
using System.Collections.Generic;

public class DefenseManager : MonoBehaviour
{
    [SerializeField] private List<FielderController> _fielders;

    public void OnBallHit(
        List<Vector3> trajectory,
        float totalFlightTime)
    {
        CatchPlan catchPlan =
            DefenseCalculator.CalculateCatchPlan(
                trajectory,
                totalFlightTime,
                _fielders);

        DefenseRoles roles =
            DefenseRolePlanner.CreateRoles(
                catchPlan,
                _fielders);

        IssueOrders(catchPlan, roles);
    }

    private void IssueOrders(
        CatchPlan catchPlan,
        DefenseRoles roles)
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
