using UnityEngine;
using System;

public enum PlayOutcomeType
{
    None,
    Ball,           // 見逃しボール
    StrikeLooking,  // 見逃しストライク
    StrikeSwinging, // 空振り
    Foul,           // ファール
    Hit,            // ヒット
    Out,            // アウト 　
    Homerun         // ホームラン
}

[Serializable]
public class Count
{
    public int Balls;
    public int Strikes;
    public int Outs;
    public void Reset()
    {
        Balls = 0;
        Strikes = 0;
    }
}

public class AtBatManager : MonoBehaviour, IInitializable
{
    [SerializeField] private InningManager _inningManager;
    [SerializeField] private BallCountDisplay _ballCountDisplay;
    private Count _currentCount = new Count();
    private PlayOutcomeType _lastPitchOutcome;
    public Count CurrentCount => _currentCount;

    public void OnInitialized(DefenseSituation situation)
    {
        _currentCount.Reset();
        _currentCount.Outs = situation.OutCount;
        _ballCountDisplay.UpdateBallCount(_currentCount);
    }

    public void ReceivedResult(PlayOutcomeType outcome, int score)
    {
        bool isFin = false;
        Debug.Log(outcome);

        if (outcome == PlayOutcomeType.Hit ||
            outcome == PlayOutcomeType.Out ||
            outcome == PlayOutcomeType.Homerun)
        {
            isFin = true;
        }

        if (outcome == PlayOutcomeType.Ball)
        {
            _currentCount.Balls++;
            if (CurrentCount.Balls >= 4)
            {
                _currentCount.Reset();
                Debug.Log("フォアボール");
            }
        }

        if (outcome == PlayOutcomeType.StrikeSwinging || outcome == PlayOutcomeType.StrikeLooking)
        {
            _currentCount.Strikes++;
            if (_currentCount.Strikes >= 3)
            {
                _currentCount.Outs++;
                if (_currentCount.Outs >= 3)
                {
                    isFin = true;
                    Debug.Log("3アウト : チェンジ");
                }
                _currentCount.Reset();
            }
        }

        if (outcome == PlayOutcomeType.Foul && _currentCount.Strikes < 2)
        {
            _currentCount.Strikes++;
        }

        // ここでカウントの表示をしているクラスの更新処理を呼ぶ
        _ballCountDisplay.UpdateBallCount(_currentCount);

        _inningManager.ReceivedResult(isFin, score);
    }
}