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
    [SerializeField] private GameObject _restartButton;
    private bool _isRestartRequested = false;
    private const string WAIT_FOR_CLICK_NEXT = "左クリックで進む";
    private const string WAIT_FOR_CLICK_IN_BATTERBOX = "左クリックで打席へ";

    private int _topScore = 0;
    private int _bottomScore = 0;
    private int _currentInningNumber = 1;

    private void Start()
    {
        _restartButton.SetActive(false);
        AdvanceInningAsync().Forget();
    }

    private async UniTaskVoid AdvanceInningAsync()
    {
        _currentInningNumber = 1;

        while (_currentInningNumber <= InningSchedule9.Innings)
        {
            // 表
            int topRuns = await PlayHalfInningAsync(isTop: true);
            _topScore += topRuns;

            await UniTask.Delay(_waitToNextInningMillis);

            if (_currentInningNumber == 9 && _topScore < _bottomScore)
            {
                // 9回表終了時点で先攻チームが負けている場合、試合終了
                _currentInningNumber++;
                break;
            }

            int bottomRuns = await PlayHalfInningAsync(isTop: false);
            _bottomScore += bottomRuns;

            await UniTask.Delay(_waitToNextInningMillis);

            _currentInningNumber++;
        }

        FinishGameAsync().Forget();
    }

    private async UniTaskVoid FinishGameAsync()
    {
        // 試合終了処理
        TextView.Instance.SetText(TextViewType.Result, $"試合結果 {_topScore} : {_bottomScore}");
        Cursor.visible = true;
        _restartButton.SetActive(true);
        // リスタートが押されるまで待機
        await UniTask.WaitUntil(() => _isRestartRequested);
        Cursor.visible = false;
        // リスタート処理（仮）
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    /// <summary>
    /// リスタートボタンが押されたときの処理
    /// </summary>
    public void OnRestartButton()
    {
        _isRestartRequested = true;
    }

    private void OnInitializedGame()
    {
        // ゲーム初期化時の処理
        Start();
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

            await _broadcastWipe.PlayAsync(() =>
            {
                _scoreboardViewController.ShowScoreboard();
            });
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
