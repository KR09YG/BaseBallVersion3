using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

/// <summary>
/// ランナーの制御
/// バッターはAnimationEventから走塁開始、既存ランナーは即座に走塁
/// </summary>
public class Runner : MonoBehaviour
{
    [SerializeField] private RunnerData _data;
    [SerializeField] private BaseManager _baseManager;
    [SerializeField] private Animator _animator;
    [SerializeField] private RuntimeAnimatorController _runnerAnimatorController;

    public RunnerData Data => _data;
    public RunnerType Type => _data.Type;
    public float SecondsPerBase => _data.SecondsPerBase;

    public BaseId CurrentBase { get; private set; }
    public BaseId TargetBase { get; private set; }
    public bool IsRunning { get; private set; }

    // バッター専用: AnimationEventで実行される走塁計画
    private BaseId _plannedTarget = BaseId.None;
    private float _plannedDelay = 0f;
    private bool _isHomeRun = false;
    private Action _onCompleted;

    public void SetActive(bool active)
    {
        gameObject.SetActive(active);
    }

    public void SetCurrentBase(Vector3 pos, BaseId baseId)
    {
        CurrentBase = baseId;
        TargetBase = BaseId.None;
        IsRunning = false;
        _animator.SetBool("IsRunning", false);
        transform.position = pos;
    }

    /// <summary>
    /// バッター専用: 走塁計画を設定（AnimationEventで実行される）
    /// </summary>
    public void SetPlannedRun(BaseId target, float delay, bool isHomeRun, Action onCompleted)
    {
        _plannedTarget = target;
        _plannedDelay = delay;
        _isHomeRun = isHomeRun;
        _onCompleted = onCompleted;

        Debug.Log($"[Runner] {Type} 走塁計画セット: → {target} (AnimationEvent待ち)");
    }

    /// <summary>
    /// AnimationEventから呼ばれる（バッティングアニメーションから）
    /// 例: HitアニメーションのフレームXで発火
    /// </summary>
    public void AnimEvent_StartRunning()
    {
        if (_runnerAnimatorController != null)
        {
            _animator.applyRootMotion = false;
            _animator.runtimeAnimatorController = _runnerAnimatorController;
        }
        else
        {
            Debug.LogWarning($"[Runner] {_data.Type} RunnerAnimatorControllerが設定されていません");
        }

        if (_plannedTarget == BaseId.None)
        {
            Debug.LogWarning($"[Runner] {Type} 走塁計画がありません");
            return;
        }

        Debug.Log($"[Runner] {Type} AnimEvent_StartRunning発火 → 走塁開始");

        if (_isHomeRun)
        {
            // ホームラン走塁
            RunHomeRunAsync(_onCompleted).Forget();
        }
        else
        {
            // 通常走塁
            RunNormalAsync(_plannedTarget, _plannedDelay, _onCompleted).Forget();
        }

        // 計画クリア
        _plannedTarget = BaseId.None;
        _plannedDelay = 0f;
        _isHomeRun = false;
    }

    /// <summary>
    /// 既存ランナー用: 即座に走塁開始
    /// StartBase → TargetBase まで順番に走る
    /// </summary>
    public async UniTask RunToBaseSequentiallyAsync(
        BaseId startBase,
        BaseId targetBase,
        CancellationToken ct,
        float startDelay = 0f)
    {
        if (_baseManager == null)
        {
            Debug.LogError($"[{name}] BaseManager is null!");
            return;
        }

        // タッチアップ待機
        if (startDelay > 0f)
        {
            await UniTask.Delay(
                TimeSpan.FromSeconds(startDelay),
                cancellationToken: ct);
        }

        int start = (int)startBase;
        int goal = (int)targetBase;

        if (start >= goal)
        {
            Debug.LogWarning($"[{name}] Invalid base range: {startBase} → {targetBase}");
            return;
        }

        // 順番に走る（例: 1塁 → 3塁 なら 1塁→2塁→3塁）
        for (int i = start; i < goal; i++)
        {
            ct.ThrowIfCancellationRequested();

            BaseId nextBase = (BaseId)(i + 1);
            await RunToNextBaseAsync(nextBase, ct);
        }
    }

    // 通常走塁（AnimationEvent経由、バッター専用）
    private async UniTaskVoid RunNormalAsync(BaseId target, float delay, Action onCompleted)
    {
        try
        {
            await RunToBaseSequentiallyAsync(
                BaseId.None,  // バッターはNoneから開始
                target,
                destroyCancellationToken,
                delay);

            onCompleted?.Invoke();
        }
        catch (OperationCanceledException)
        {
            Debug.Log($"[{name}] 走塁キャンセル");
        }
        finally
        {
            _onCompleted = null;
        }
    }

    // ホームラン走塁（AnimationEvent経由、バッター専用）
    private async UniTaskVoid RunHomeRunAsync(Action onCompleted)
    {
        if (_baseManager == null)
        {
            Debug.LogError($"[{name}] BaseManager is null!");
            onCompleted?.Invoke();
            return;
        }

        try
        {
            // タッチアップ待機
            if (_plannedDelay > 0f)
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(_plannedDelay),
                    cancellationToken: destroyCancellationToken);
            }

            // ホーム → 1塁 → 2塁 → 3塁 → ホーム
            BaseId[] homeRunPath = {
                BaseId.First,
                BaseId.Second,
                BaseId.Third,
                BaseId.Home
            };

            foreach (var nextBase in homeRunPath)
            {
                destroyCancellationToken.ThrowIfCancellationRequested();
                await RunToNextBaseAsync(nextBase, destroyCancellationToken);
            }

            onCompleted?.Invoke();
        }
        catch (OperationCanceledException)
        {
            Debug.Log($"[{name}] ホームラン走塁キャンセル");
        }
        finally
        {
            _onCompleted = null;
        }
    }

    /// <summary>
    /// 現在位置 → nextBase まで走る
    /// </summary>
    private async UniTask RunToNextBaseAsync(BaseId nextBase, CancellationToken ct)
    {
        Vector3 targetPos = _baseManager.GetBasePosition(nextBase);
        targetPos.y = transform.position.y;

        Vector3 startPos = transform.position;
        TargetBase = nextBase;
        IsRunning = true;
        _animator.SetBool("IsRunning", true);

        float totalTime = SecondsPerBase;
        float elapsed = 0f;

        while (elapsed < totalTime)
        {
            ct.ThrowIfCancellationRequested();

            float t = elapsed / totalTime;
            Vector3 newPos = Vector3.Lerp(startPos, targetPos, t);
            newPos.y = startPos.y;
            transform.position = newPos;

            // 目標ベースの方向を向く
            Vector3 direction = targetPos - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }

            elapsed += Time.deltaTime;
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }

        // 最終位置にセット
        transform.position = targetPos;
        CurrentBase = nextBase;
        TargetBase = BaseId.None;
        IsRunning = false;
        _animator.SetBool("IsRunning", false);
    }
}