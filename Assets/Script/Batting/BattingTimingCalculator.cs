using UnityEngine;

public enum AccuracyType
{
    Perfect,      // �����ȓ�����
    Good,         // �ǂ�������
    Fair,         // ���ʂ̓�����
    Bad,          // ����������
    Miss,          // �~�X�i������Ȃ��j
    Default
}

public class BattingTimingCalculator : MonoBehaviour
{
    [SerializeField] BattingData _battingData;

    private const int PERCENTAGE_MULTIPLIER = 100;

    public bool _debugTiming;

    

    /// <summary>
    /// �^�C�~���O�̃W���X�g�x���v�Z����
    /// </summary>
    /// <param name="ballProgress">�{�[���̎n�_����I�_�܂ł̓��B�x�i0����1�j</param>
    /// <returns>�^�C�~���O���x�i0�`1�j</returns>
    public float BattingTimingCalsulate(float ballProgress, out AccuracyType accuracyType)
    {
        // 100%�ɍ��킹�邽��ballProgress��100�{����
        float difference = Mathf.Abs(ballProgress * PERCENTAGE_MULTIPLIER - _battingData.TimingReferenceValue);

        if (difference <= _battingData.PerfectTimingRange)
        {
            accuracyType = AccuracyType.Perfect;
            return Mathf.Lerp(_battingData.PerfectMin, 1f, difference / _battingData.PerfectTimingRange);
        }
        else if (difference <= _battingData.GoodTimingRange)
        {
            accuracyType = AccuracyType.Good;
            return Mathf.Lerp(_battingData.GoodMin, _battingData.PerfectMin, difference / _battingData.GoodTimingRange);
        }
        else if (difference <= _battingData.FairTimingRange)
        {
            accuracyType = AccuracyType.Fair;
            return Mathf.Lerp(_battingData.FairMin, _battingData.GoodMin, difference / _battingData.FairTimingRange);
        }
        else if (difference <= _battingData.BadTimingRange)
        {
            accuracyType = AccuracyType.Bad;
            return Mathf.Lerp(_battingData.Maxtolerable, _battingData.FairMin, difference / _battingData.BadTimingRange);
        }
        else
        {
            accuracyType = AccuracyType.Miss;
            return 0f;
        }
    }

}
