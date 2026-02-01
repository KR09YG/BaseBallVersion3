using UnityEngine;
using RootMotion.FinalIK;

public class BattingIKController : MonoBehaviour
{
    [SerializeField] private BattingCursor _cursor;
    [SerializeField] private FullBodyBipedIK _ik;

    [SerializeField] private Transform _rightHandTarget;
    [SerializeField] private Transform _leftHandTarget;

    [SerializeField] private Vector3 _rightHandOffset = new Vector3(0.1f, 0f, 0f);
    [SerializeField] private Vector3 _leftHandOffset = new Vector3(-0.1f, 0f, 0f);

    [Range(0f, 1f)][SerializeField] private float _rightHandPositionWeight = 0.7f;
    [Range(0f, 1f)][SerializeField] private float _leftHandPositionWeight = 0.25f;
    [Range(0f, 1f)][SerializeField] private float _rotationWeight = 0f;

    [SerializeField] private float _posSmooth = 10f;

    private bool _ikWindowActive = false;
    private bool _disableAfterThisFrame = false;

    private Vector3 _rightVel;
    private Vector3 _leftVel;

    private void Awake()
    {
        SetupTargetsAndSolver();
        ForceDisableIKHard(); // ★重要：起動時にコンポーネントごとOFF
    }

    private void OnEnable()
    {
        ForceDisableIKHard(); // ★重要：有効化された瞬間もOFF
    }

    private void SetupTargetsAndSolver()
    {
        if (_ik == null)
        {
            Debug.LogError("[BattingIK] FullBodyBipedIK is missing.");
            return;
        }
        if (_cursor == null)
        {
            Debug.LogError("[BattingIK] BattingCursor is missing.");
            return;
        }

        if (_rightHandTarget == null)
        {
            var go = new GameObject("RightHandTarget");
            go.transform.SetParent(transform, false);
            _rightHandTarget = go.transform;
        }
        if (_leftHandTarget == null)
        {
            var go = new GameObject("LeftHandTarget");
            go.transform.SetParent(transform, false);
            _leftHandTarget = go.transform;
        }

        _ik.solver.rightHandEffector.target = _rightHandTarget;
        _ik.solver.leftHandEffector.target = _leftHandTarget;

        // 体幹・肩は使わせない
        _ik.solver.bodyEffector.positionWeight = 0f;
        _ik.solver.bodyEffector.rotationWeight = 0f;
        _ik.solver.leftShoulderEffector.positionWeight = 0f;
        _ik.solver.rightShoulderEffector.positionWeight = 0f;

        // 初期位置
        var cpos = _cursor.transform.position;
        _rightHandTarget.position = cpos + _rightHandOffset;
        _leftHandTarget.position = cpos + _leftHandOffset;
    }

    private void LateUpdate()
    {
        if (_ik == null || _cursor == null) return;

        if (_ikWindowActive)
        {
            // ★この瞬間だけIKコンポーネントをON
            if (!_ik.enabled) _ik.enabled = true;

            UpdateTargetsSmooth();
            ApplyWeights(_rightHandPositionWeight, _leftHandPositionWeight, _rotationWeight);

            if (_disableAfterThisFrame)
            {
                // このフレーム使ったら即OFF
                ForceDisableIKHard();
            }
        }
        else
        {
            // 使わない時はコンポーネントOFF（これが一番確実）
            if (_ik.enabled) _ik.enabled = false;
        }
    }

    private void UpdateTargetsSmooth()
    {
        Vector3 cursorPos = _cursor.transform.position;

        Vector3 rightDesired = cursorPos + _rightHandOffset;
        Vector3 leftDesired = cursorPos + _leftHandOffset;

        float smoothTime = 1f / Mathf.Max(0.001f, _posSmooth);

        _rightHandTarget.position = Vector3.SmoothDamp(_rightHandTarget.position, rightDesired, ref _rightVel, smoothTime);
        _leftHandTarget.position = Vector3.SmoothDamp(_leftHandTarget.position, leftDesired, ref _leftVel, smoothTime);
        // 回転追従はしない（ねじれ防止）
    }

    private void ApplyWeights(float rightPosW, float leftPosW, float rotW)
    {
        _ik.solver.rightHandEffector.positionWeight = rightPosW;
        _ik.solver.leftHandEffector.positionWeight = leftPosW;

        _ik.solver.rightHandEffector.rotationWeight = rotW;
        _ik.solver.leftHandEffector.rotationWeight = rotW;
    }

    private void ForceDisableIKHard()
    {
        _ikWindowActive = false;
        _disableAfterThisFrame = false;

        _rightVel = Vector3.zero;
        _leftVel = Vector3.zero;

        if (_ik != null)
        {
            // まずウェイト0
            ApplyWeights(0f, 0f, 0f);
            // ★そしてコンポーネントOFF
            _ik.enabled = false;
        }
    }

    // ===== BattingAnimationControllerから呼ぶAPI =====

    public void OnPreparationStarted() => ForceDisableIKHard();
    public void OnPreparationCompleted() { /* OFFのまま */ }

    // スイング開始〜ImpactまでON
    public void OnSwingStarted()
    {
        _ikWindowActive = true;
        _disableAfterThisFrame = false;
    }

    // Impactのフレームだけ使って、直後OFF
    public void OnImpactTiming()
    {
        _ikWindowActive = true;
        _disableAfterThisFrame = true;
    }

    public void OnImpactCompleted(BattingBallType ballType) => ForceDisableIKHard();
    public void OnAnimationCompleted() => ForceDisableIKHard();
    public void ForceOff() => ForceDisableIKHard();
}
