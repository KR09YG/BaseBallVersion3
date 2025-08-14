using UnityEngine;

[CreateAssetMenu]
public class BattingData : ScriptableObject
{
    [Header("��{�v�Z�p�����[�^")]
    public float BasePower;
    public float BaseHeight;

    [Header("�{�[���ƃo�b�g�̈ʒu���ɑ΂���{��")]
    public float BattingXMultiplier = 2f;
    public float BattingYMultiplier = 1f;

    [Header("�ŋ���z�����̐��`�⊮�͈̔�")]
    public float MinBattingZRange;
    public float MaxBattingZRange;

    [Header("�^�C�~���O���Ƃ̕␳�l")]
    public float AccuracyPerfect;
    public float AccuracyGood;
    public float AccuracyFair;
    public float AccuracyBad;
    public float AccuracyMiss;

    [Header("�e�^�C�~���O�̌��ʂ͈̔́i�e�^�C�~���O�̍ŏ��l�j")]
    public float PerfectMin;
    public float GoodMin;
    public float FairMin;
    public float Maxtolerable;

    [Header("�^�C�~���O����̊�l(%)")]
    public float TimingReferenceValue;

    [Header("�^�C�~���O�̃Y���ɑ΂��鋖�e�l(0�`100��)")]
    public float PerfectTimingRange;
    public float GoodTimingRange;
    public float FairTimingRange;
    public float BadTimingRange;

    [Header("�^�C�~���O���x���Ƃ̊�ƂȂ�ŋ��̍���")]
    public float PerfectHeight;
    public float GoodHeight;
    public float FairHeight;
    public float BadHeight;
}
