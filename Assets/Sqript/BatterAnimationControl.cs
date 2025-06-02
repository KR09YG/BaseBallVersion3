using System.Collections;
using UnityEngine;

public class BatterAnimationControl : MonoBehaviour
{
    [SerializeField] Animator _batterAnim;

    [SerializeField] Batting _batting;

    private Quaternion _batterStartQuaternion;
    private Vector3 _batterStartPosition;

    private void Start()
    {
        _batterStartQuaternion = transform.rotation;
        _batterStartPosition = transform.position;
    }
    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            _batterAnim.SetTrigger("Swing");
            StartCoroutine(PositionReset());
        }
    }

    public void BattingBallCall()
    {
        _batting.BattingBall();
    }

    IEnumerator PositionReset()
    {
        yield return new WaitForSeconds(1f);
        transform.rotation = _batterStartQuaternion;
        transform.position = _batterStartPosition;
    }
}
