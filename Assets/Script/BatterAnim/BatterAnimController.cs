//using System.Collections;
//using UnityEngine;

//public class BatterAnimController : MonoBehaviour
//{
//    [SerializeField] private Animator _animator;

//    [Header("アニメーション名")]
//    [SerializeField] private string _hitAnimName = "'Batter_Hit'";
//    [SerializeField] private string _missAnimName = "Batter_MissSwing";
//    [SerializeField] private string _noSwingAnimName = "Swing_NoSwing";
//    [SerializeField] private string _homeRunAnimName = "Swing_HomeRun";

//    [Header("フレーム設定")]
//    [SerializeField] private float _fps = 30f;
//    [SerializeField] private int _windupEndFrame = 23;

//    [Header("入力設定")]
//    [SerializeField] private KeyCode _swingKey = KeyCode.Space;

//    private float SwitchTime => _windupEndFrame / _fps;
//    private bool _isSwinging = false;
//    private bool _isWaitingForSwing = false; // 追加：スイング待ち状態かどうか

//    public bool IsSwinging => _isSwinging;   

//    private void Update()
//    {
//        if (Input.GetKeyDown(_swingKey))
//        {
//            if (!_isWaitingForSwing)
//            {
//                // 最初の入力：バッティング開始
//                StartBatting();
//            }
//            else
//            {
//                // 2回目の入力：スイング実行
//                _isSwinging = true;
//                Debug.Log("スイング入力！");
//            }
//        }
//    }

//    public void StartBatting()
//    {
//        _animator.Play(_hitAnimName);
//        _isSwinging = false; // リセット
//        _isWaitingForSwing = false; // リセット
//        StartCoroutine(WaitForSwing());
//    }

//    private IEnumerator WaitForSwing()
//    {
//        // 23フレーム目まで待つ
//        yield return new WaitForSeconds(SwitchTime);

//        // アニメーションを一時停止
//        _animator.speed = 0f;
//        _isWaitingForSwing = true; // スイング待ち状態に
//        Debug.Log("一時停止 - スイング待ち");

//        // プレイヤーがスイングするまで待つ
//        yield return new WaitUntil(() => IsSwinging);

//        _isWaitingForSwing = false; // スイング待ち解除
//        Debug.Log("スイング入力検知！");

//        // ゲーム判定
//        SwingResult result = JudgeSwing();

//        string targetAnim = GetAnimationName(result);

//        // ★こちらを試してみてください
//        float targetAnimLength = GetAnimationLength(targetAnim);

//        // デバッグ出力
//        Debug.Log($"アニメーション: {targetAnim}, 長さ: {targetAnimLength}秒");

//        float normalizedTime = (_windupEndFrame / _fps) / targetAnimLength;

//        Debug.Log($"normalizedTime計算: ({_windupEndFrame} / {_fps}) / {targetAnimLength} = {normalizedTime}");

//        _animator.Play(targetAnim, 0, normalizedTime);
//        _animator.speed = 1f;

//        // アニメーション切り替えて再開
//        _animator.Play(targetAnim, 0, normalizedTime);
//        _animator.speed = 1f;

//        Debug.Log($"アニメーション再開: {result}");
//    }

//    private SwingResult JudgeSwing()
//    {
//        // ボールとの当たり判定などのロジック
//        return SwingResult.Miss;
//    }

//    private string GetAnimationName(SwingResult result)
//    {
//        return result switch
//        {
//            SwingResult.Hit => _hitAnimName,
//            SwingResult.Miss => _missAnimName,
//            SwingResult.NoSwing => _noSwingAnimName,
//            SwingResult.HomeRun => _homeRunAnimName,
//            _ => _hitAnimName
//        };
//    }

//    private float GetAnimationLength(string animName)
//    {
//        foreach (AnimationClip clip in _animator.runtimeAnimatorController.animationClips)
//        {
//            if (clip.name == animName)
//                return clip.length;
//        }
//        return 1f;
//    }
//}