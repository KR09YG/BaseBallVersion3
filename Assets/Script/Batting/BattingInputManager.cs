using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// �ŋ��Ɋւ���f�[�^
/// </summary>
[Serializable]
public class TrajectoryData
{
    public Vector3 DropPoint;//�����_
    public float FlightTime;//��s����
    public float MaxHeight;//�ō����B�_�̍���
    public float FlightDistance;//�򋗗�
    public List<Vector3> TrajectoryPoints;//�O���̓_�Q

    public HitType hitType;//�ŋ��̎��
    public bool isHomeRun;

    /// <summary>
    /// �ŋ��̎�ނ�\���񋓌^
    /// </summary>
    public enum HitType
    {
        GroundBall,
        LineDrive,
        FlyBall,
        HomeRun
    }
}

/// <summary>
/// �o�b�e�B���O�̓��̓f�[�^
/// </summary>
[Serializable]
public class BattingInputData
{
    public float InputTime;              // ���͂�������
    public float TimingAccuracy;         // �^�C�~���O�̐��x�i0.0�`1.0�j
    public BattingResultCalculator.AccuracyType Accuracy;    // Perfect/Good/Fair/Bad/Miss

    [Header("�ʒu���")]
    public Vector3 BallPosition;         // ���͎��̃{�[���ʒu
    public Vector3 AtCorePosition;      // ���͎��̃o�b�g�ʒu
    public float DistanceFromCore;       // �{�[���ƃo�b�g�̋���

    [Header("�v���C���[�ݒ�")]
    public BattingType BatterType;       // �I�������o�b�^�[�^�C�v

    public enum BattingType
    {
        Normal, PullHit, OppositeHit, AimHomeRun
    }
}

/// <summary>
/// �Ō����ʂ̃f�[�^
/// </summary>
[Serializable]
public class BattingResultData
{
    /// <summary>
    /// �ŋ��̃p���[
    /// </summary>
    public float BattingPower;
    /// <summary>
    /// �ŋ�����
    /// </summary>
    public Vector3 ActualDirection;
    /// <summary>
    /// �����x�N�g��
    /// </summary> 
    public Vector3 InitialVelocity;
    /// <summary>
    /// �ŋ��p�x�iY�j
    /// </summary>
    public float LaunchAngle;
    /// <summary>
    /// �ŋ������i�����p�x�j
    /// </summary>
    public float LaunchDirection;

    public HitType HittingType; // �ŋ��̎��

    public enum HitType
    {
        GroundBall,
        LineDrive,
        FoulBall,
        FlyBall,
        PopFly,
        HomeRun
    }
}

public class BattingInputManager : MonoBehaviour
{
    [SerializeField] BattingResultCalculator.BatterTypeSettings _currentBatterType;

    [SerializeField] bool _debug;

    private BattingResultData _resultData;

    private BattingInputData _inputData;

    public IEnumerator BallMoveCoroutine { get; private set; }

    public List<Vector3> _trajectoryPoints { get; private set; } = new List<Vector3>();

    Vector3 _landingPoint;

    public void StartInput()
    {
        _inputData = InputData();

        if (_inputData.Accuracy != BattingResultCalculator.AccuracyType.Miss) // �{�[�����o�b�g�ɓ��������ꍇ
        {
            // �����̏������~
            ServiceLocator.BallControlInstance.StopBall();

            // �e���̌v�Z
            _trajectoryPoints = ServiceLocator.AdvancedTrajectoryCalculatorInstance.TrajectoryCalculate(_resultData, _inputData, out Vector3 landingPoint, out float flightTime);

            if (_debug)
            {
                Debug.Log($"�ŋ��̗����_: {landingPoint}, ��s����: {flightTime}�b");
                Debug.Log($"�ŋ��^�C�v: {_resultData.HittingType}");
            }

            // �ŋ��̎�ނɉ���������
            if (_resultData.HittingType == BattingResultData.HitType.FoulBall) ServiceLocator.BallJudgeInstance.FoulBall();
            else ServiceLocator.BallJudgeInstance.Hit();

            // �e���v�Z�̌��ʂ����ƂɎ��ۂɃ{�[���𓮂���
            BallMoveCoroutine = ServiceLocator.BattingBallMoveInstance.BattingMove(_trajectoryPoints, landingPoint, false);
            _landingPoint = landingPoint;
            StartCoroutine(BallMoveCoroutine);

            // �����i�[�̌v�Z���J�n
            StartCoroutine(ServiceLocator.RunnerCalculationInstance.RunningCalculate(
                6f, 0f, ServiceLocator.BaseManagerInstance.HomeBase, ServiceLocator.BaseManagerInstance.FirstBase));
        }
        else�@ // �{�[�����o�b�g�ɓ�����Ȃ������ꍇ
        {
            ServiceLocator.BallJudgeInstance.SwingStrike();
            if (_debug) Debug.Log("��U��");
        }
    }

    public void RePlayHomeRunBall()
    {
        ServiceLocator.BallControlInstance.StopBall();
        BallMoveCoroutine = ServiceLocator.BattingBallMoveInstance.BattingMove(_trajectoryPoints, _landingPoint, true);
        StartCoroutine(BallMoveCoroutine);
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
        inputData.BallPosition = ServiceLocator.BallControlInstance.transform.position;
        inputData.AtCorePosition = ServiceLocator.CursorControllerInstance.CursorPosition.position;
        inputData.DistanceFromCore = Vector3.Distance(inputData.BallPosition, inputData.AtCorePosition);

        // ���x�v�Z
        inputData.TimingAccuracy = ServiceLocator.BattingResultCalculatorInstance.CalculatePositionBasedTiming(
            ServiceLocator.BallControlInstance.BallPitchProgress, out BattingResultCalculator.AccuracyType accuracyType);
        inputData.Accuracy = accuracyType;
        Debug.Log(inputData.TimingAccuracy);
        Debug.Log(inputData.Accuracy);

        // �v���C���[�ݒ�
        inputData.BatterType = _currentBatterType.Type;

        _resultData = ServiceLocator.BattingResultCalculatorInstance.CalculateBattingResult(inputData);
        return inputData;
    }

    private void OnDrawGizmos()
    {
        if (_resultData != null)
            Gizmos.DrawLine(Vector3.zero, _resultData.ActualDirection);
    }
}