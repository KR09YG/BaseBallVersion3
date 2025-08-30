using RootMotion.FinalIK;
using System.Collections;
using UnityEngine;

public class BatterAnimationControl : MonoBehaviour
{
    [SerializeField] Animator _batterAnim;
    [SerializeField] AimIK _aimIK;

    private Quaternion _batterStartQuaternion;
    private Vector3 _batterStartPosition;
    [SerializeField] private float _inputCooldown;
    private float _lastInputTime;
    private bool _canSwing = true;

    private void Start()
    {
        ServiceLocator.Register(this);
        _batterStartQuaternion = transform.rotation;
        _batterStartPosition = transform.position;
        //_aimIK.enabled = false;
    }
    private void Update()
    {
        if (!ServiceLocator.Get<BattingInputManager>().CanStartInput) return;

        if (Input.GetMouseButtonDown(0) && CanInput())
        {
            _canSwing = CanInput();
            if (_canSwing)
            {
                StartAnim("Swing");
                ServiceLocator.Get<RePlayManager>().ClickTimeSet(ServiceLocator.Get<BallControl>().MoveBallTime);
                StartCoroutine(PositionReset());
                _lastInputTime = Time.time; // 入力時刻を更新
            }
        }
    }

    public void StartAnim(string animName)
    {
        _batterAnim.Play(animName);
    }

    private bool CanInput()
    {
        // クールダウン時間を超えているかつ、カーソルがゾーン内にあるかをチェック
        return Time.time - _lastInputTime > _inputCooldown && 
            ServiceLocator.Get<CursorController>().IsCursorInZone && 
            !ServiceLocator.Get<BallControl>().IsMoveBall;
    }


    public void AimIKCall()
    {
        _aimIK.enabled = true;
    }

    public void AimIKReCall()
    {
        _aimIK.enabled = false;
    }

    public void BattingBallCall()
    {
        ServiceLocator.Get<BattingInputManager>().StartInput();
        AimIKReCall();
    }

    public IEnumerator PositionReset()
    {
        yield return new WaitForSeconds(0.8f);
        transform.rotation = _batterStartQuaternion;
        transform.position = _batterStartPosition;
    }
}
