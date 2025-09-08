using UnityEngine;

public enum AccuracyType
{
    Perfect,      // 完璧な当たり
    Good,         // 良い当たり
    Fair,         // 普通の当たり
    Bad,          // 悪い当たり
    Miss,          // ミス（当たらない）
    Default
}

public class BattingTimingCalculator : MonoBehaviour
{
    [SerializeField] BattingData _battingData;

    private const int PERCENTAGE_MULTIPLIER = 100;

    public bool _debugTiming;

    

    /// <summary>
    /// タイミングのジャスト度を計算する
    /// </summary>
    /// <param name="ballProgress">ボールの始点から終点までの到達度（0から1）</param>
    /// <returns>タイミング精度（0～1）</returns>
    public float BattingTimingCalsulate(float ballProgress, out AccuracyType accuracyType)
    {
        // 100%に合わせるためballProgressを100倍する
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
