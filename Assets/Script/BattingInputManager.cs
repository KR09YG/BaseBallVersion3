using System;
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
        PopFly,
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
    public BattingCalculation.AccuracyType Accuracy;    // Perfect/Good/Fair/Bad/Miss

    [Header("�ʒu���")]
    public Vector3 BallPosition;         // ���͎��̃{�[���ʒu
    public Vector3 AtCorePosition;      // ���͎��̃o�b�g�ʒu
    public float DistanceFromCore;       // �{�[���ƃo�b�g�̋���

    [Header("�v���C���[�ݒ�")]
    public BattingType BatterType;       // �I�������o�b�^�[�^�C�v
    public Vector3 CursorPosition;       // �J�[�\���ʒu

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
    public float battingPower; //�ŋ��p���[

    public Vector3 initialVelocity; //����
    public float launchAngle; // �ł��グ�p�x
    public float launchDirection; // �ŋ������i�����p�x�j
}

public class BattingInputManager : MonoBehaviour
{
    [SerializeField] private BallControl _ballControl;
    [SerializeField] private CursorController _cursorController;
    [SerializeField] private BattingCalculation _battingCalculation;

    [SerializeField] private float _inputCooldown = 0.1f;

    private float _lastInputTime;

    public event System.Action<BattingInputData> OnBattingInput;

    private void Update()
    {
        if (CanInput())
        {
            var inputData = InputData();
            OnBattingInput?.Invoke(inputData);
            _lastInputTime = Time.time;
        }
    }

    private bool CanInput()
    {
        // �N�[���_�E�����Ԃ𒴂��Ă��邩�A�J�[�\�����]�[�����ɂ��邩���`�F�b�N
        return Time.time - _lastInputTime > _inputCooldown && _cursorController.IsCursorInZone;
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
        inputData.BallPosition = _ballControl.transform.position;
        inputData.AtCorePosition = _cursorController.CursorPosition.position;
        inputData.DistanceFromCore = Vector3.Distance(inputData.BallPosition, inputData.AtCorePosition);

        // ���x�v�Z
        inputData.TimingAccuracy = _battingCalculation.CalculatePositionBasedTiming(
            inputData.BallPosition, inputData.AtCorePosition, _battingCalculation.CurrentType);
        inputData.Accuracy = _battingCalculation.PassResultAccuracy();

        // �v���C���[�ݒ�
        inputData.BatterType = (BattingInputData.BattingType)(int)_battingCalculation.CurrentType;
        inputData.CursorPosition = _cursorController != null ? _cursorController.transform.position : Vector3.zero;

        return inputData;
    }
}