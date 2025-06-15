using RootMotion.FinalIK;
using System.Collections;
using UnityEngine;

public class BatterAnimationControl : MonoBehaviour
{
    [SerializeField] Animator _batterAnim;
    [SerializeField] Batting _batting;
    [SerializeField] AimIK _aimIK;
    [SerializeField] CursorController _cursorController;

    private Quaternion _batterStartQuaternion;
    private Vector3 _batterStartPosition;

    private void Start()
    {
        _batterStartQuaternion = transform.rotation;
        _batterStartPosition = transform.position;
        //_aimIK.enabled = false;
    }
    private void Update()
    {
        if (Input.GetMouseButtonDown(0) && _cursorController.IsCursorInZone)
        {
            _batterAnim.SetTrigger("Swing");
            StartCoroutine(PositionReset());
        }
    }

    public void AimIKCall()
    {
        _aimIK.enabled = true;
    }

    public void BattingBallCall()
    {
        _batting.BattingBall();
        _aimIK.enabled = false;
    }

    IEnumerator PositionReset()
    {
        yield return new WaitForSeconds(1f);
        transform.rotation = _batterStartQuaternion;
        transform.position = _batterStartPosition;
    }
}
