using UnityEngine;
using RootMotion.FinalIK;

/// <summary>
/// バッティングIK制御（遅延IK有効化版）
/// </summary>
public class BattingIKController : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private Transform _bat;
    [SerializeField] private BattingCursor _cursor;
    [SerializeField] private FullBodyBipedIK _ik;

    [Header("IKターゲット")]
    [Tooltip("右手のターゲット")]
    [SerializeField] private Transform _rightHandTarget;

    [Tooltip("左手のターゲット")]
    [SerializeField] private Transform _leftHandTarget;

    [Header("オフセット設定")]
    [Tooltip("右手のオフセット（カーソルからの相対位置）")]
    [SerializeField] private Vector3 _rightHandOffset = new Vector3(0.1f, 0f, 0f);

    [Tooltip("左手のオフセット（カーソルからの相対位置）")]
    [SerializeField] private Vector3 _leftHandOffset = new Vector3(-0.1f, 0f, 0f);

    [Header("IK有効化タイミング")]
    [Tooltip("スイング開始からIK有効化までの遅延時間（秒）")]
    [SerializeField] private float _ikActivationDelay = 0.2f;

    [Header("IKウェイト設定")]
    [Tooltip("スイング開始時のIKウェイト")]
    [Range(0f, 1f)]
    [SerializeField] private float _swingIKWeight = 0.8f;

    [Tooltip("インパクト時のIKウェイト")]
    [Range(0f, 1f)]
    [SerializeField] private float _impactIKWeight = 1.0f;

    [Tooltip("スイング時のIK立ち上がり速度")]
    [SerializeField] private float _swingIKRiseSpeed = 15f;

    [Tooltip("通常のウェイト変化速度")]
    [SerializeField] private float _weightChangeSpeed = 5f;

    [Header("フェードアウト設定")]
    [Tooltip("インパクト後のフェードアウト速度")]
    [SerializeField] private float _fadeOutSpeed = 10f;

    [Tooltip("インパクト後のフェードアウト開始遅延（秒）")]
    [SerializeField] private float _fadeOutDelay = 0.05f;

    [Header("デバッグ")]
    [SerializeField] private bool _enableDebugLogs = true;
    [SerializeField] private bool _showGizmos = true;

    private bool _isIKActive = false;
    private float _currentIKWeight = 0f;
    private float _targetIKWeight = 0f;
    private bool _isSwinging = false;
    private bool _isFadingOut = false;
    private float _fadeOutTimer = 0f;

    // ✅ IK有効化タイマー
    private bool _isWaitingForIKActivation = false;
    private float _ikActivationTimer = 0f;

    private void Awake()
    {
        if (_enableDebugLogs)
        {
            Debug.Log("[BattingIK] Awake開始");
        }

        SetupIKTargets();
        DisableIKImmediate();

        if (_enableDebugLogs)
        {
            Debug.Log("[BattingIK] Awake完了 - IK初期無効化");
        }
    }

    private void SetupIKTargets()
    {
        if (_ik == null)
        {
            Debug.LogError("[BattingIK] FullBodyBipedIKが設定されていません");
            return;
        }

        if (_cursor == null)
        {
            Debug.LogError("[BattingIK] BattingCursorが設定されていません");
            return;
        }

        // 右手ターゲット作成
        if (_rightHandTarget == null)
        {
            GameObject rightHandObj = new GameObject("RightHandTarget");
            rightHandObj.transform.SetParent(transform);
            _rightHandTarget = rightHandObj.transform;
        }

        // 左手ターゲット作成
        if (_leftHandTarget == null)
        {
            GameObject leftHandObj = new GameObject("LeftHandTarget");
            leftHandObj.transform.SetParent(transform);
            _leftHandTarget = leftHandObj.transform;
        }

        // IKにターゲットを設定
        _ik.solver.rightHandEffector.target = _rightHandTarget;
        _ik.solver.leftHandEffector.target = _leftHandTarget;

        if (_enableDebugLogs)
        {
            Debug.Log("[BattingIK] IKターゲットセットアップ完了");
        }
    }

    private void Update()
    {
        // ✅ IK有効化の遅延処理
        if (_isWaitingForIKActivation)
        {
            _ikActivationTimer += Time.deltaTime;

            if (_ikActivationTimer >= _ikActivationDelay)
            {
                // 遅延時間経過 → IK有効化
                if (_enableDebugLogs)
                {
                    Debug.Log($"[BattingIK] 遅延終了（{_ikActivationDelay}秒） → IK有効化開始");
                }

                _isWaitingForIKActivation = false;
                _isIKActive = true;
                _targetIKWeight = _swingIKWeight;
            }
        }
    }

    private void LateUpdate()
    {
        if (_ik == null) return;

        if (_isIKActive)
        {
            // IKターゲット位置をカーソル位置に更新
            UpdateIKTargetPositions();

            // フェードアウト処理
            if (_isFadingOut)
            {
                _fadeOutTimer += Time.deltaTime;

                if (_fadeOutTimer >= _fadeOutDelay)
                {
                    _targetIKWeight = 0f;
                    _currentIKWeight = Mathf.Lerp(_currentIKWeight, _targetIKWeight, Time.deltaTime * _fadeOutSpeed);

                    if (_currentIKWeight < 0.01f)
                    {
                        if (_enableDebugLogs)
                        {
                            Debug.Log("[BattingIK] フェードアウト完了 → IK無効化");
                        }
                        DisableIKImmediate();
                    }
                }
            }
            else
            {
                // 通常のウェイト補間
                float speed = _isSwinging ? _swingIKRiseSpeed : _weightChangeSpeed;
                _currentIKWeight = Mathf.Lerp(_currentIKWeight, _targetIKWeight, Time.deltaTime * speed);
            }

            ApplyIKWeight(_currentIKWeight);
        }
        else
        {
            // IK無効時は強制的にウェイト0
            _currentIKWeight = 0f;
            ApplyIKWeight(0f);
        }
    }

    private void UpdateIKTargetPositions()
    {
        if (_cursor == null || _rightHandTarget == null || _leftHandTarget == null)
            return;

        Vector3 cursorPosition = _cursor.transform.position;

        _rightHandTarget.position = cursorPosition + _rightHandOffset;
        _leftHandTarget.position = cursorPosition + _leftHandOffset;

        _rightHandTarget.rotation = _cursor.transform.rotation;
        _leftHandTarget.rotation = _cursor.transform.rotation;
    }

    private void ApplyIKWeight(float weight)
    {
        if (_ik == null) return;

        // 手のIKのみ使用
        _ik.solver.rightHandEffector.positionWeight = weight;
        _ik.solver.leftHandEffector.positionWeight = weight;
        _ik.solver.rightHandEffector.rotationWeight = weight * 0.7f;
        _ik.solver.leftHandEffector.rotationWeight = weight * 0.7f;

        // Body/Spineは常に無効
        _ik.solver.bodyEffector.positionWeight = 0f;
        _ik.solver.bodyEffector.rotationWeight = 0f;

        // 脚のIKも無効
        _ik.solver.leftFootEffector.positionWeight = 0f;
        _ik.solver.rightFootEffector.positionWeight = 0f;
    }

    private void DisableIKImmediate()
    {
        _isIKActive = false;
        _currentIKWeight = 0f;
        _targetIKWeight = 0f;
        _isSwinging = false;
        _isFadingOut = false;
        _fadeOutTimer = 0f;
        _isWaitingForIKActivation = false;  // ✅ 待機フラグもリセット
        _ikActivationTimer = 0f;

        if (_ik != null)
        {
            ApplyIKWeight(0f);
        }
    }

    public void OnPreparationStarted()
    {
        if (_enableDebugLogs)
        {
            Debug.Log("[BattingIK] 準備開始 - IK完全無効");
        }

        DisableIKImmediate();
    }

    public void OnPreparationCompleted()
    {
        if (_enableDebugLogs)
        {
            Debug.Log("[BattingIK] 準備完了 - IK無効のまま");
        }
    }

    /// <summary>
    /// スイング開始時（BattingAnimationControllerから呼ばれる）
    /// ✅ すぐにIKをオンにせず、遅延タイマー開始
    /// </summary>
    public void OnSwingStarted()
    {
        if (_enableDebugLogs)
        {
            Debug.Log($"[BattingIK] スイング開始 → {_ikActivationDelay}秒後にIK有効化");
        }

        _isSwinging = true;
        _isFadingOut = false;
        _fadeOutTimer = 0f;

        // ✅ IK有効化の遅延タイマー開始
        _isWaitingForIKActivation = true;
        _ikActivationTimer = 0f;
        _isIKActive = false;  // まだIKは無効
        _currentIKWeight = 0f;
        _targetIKWeight = 0f;
    }

    public void OnImpactTiming()
    {
        if (_enableDebugLogs)
        {
            Debug.Log($"[BattingIK] インパクト - IK完全制御: {_impactIKWeight}");
        }

        // ✅ まだIKが有効化されていない場合は即座に有効化
        if (!_isIKActive)
        {
            if (_enableDebugLogs)
            {
                Debug.Log("[BattingIK] インパクト時に強制IK有効化");
            }
            _isWaitingForIKActivation = false;
            _isIKActive = true;
        }

        _targetIKWeight = _impactIKWeight;
    }

    public void OnImpactCompleted(BattingBallType ballType)
    {
        if (_enableDebugLogs)
        {
            Debug.Log($"[BattingIK] インパクト完了 - 結果: {ballType} → フェードアウト開始");
        }

        _isSwinging = false;
        _isFadingOut = true;
        _fadeOutTimer = 0f;
        _isWaitingForIKActivation = false;  // ✅ 待機中なら中断
    }

    public void OnAnimationCompleted()
    {
        if (_enableDebugLogs)
        {
            Debug.Log("[BattingIK] アニメーション完了 - IK無効化");
        }

        DisableIKImmediate();
    }

    private void OnDrawGizmos()
    {
        if (!_showGizmos) return;

        // カーソル位置
        if (_cursor != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(_cursor.transform.position, 0.1f);
        }

        // IKターゲット位置（右手）
        if (_rightHandTarget != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(_rightHandTarget.position, 0.05f);

            if (_cursor != null)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(_cursor.transform.position, _rightHandTarget.position);
            }
        }

        // IKターゲット位置（左手）
        if (_leftHandTarget != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(_leftHandTarget.position, 0.05f);

            if (_cursor != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(_cursor.transform.position, _leftHandTarget.position);
            }
        }

        // バット先端の表示
        if (_bat != null)
        {
            if (_isIKActive)
            {
                Gizmos.color = Color.Lerp(Color.yellow, Color.green, _currentIKWeight);
            }
            else if (_isWaitingForIKActivation)
            {
                // ✅ 待機中は点滅
                Gizmos.color = Color.Lerp(Color.red, Color.yellow, Mathf.PingPong(Time.time * 3f, 1f));
            }
            else
            {
                Gizmos.color = Color.red;
            }
            Gizmos.DrawSphere(_bat.position, 0.08f);

            // カーソルへの線
            if (_cursor != null && _isIKActive)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(_bat.position, _cursor.transform.position);
            }

            // 状態マーカー
            if (_isWaitingForIKActivation)
            {
                // ✅ IK有効化待機中（オレンジ）
                Gizmos.color = Color.Lerp(Color.red, Color.yellow, 0.5f);
                Gizmos.DrawWireCube(_bat.position + Vector3.up * 0.3f, Vector3.one * 0.2f);

                // 進行状況を表示
                float progress = _ikActivationTimer / _ikActivationDelay;
                Gizmos.DrawWireCube(_bat.position + Vector3.up * (0.3f + progress * 0.2f), Vector3.one * 0.1f);
            }
            else if (_isFadingOut)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireCube(_bat.position + Vector3.up * 0.3f, Vector3.one * 0.15f);
            }
            else if (_isSwinging)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(_bat.position + Vector3.up * 0.3f, Vector3.one * 0.2f);
            }
        }
    }
}