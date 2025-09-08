using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattingInputManager : MonoBehaviour
{
    [SerializeField] BattingInitialSpeedCalculator.BatterTypeSettings _currentBatterType;
    [SerializeField] BallControl _ballControl;
    [SerializeField] BattingTimingCalculator _timingCalculator;
    [SerializeField] BattingInitialSpeedCalculator _initialSpeedCalculator;
    [SerializeField] BattingResultDataManager _resultDataManager;
    [SerializeField] AdvancedTrajectoryCalculator _trajectoryCalculator;
    [SerializeField] HomeRunChecker _homerunChecker;
    [SerializeField] BallJudge _ballJudge;

    [SerializeField, Header("�A�j���[�V�������{�[����������^�C�~���O�܂ł����鎞��")] float _swingAnimTime;

    public IEnumerator BallMoveCoroutine { get; private set; }

    public List<Vector3> _trajectoryPoints { get; private set; } = new List<Vector3>();

    Vector3 _landingPoint;

    public bool CanStartInput { get; private set; } = true;

    public Action _startReplay;
    public Action _endReplay;

    [Header("�f�o�b�O�p")]
    public bool InputTime;
    public bool BallPosition;
    public bool BallCorePosition;
    public bool DistanceFromCore;
    public bool TimingAccuracy;
    public bool AccuracyEvaluation;


    private void Awake()
    {
        ServiceLocator.Register(this);
    }

    private void Start()
    {
        _startReplay += () =>
        {
            _ballControl.StopBall();
            BallMoveCoroutine = ServiceLocator.Get<BattingBallMove>().BattingMove(_trajectoryPoints, _landingPoint, true);
            CanStartInput = false;
            StartCoroutine(BallMoveCoroutine);
        };
        _endReplay += () =>
        {
            Debug.Log("���v���C�I��");
            CanStartInput = true;
            ResetData();
        };
    }

    public void StartInput()
    {
        _resultDataManager.InputData = InputData();

        // �f�o�b�O�̕\��
        if (InputTime) Debug.Log(_resultDataManager.InputData.InputTime);
        if (BallPosition) Debug.Log(_resultDataManager.InputData.BallPosition);
        if (BallCorePosition) Debug.Log(_resultDataManager.InputData.AtCorePosition);
        if (DistanceFromCore) Debug.Log(_resultDataManager.InputData.DistanceFromCore);
        if (TimingAccuracy) Debug.Log(_resultDataManager.InputData.TimingAccuracy);
        if (AccuracyEvaluation) Debug.Log(_resultDataManager.InputData.Accuracy);

        // Input���̃f�[�^�����Ƃɏ������v�Z
        _resultDataManager.ResultData = _initialSpeedCalculator.InitialSpeedCalculate(_resultDataManager.InputData);

        if (_resultDataManager.InputData.Accuracy != AccuracyType.Miss) // �{�[�����o�b�g�ɓ��������ꍇ
        {
            // �����̏������~
            _ballControl.StopBall();

            // �e���̌v�Z
            _trajectoryPoints = _trajectoryCalculator.TrajectoryCalculate(
                _resultDataManager.ResultData, _resultDataManager.InputData, out Vector3 landingPoint, out float flightTime);

            if (_homerunChecker.HomeRunCheck(landingPoint)) _resultDataManager.ResultData.HittingType = HitType.HomeRun;

            Debug.Log($"�ŋ��̗����_: {landingPoint}, ��s����: {flightTime}�b");
            Debug.Log($"�ŋ��^�C�v: {_resultDataManager.ResultData.HittingType}");

            // �ŋ��̎�ނɉ���������
            if (_resultDataManager.ResultData.HittingType == HitType.FoulBall) _ballJudge.FoulBall();
            else _ballJudge.Hit();

            // �e���v�Z�̌��ʂ����ƂɎ��ۂɃ{�[���𓮂���
            //BallMoveCoroutine = ServiceLocator.Get<BattingBallMove>().BattingMove(_trajectoryPoints, landingPoint, false);
            //_landingPoint = landingPoint;
            //StartCoroutine(BallMoveCoroutine);

            // �����i�[�̌v�Z���J�n
            //StartCoroutine(ServiceLocator.Get<RunnerCalculation>().RunningCalculate(
            //    6f, 0f, ServiceLocator.Get<BaseManager>().HomeBase, ServiceLocator.Get<BaseManager>().FirstBase));
        }
        else�@ // �{�[�����o�b�g�ɓ�����Ȃ������ꍇ
        {
            ServiceLocator.Get<BallJudge>().SwingStrike();
        }
    }

    /// <summary>
    /// ��x�ŋ��𓮂��������U�肵�Ă�����ɈȑO�̑ŋ����Đ����Ȃ��悤�ɂ��邽�߂̐ݒ�
    /// </summary>
    public void ResetData()
    {
        _resultDataManager.InputData = null;
        _trajectoryPoints.Clear();
        _landingPoint = Vector3.zero;
        _resultDataManager.ResultData = null;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (BallMoveCoroutine != null)
            {
                StopCoroutine(BallMoveCoroutine);
            }
        }
    }

    /// <summary>
    /// BattingInputData���C���v�b�g����
    /// </summary>
    /// <returns>
    /// �ŋ��Ɋւ���f�[�^
    /// </returns>
    private BattingInputData InputData()
    {
        var inputData = new BattingInputData();

        // �^�C�~���O���
        inputData.InputTime = Time.time;

        // �X�C���O�����A�j���[�V�����Ń{�[����������^�C�~���O�܂Ői�߂��{�[���̍��W�Ōv�Z����
        inputData.BallPosition = _ballControl.BezierPoint(_ballControl.MoveBallTime + _swingAnimTime);
        inputData.AtCorePosition = ServiceLocator.Get<CursorController>().CursorPosition.position;
        inputData.DistanceFromCore = Vector3.Distance(inputData.BallPosition, inputData.AtCorePosition);

        // ���x�v�Z
        inputData.TimingAccuracy = _timingCalculator.BattingTimingCalsulate(_ballControl.MoveBallTime, out AccuracyType accuracyType);
        inputData.Accuracy = accuracyType;

        // �v���C���[�ݒ�
        inputData.BatterType = _currentBatterType.Type;

        return inputData;
    }

    private void OnDrawGizmos()
    {
        if (_resultDataManager.ResultData != null)
            Gizmos.DrawLine(Vector3.zero, _resultDataManager.ResultData.ActualDirection);
    }
}