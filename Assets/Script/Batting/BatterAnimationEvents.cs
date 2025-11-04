using RootMotion.FinalIK;
using System.Collections;
using UnityEngine;

public class BatterAnimationEvents : MonoBehaviour
{
    [SerializeField] Animator _batterAnim;
    [SerializeField] AimIK _aimIK;

    private Quaternion _batterStartQuaternion;
    private Vector3 _batterStartPosition;
    [SerializeField] private float _inputCooldown;
    private float _lastInputTime;
    private bool _canSwing = true;
    public event System.Action OnMissSwing;
    public event System.Action OnHitSwing;
    public event System.Action OnHomeRun;

    [SerializeField] private Rigidbody _batRb;
    [SerializeField] private Transform _batter;
    [SerializeField] private float _batFlipPower;

    [SerializeField] private RePlayManager _rePlayManager;
    [SerializeField] private BallControl _ballControl;
    [SerializeField] private GameStateManager _gameStateManager;
    [SerializeField] private BattingInputManager _battingInputManager;
    [SerializeField] private CursorController _cursorController;
    [SerializeField] private BattingBallMove _ballMove;
    [SerializeField] private BattingResultDataManager _ballResultDataManager;

    private void Start()
    {
        _batterStartQuaternion = transform.rotation;
        _batterStartPosition = transform.position;

        if (_cursorController) Debug.Log("CursorController found");
        else Debug.Log("CursorController not found");
        //_aimIK.enabled = false;
    }

    private void Update()
    {
        Quaternion currentRotation = transform.rotation;
        
        transform.position += _batterAnim.deltaPosition;

        transform.rotation = currentRotation;
    }

    public void LookAtFirstBase()
    {
        Debug.Log("Look at first base");
    }

    public void StartAnim(string trigger)
    {
        _batterAnim.Play(trigger);
    }

    private bool CanInput()
    {
        // クールダウン時間を超えているかつ、カーソルがゾーン内にあるかをチェック
        return Time.time - _lastInputTime > _inputCooldown &&
            _cursorController.IsCursorInZone &&
            _gameStateManager.CurrentState == GameState.Batting;
    }

    public void BatFlip()
    {
        _batRb.transform.SetParent(null);
        _batRb.useGravity = true;
        _batRb.AddForce((_batter.right + _batter.up).normalized * _batFlipPower, ForceMode.Impulse);
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
        _battingInputManager.StartInput();
        if (_ballResultDataManager.InputData.Accuracy != AccuracyType.Miss)
        {
            _ballControl.StopBall();
            OnHomeRun?.Invoke();
            StartCoroutine(_ballMove.BattingMove(_ballResultDataManager.TrajectoryData.TrajectoryPoints,
                _ballResultDataManager.TrajectoryData.LandingPosition, false));
        }
        else
        {
            OnMissSwing?.Invoke();
        }
            AimIKReCall();
    }

    public IEnumerator PositionReset()
    {
        yield return new WaitForEndOfFrame();
        transform.rotation = _batterStartQuaternion;
        transform.position = _batterStartPosition;
    }
}
