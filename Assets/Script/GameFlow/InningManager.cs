using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

public class InningManager : MonoBehaviour
{
    [SerializeField] private OnBatterReadyForPitchEvent _batterReady;
    [SerializeField] private OnStartPitchEvent _startPitchEvent;
    [SerializeField] private OnDefensePlayJudged _defensePlayJudgedEvent;
    [SerializeField] private OnAtBatResetEvent _atBatResetEvent;

    [SerializeField] private MonoBehaviour[] _initializedObjects;
    private List<IInitializable> _initializables = new List<IInitializable>();
    private UniTaskCompletionSource _playEndTcs;
    private const int DELAY_PITCH = 1000;
    private bool _isPlayEnd;
    private DefenseSituation _currentSituation;
    private int _scoreThisHalfInning;

    private GameFlow _currentFlow = GameFlow.StartIning;
    public GameFlow CurrentFlow => _currentFlow;
    [SerializeField] private Animator Animator;
    [SerializeField] private PlayableDirector _batterAppear;
    [SerializeField] private PlayableDirector _batterRoutine;

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

    private void OnDestroy()
    {
        _batterReady?.UnregisterListener(OnBatterReady);
        _defensePlayJudgedEvent.UnregisterListener(PlayJudge);
    }

    public void ReceivedResult(bool isFinInning, int score)
    {
        if (isFinInning)
        {
            _scoreThisHalfInning = score;
            _playEndTcs.TrySetResult();
        }
        else
        {
            _ = ResetAtBatAsync();
        }
    }

    private async UniTaskVoid ResetAtBatAsync()
    {
        _atBatResetEvent.RaiseEvent();
        _isPlayEnd = false;
        //await UniTask.WaitUntil(() => Input.GetMouseButtonDown(0));
        StartPitchAsync().Forget();

    }

    public void InningInitialized(DefenseSituation situation)
    {
        SetGameFlow(GameFlow.StartIning);
        _scoreThisHalfInning = 0;
        _isPlayEnd = false;
        _batterAppear.Play();
        if (situation == null)
        {
            Debug.LogError("DefenseSituation is null in InningInitialized");
            return;
        }
        foreach (var initializable in _initializables)
        {
            initializable.OnInitialized(situation);
        }
    }

    public async UniTask<int> PlayHalfAndWaitAsync()
    {
        _playEndTcs = new UniTaskCompletionSource();

        // 入力があるまで待機
        await UniTask.WaitUntil(() => Input.GetMouseButtonDown(0));

        _batterAppear.Stop();
        _batterRoutine.Play();

        var completed = await UniTask.WhenAny(_playEndTcs.Task);
        
        return _scoreThisHalfInning;
    }

    public void OnBatterReady()
    {
        SetGameFlow(GameFlow.WaitingForPitch);
        _batterRoutine.Stop();
        StartPitchAsync().Forget();
    }

    public void SetGameFlow(GameFlow newFlow)
    {
        _currentFlow = newFlow;
        Debug.Log("Game flow changed to: " + newFlow);
    }

    private async UniTaskVoid StartPitchAsync()
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


}
