using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using static DefenseManager;

public class GameFlowManager : MonoBehaviour
{
    [SerializeField] private OnBatterReadyForPitchEvent _batterReady;
    [SerializeField] private StartPitchEvent _startPitchEvent;
    [SerializeField] private OnDefensePlayJudged _defensePlayJudgedEvent;
    [SerializeField] private Animator Animator;
    [SerializeField] private MonoBehaviour[] _initializedObjects;
    [SerializeField] private InningSchedule9 _inningSchedule9;
    [SerializeField] private int _waitToNextInningMillis = 2000;
    private List<IInitializable> _initializables = new List<IInitializable>();

    private GameFlow _currentFlow = GameFlow.StartIning;
    private int _currentInningNumber = 1;

    private UniTaskCompletionSource<DefensePlayOutcome> _playEndTcs;
    private bool _isWaitingPlayEnd;


    public GameFlow CurrentFlow => _currentFlow;

    private const int DELAY_PITCH = 1000;

    private void Awake()
    {
        if (_batterReady == null) Debug.LogError("OnBatterReadyForPitchEvent is not assigned in the inspector.");
        else _batterReady.RegisterListener(OnBatterReady);

        if (_startPitchEvent == null) Debug.LogError("StartPitchEvent is not assigned in the inspector.");

        if (_defensePlayJudgedEvent == null) Debug.LogError("OnDefensePlayJudged is not assigned in the inspector.");
        else _defensePlayJudgedEvent.RegisterListener(PlayJudge);

        // 初期化可能なオブジェクトをリストに追加
        foreach (var mono in _initializedObjects)
        {
            if (mono is IInitializable initializable)
            {
                _initializables.Add(initializable);
            }
            else
            {
                Debug.LogWarning($"The object {mono.name} does not implement IInitializable interface.");
            }
        }
    }

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

    private void OnDestroy()
    {
        if (_batterReady != null)
            _batterReady.UnregisterListener(OnBatterReady);

        if (_defensePlayJudgedEvent != null)
            _defensePlayJudgedEvent.UnregisterListener(PlayJudge);
    }

    private async UniTaskVoid AdvanceInningAsync()
    {
        _currentInningNumber = 1;
        while (_currentInningNumber <= InningSchedule9.Innings)
        {
            // 表の攻撃
            if (_inningSchedule9.GetTop(_currentInningNumber - 1).PlayThisHalf)
            {
                Debug.Log($"=== {_currentInningNumber}回表 ===");
                await PlayHalfAndWaitAsync();
            }
            else
            {
                // プレイしないときの得点処理
                Debug.Log("回表はプレイしません");
            }

            await UniTask.Delay(_waitToNextInningMillis);

            // 裏の攻撃
            if (_inningSchedule9.GetBottom(_currentInningNumber - 1).PlayThisHalf)
            {
                Debug.Log($"=== {_currentInningNumber}回裏 ===");
                await PlayHalfAndWaitAsync();
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

    private async UniTask<DefensePlayOutcome> PlayHalfAndWaitAsync()
    {
        // 既に待機中なら作り直さない（保険）
        _playEndTcs = new UniTaskCompletionSource<DefensePlayOutcome>();
        _isWaitingPlayEnd = true;

        SetGameFlow(GameFlow.StartIning);
        Initialized();

        // 入力があるまで待機
        await UniTask.WaitUntil(() => Input.GetMouseButtonDown(0));

        Animator.SetTrigger("InBatterBox");

        var completed = await UniTask.WhenAny(_playEndTcs.Task);

        return default;
    }


    private void Initialized()
    {
        Animator.Play("BatterAppearing");
        foreach (var initializable in _initializables)
        {
            initializable.OnInitialized();
        }
    }

    public void OnBatterReady()
    {
        SetGameFlow(GameFlow.WaitingForPitch);
        StartPitch().Forget();
    }

    public void SetGameFlow(GameFlow newFlow)
    {
        _currentFlow = newFlow;
        Debug.Log("Game flow changed to: " + newFlow);
    }

    private async UniTaskVoid StartPitch()
    {
        await UniTask.Delay(DELAY_PITCH);
        SetGameFlow(GameFlow.Batting);
        _startPitchEvent.RaiseEvent();
    }

    private void PlayJudge(DefensePlayOutcome judged)
    {
        SetGameFlow(GameFlow.Judging);
        Debug.Log($"判定ができたか{judged.HasJudgement}");
        Debug.Log($"アウトかどうか{judged.IsOut}");
        Debug.Log($"判定を行った塁{judged.TargetBase}");

        // 判定待ち中で、かつ判定が存在する場合にのみ完了させる
        if (_isWaitingPlayEnd && judged.HasJudgement && _playEndTcs != null)
        {
            _isWaitingPlayEnd = false;
            _playEndTcs.TrySetResult(judged);
        }
    }
}
