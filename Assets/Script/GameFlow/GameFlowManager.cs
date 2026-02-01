using Cysharp.Threading.Tasks;
using System;
using UnityEngine;

public class GameFlowManager : MonoBehaviour
{
    [SerializeField] private ScoreboardViewController _scoreboardViewController;
    [SerializeField] private InningSchedule9 _inningSchedule9;
    [SerializeField] private BroadcastWipeRuntimeInCanvas _broadcastWipe;
    [SerializeField] private InningManager _inningManager;
    [SerializeField] private int _waitToNextInningMillis = 2000;

    private const string WAIT_FOR_CLICK_NEXT = "左クリックで進む";
    private const string WAIT_FOR_CLICK_IN_BATTERBOX = "左クリックで打席へ";

    private int _topScore = 0;
    private int _bottomScore = 0;
    private int _currentInningNumber = 1;

    private void Start()
    {
        AdvanceInningAsync().Forget();
    }

    private async UniTaskVoid AdvanceInningAsync()
    {
        _currentInningNumber = 1;
        Action ShowScoreAction = () => _scoreboardViewController.ShowScoreboard();

        while (_currentInningNumber <= InningSchedule9.Innings)
        {
            // 表
            int topRuns = await PlayHalfInningAsync(isTop: true);
            _topScore += topRuns;

            await _broadcastWipe.PlayAsync(ShowScoreAction);

            // 9回裏の不要試合（サヨナラ成立済みで裏不要）
            bool playBottom = !(_currentInningNumber == 9 && _topScore < _bottomScore);

            int bottomRuns = await PlayHalfInningAsync(isTop: false);
            _bottomScore += bottomRuns;

            await UniTask.Delay(_waitToNextInningMillis);

            _currentInningNumber++;
        }
    }

    /// <summary>
    /// 半イニングを1回分実行して、その半イニングの得点を返す
    /// </summary>
    private async UniTask<int> PlayHalfInningAsync(bool isTop, bool? forcePlay = null)
    {
        HalfInningPlan plan = _inningSchedule9.Get(_currentInningNumber, isTop);

        // 呼び出し側で強制的にプレイ/スキップを決めたい場合
        if (forcePlay.HasValue)
        {
            plan.PlayThisHalf = forcePlay.Value;
        }

        string label = isTop ? "表" : "裏";
        int runs;


        if (plan.PlayThisHalf)
        {
            Debug.Log($"=== {_currentInningNumber}回{label} ===");


            await WaitForClickAsync(TextViewType.Infomation, WAIT_FOR_CLICK_NEXT);

            DefenseSituation situation = plan.StartDefenseSituation;

            await _broadcastWipe.PlayAsync(() =>
            {
                _scoreboardViewController.HideScoreboard();
                // テキスト表示(クリックすると非表示→処理はここでは待たない）
                _ = WaitForClickAsync(TextViewType.Infomation, WAIT_FOR_CLICK_IN_BATTERBOX);
                _inningManager.InningInitialized(situation);
            });

            // 半イニングをプレイして得点を受け取る
            runs = await _inningManager.PlayHalfAndWaitAsync();

        }
        else
        {
            runs = plan.SkipRule.BaseRuns;
        }

        // その半イニングの得点を反映して表示
        _scoreboardViewController.UpdateScore(isTop, _currentInningNumber, runs);

        return runs;
    }

    private async UniTask WaitForClickAsync(TextViewType type, string s)
    {
        TextView.Instance.SetText(type, s);
        await UniTask.WaitUntil(() => Input.GetMouseButtonDown(0));
        TextView.Instance.Hide(type);
    }
}
