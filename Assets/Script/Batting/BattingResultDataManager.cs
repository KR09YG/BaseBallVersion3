using System.Collections.Generic;
using System;
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
    public AccuracyType Accuracy;    // Perfect/Good/Fair/Bad/Miss

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
}
public enum HitType
{
    GroundBall,
    LineDrive,
    FoulBall,
    FlyBall,
    PopFly,
    HomeRun
}

public class BattingResultDataManager : MonoBehaviour
{
    public BattingResultData ResultData { get; internal set; }

    public BattingInputData InputData { get; internal set; }


}
