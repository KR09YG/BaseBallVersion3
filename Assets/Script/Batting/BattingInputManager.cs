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

    private BattingResultData _resultData;

    private BattingInputData _inputData;
    
    IEnumerator _tempCoroutine;

    public List<Vector3> _trajectoryPoints { get; private set; } = new List<Vector3>();

    public void StartInput()
    {
        _inputData = InputData();
        if (_inputData.Accuracy != BattingResultCalculator.AccuracyType.Miss)
        {
            SceneSingleton.BallControlInstance.StopBall();
            _trajectoryPoints = SceneSingleton.AdvancedTrajectoryCalculatorInstance.TrajectoryCalculate(_resultData, _inputData, out Vector3 landingPoint, out float flightTime);
            Debug.Log($"�ŋ��̗����_: {landingPoint}, ��s����: {flightTime}�b");
            Debug.Log($"�ŋ��^�C�v: {_resultData.HittingType}");
            if (_resultData.HittingType == BattingResultData.HitType.FoulBall) SceneSingleton.BallJudgeInstance.FoulBall();
            else SceneSingleton.BallJudgeInstance.Hit();
            _tempCoroutine = SceneSingleton.BattingBallMoveInstance.BattingMove(_trajectoryPoints, landingPoint);
            StartCoroutine(_tempCoroutine);
            StartCoroutine(SceneSingleton.RunnerCalculationInstance.RunningCalculate(
                6f, 0f, SceneSingleton.BaseManagerInstance.HomeBase, SceneSingleton.BaseManagerInstance.FirstBase));
        }
        else
        {
            SceneSingleton.BallJudgeInstance.SwingStrike();
            Debug.Log("��U��");
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (_tempCoroutine != null)
            {
                StopCoroutine(_tempCoroutine);
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
        inputData.BallPosition = SceneSingleton.BallControlInstance.transform.position;
        inputData.AtCorePosition = SceneSingleton.CursorControllerInstance.CursorPosition.position;
        inputData.DistanceFromCore = Vector3.Distance(inputData.BallPosition, inputData.AtCorePosition);

        // ���x�v�Z
        inputData.TimingAccuracy = SceneSingleton.BattingResultCalculatorInstance.CalculatePositionBasedTiming(
            SceneSingleton.BallControlInstance.BallPitchProgress, out BattingResultCalculator.AccuracyType accuracyType);
        inputData.Accuracy = accuracyType;
        Debug.Log(inputData.TimingAccuracy);
        Debug.Log(inputData.Accuracy);

        // �v���C���[�ݒ�
        inputData.BatterType = _currentBatterType.Type;

        _resultData = SceneSingleton.BattingResultCalculatorInstance.CalculateBattingResult(inputData);
        return inputData;
    }

    private void OnDrawGizmos()
    {
        if (_resultData != null)
            Gizmos.DrawLine(Vector3.zero, _resultData.ActualDirection);
    }
}