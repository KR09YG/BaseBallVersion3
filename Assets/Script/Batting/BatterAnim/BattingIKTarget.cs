using UnityEngine;

public class BattingIKTarget : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private BattingCursor _cursor;

    [Header("手の位置設定")]
    [Tooltip("右手のオフセット（バット基準）")]
    [SerializeField] private Vector3 _rightHandOffset = new Vector3(0f, -0.3f, 0f);

    [Tooltip("左手のオフセット（バット基準）")]
    [SerializeField] private Vector3 _leftHandOffset = new Vector3(0f, -0.5f, 0f);

    [Header("補間設定")]
    [Tooltip("ターゲット位置への補間速度")]
    [SerializeField] private float _smoothSpeed = 10f;

    [Header("デバッグ")]
    [SerializeField] private bool _enableDebugLogs = true;

    private Transform _rightHandTarget;
    private Transform _leftHandTarget;
    private Vector3 _targetPosition;
    private bool _isInitialized = false;

    public Transform RightHandTarget => _rightHandTarget;
    public Transform LeftHandTarget => _leftHandTarget;
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// 初期化（外部から呼ばれる）
    /// </summary>
    public void Initialize(BattingCursor cursor)
    {
        if (_isInitialized)
        {
            if (_enableDebugLogs)
            {
                Debug.LogWarning("[BattingIKTarget] 既に初期化済みです");
            }
            return;
        }

        _cursor = cursor;

        // Transformを即座に生成
        CreateTargetTransforms();

        // 初期位置を設定
        if (_cursor != null)
        {
            _targetPosition = _cursor.CurrentPos;
            UpdateInitialPositions();
        }

        _isInitialized = true;

        if (_enableDebugLogs)
        {
            Debug.Log("[BattingIKTarget] 初期化完了");
            Debug.Log($"  - Cursor: {_cursor != null}");
            Debug.Log($"  - Right Hand Target: {_rightHandTarget != null} ({_rightHandTarget?.name})");
            Debug.Log($"  - Left Hand Target: {_leftHandTarget != null} ({_leftHandTarget?.name})");
        }
    }

    /// <summary>
    /// ターゲットTransformを生成
    /// </summary>
    private void CreateTargetTransforms()
    {
        // 右手ターゲット
        if (_rightHandTarget == null)
        {
            GameObject rightHandObj = new GameObject("RightHandTarget");
            rightHandObj.transform.SetParent(transform);
            rightHandObj.transform.localPosition = Vector3.zero;
            rightHandObj.transform.localRotation = Quaternion.identity;
            _rightHandTarget = rightHandObj.transform;

            if (_enableDebugLogs)
            {
                Debug.Log($"[BattingIKTarget] RightHandTarget生成: {_rightHandTarget.GetInstanceID()}");
            }
        }

        // 左手ターゲット
        if (_leftHandTarget == null)
        {
            GameObject leftHandObj = new GameObject("LeftHandTarget");
            leftHandObj.transform.SetParent(transform);
            leftHandObj.transform.localPosition = Vector3.zero;
            leftHandObj.transform.localRotation = Quaternion.identity;
            _leftHandTarget = leftHandObj.transform;

            if (_enableDebugLogs)
            {
                Debug.Log($"[BattingIKTarget] LeftHandTarget生成: {_leftHandTarget.GetInstanceID()}");
            }
        }
    }

    /// <summary>
    /// 初期位置を設定
    /// </summary>
    private void UpdateInitialPositions()
    {
        if (_rightHandTarget != null)
        {
            _rightHandTarget.position = _targetPosition + _rightHandOffset;
        }

        if (_leftHandTarget != null)
        {
            _leftHandTarget.position = _targetPosition + _leftHandOffset;
        }
    }

    private void Update()
    {
        if (!_isInitialized || _cursor == null) return;

        _targetPosition = _cursor.CurrentPos;
        UpdateHandTargets();
    }

    /// <summary>
    /// バット位置からターゲット位置を更新
    /// </summary>
    public void UpdateTargetPosition(Vector3 batPosition)
    {
        if (_cursor != null)
        {
            _targetPosition = _cursor.CurrentPos;
        }
    }

    /// <summary>
    /// 両手のターゲット位置を更新
    /// </summary>
    private void UpdateHandTargets()
    {
        if (_rightHandTarget == null || _leftHandTarget == null)
            return;

        // 右手ターゲット
        Vector3 rightTargetPos = _targetPosition + _rightHandOffset;
        _rightHandTarget.position = Vector3.Lerp(
            _rightHandTarget.position,
            rightTargetPos,
            Time.deltaTime * _smoothSpeed
        );

        // 左手ターゲット
        Vector3 leftTargetPos = _targetPosition + _leftHandOffset;
        _leftHandTarget.position = Vector3.Lerp(
            _leftHandTarget.position,
            leftTargetPos,
            Time.deltaTime * _smoothSpeed
        );

        // 回転も設定（バットの向き）
        if (_cursor != null)
        {
            Vector3 batDirection = (_cursor.CurrentPos - transform.position).normalized;

            // ゼロベクトルチェック
            if (batDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(batDirection);

                _rightHandTarget.rotation = Quaternion.Slerp(
                    _rightHandTarget.rotation,
                    targetRotation,
                    Time.deltaTime * _smoothSpeed
                );

                _leftHandTarget.rotation = _rightHandTarget.rotation;
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (_rightHandTarget == null || _leftHandTarget == null) return;

        // ターゲット位置を表示
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(_rightHandTarget.position, 0.03f);
        Gizmos.DrawWireSphere(_leftHandTarget.position, 0.03f);

        // カーソル位置への線
        if (_cursor != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(_rightHandTarget.position, _cursor.CurrentPos);
        }

        // 初期化状態の表示
        if (!_isInitialized)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);
        }
    }
}