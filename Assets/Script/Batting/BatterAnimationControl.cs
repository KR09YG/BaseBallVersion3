using RootMotion.FinalIK;
using System.Collections;
using UnityEngine;

public class BatterAnimationControl : MonoBehaviour
{
    [SerializeField] Animator _batterAnim;
    [SerializeField] AimIK _aimIK;
    [SerializeField] CursorController _cursorController;
    [SerializeField] BattingInputManager _battingInputManager;

    private Quaternion _batterStartQuaternion;
    private Vector3 _batterStartPosition;
    [SerializeField] private float _inputCooldown;
    private float _lastInputTime;

    private void Start()
    {
        _batterStartQuaternion = transform.rotation;
        _batterStartPosition = transform.position;
        //_aimIK.enabled = false;
    }
    private void Update()
    {
        if (Input.GetMouseButtonDown(0) && _cursorController.IsCursorInZone && CanInput())
        {
            _batterAnim.SetTrigger("Swing");
            StartCoroutine(PositionReset());
            _lastInputTime = Time.time; // 入力時刻を更新
        }
    }

    private bool CanInput()
    {
        // クールダウン時間を超えているかつ、カーソルがゾーン内にあるかをチェック
        return Time.time - _lastInputTime > _inputCooldown && _cursorController.IsCursorInZone;
    }


    public void AimIKCall()
    {
        _aimIK.enabled = true;
    }

    public void BattingBallCall()
    {
        _battingInputManager.StartInput();
        _aimIK.enabled = false;
    }

    IEnumerator PositionReset()
    {
        yield return new WaitForSeconds(0.8f);
        transform.rotation = _batterStartQuaternion;
        transform.position = _batterStartPosition;
    }
}
