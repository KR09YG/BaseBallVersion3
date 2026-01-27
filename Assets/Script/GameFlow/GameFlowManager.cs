using Cysharp.Threading.Tasks;
using UnityEngine;

public class GameFlowManager : MonoBehaviour
{
    [SerializeField] private MonoBehaviour[] _initializedObjects;
    [SerializeField] private InningSchedule9 _inningSchedule9;
    [SerializeField] private InningManager _inningManager;
    [SerializeField] private int _waitToNextInningMillis = 2000;

    private int _currentInningNumber = 1;

    private void Start()
    {
        AdvanceInningAsync().Forget();
    }

    void OnValidate()
    {
        // IInitializable型でないものがアサインされていたらエラーを出し、nullにする
        for (int i = 0; i < _initializedObjects.Length; i++)
        {
            if (_initializedObjects[i] != null &&
                _initializedObjects[i] is not IInitializable)
            {
                Debug.LogError(
                    $"[{name}] Invalid initializable assigned",
                    _initializedObjects[i]
                );
                _initializedObjects[i] = null;
            }
        }
    }


    private async UniTaskVoid AdvanceInningAsync()
    {
        _currentInningNumber = 1;
        HalfInningPlan halfInningPlan;
        DefenseSituation situation;

        while (_currentInningNumber <= InningSchedule9.Innings)
        {
            // 表の攻撃
            halfInningPlan = _inningSchedule9.Get(_currentInningNumber, true);

            if (halfInningPlan.PlayThisHalf)
            {
                Debug.Log($"=== {_currentInningNumber}回表 ===");
                situation = halfInningPlan.StartDefenseSituation;
                await _inningManager.PlayHalfAndWaitAsync(situation);
            }
            else
            {
                // プレイしないときの得点処理
                Debug.Log("回表はプレイしません");
            }

            await UniTask.Delay(_waitToNextInningMillis);

            // 裏の攻撃
            halfInningPlan = _inningSchedule9.Get(_currentInningNumber, false);

            if (halfInningPlan.PlayThisHalf)
            {
                Debug.Log($"=== {_currentInningNumber}回裏 ===");
                situation = halfInningPlan.StartDefenseSituation;
                await _inningManager.PlayHalfAndWaitAsync(situation);
            }
            else
            {
                // プレイしないときの得点処理
                Debug.Log("回裏はプレイしません");
            }

            await UniTask.Delay(_waitToNextInningMillis);
            _currentInningNumber++;
        }
    }


}
