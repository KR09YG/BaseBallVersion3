using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.ShaderGraph.Internal;
using Cinemachine.Utility;
using DG.Tweening;
public class BallControl : MonoBehaviour
{
    [SerializeField, Header("始点")] private Transform _releasePoint;
    [SerializeField, Header("終点")] private Transform _endPos;
    [SerializeField, Header("EndPosのxy座標それぞれの最小（左上）")] private Transform _xYMin;
    [SerializeField, Header("EndPosのxy座標それぞれの最大(右下)")] private Transform _xYMax;
    private Vector3 _startPoint;


    [SerializeField] private MeshRenderer _meetRenderer;

    [SerializeField] private PitchType _pitchType;

    [SerializeField] private Transform _ballDisplayTransform;
    Vector3 _ballDisplaySize;

    [SerializeField] private MeshRenderer _moveBallMesh;
    [SerializeField] private MeshRenderer _pitcherBallMesh;
    public float debugTime;


    public int PichTypeCount { get; private set; }
    /// <summary>
    /// 制御点
    /// </summary>
    private Vector3 _controlPoint1, _controlPoint2;
    /// <summary>
    /// 始点から終点にたどり着くまでの時間
    /// </summary>
    private float _pitchDuration;

    private Vector3[] _gizmosPositions = new Vector3[4];
    public float MoveBallTime { get; private set; }

    public IEnumerator PitchBallMoveCoroutine { get; private set; }

    [SerializeField]
    public enum PitchType
    {
        Fastball,
        Curveball,
        Slider,
        Fork
    }

    private void Awake()
    {
        ServiceLocator.Register(this);
    }

    private void Start()
    {
        PichTypeCount = Enum.GetValues(typeof(PitchType)).Length;
        _ballDisplaySize = _ballDisplayTransform.localScale;
        _ballDisplayTransform.localScale = Vector3.zero;
    }

    public void RePlayPitching()
    {
        PitchBallMoveCoroutine = MoveBall();
        StartCoroutine(PitchBallMoveCoroutine);
    }

    public void Pitching(PitchBallData pitchBall)
    {
        Debug.Log("スタートピッチ");
        ServiceLocator.Get<BallJudge>().IsPitching();

        //　ミートゾーンを見えなくする
        _meetRenderer.enabled = false;
        this.transform.position = _releasePoint.position;
        _startPoint = _releasePoint.position;

        //　_endPosのx,y座標をランダムに決定
        _endPos.position = SetRandomEndPos();

        // ベジェ曲線の制御点と到達時間を設定
        SetupControlPoints(pitchBall);

        //　実際にボールを動かす
        PitchBallMoveCoroutine = MoveBall();
        StartCoroutine(PitchBallMoveCoroutine);
    }

    /// <summary>
    /// 球種によって制御点と到達時間を設定
    /// </summary>
    private void SetupControlPoints(PitchBallData pitchBall)
    {
        _controlPoint1 = Vector3.Lerp(_startPoint, _endPos.position, pitchBall.PathControlRatio1) + pitchBall.ControlPoint;
        _controlPoint2 = Vector3.Lerp(_startPoint, _endPos.position, pitchBall.PathControlRatio2) + pitchBall.ControlPoint2;
        _pitchDuration = pitchBall.Time;
    }

    private Vector3 SetRandomEndPos()
    {
        float x = UnityEngine.Random.Range(_xYMin.position.x, _xYMax.position.x);
        float y = UnityEngine.Random.Range(_xYMin.position.y, _xYMax.position.y);
        return new Vector3(x, y, _endPos.position.z);
    }

    /// <summary>
    /// ベジェ曲線を用いたボールの挙動
    /// </summary>
    private IEnumerator MoveBall()
    {
        _pitcherBallMesh.enabled = false;
        _moveBallMesh.enabled = true;
        _ballDisplayTransform.localScale = _ballDisplaySize;
        MoveBallTime = 0;

        while (MoveBallTime < 1)
        {
            MoveBallTime += Time.deltaTime / _pitchDuration;
            this.transform.position = BezierPoint(MoveBallTime);

            yield return null; // 次のフレームまで待機
        }

        ServiceLocator.Get<BallJudge>().IsPitching();
        ServiceLocator.Get<BallJudge>().StrikeJudge();

        _moveBallMesh.enabled = false;
        _pitcherBallMesh.enabled = true;
        _ballDisplayTransform.localScale = Vector3.zero;
        _startPoint = Vector3.zero;
        _meetRenderer.enabled = true;
    }

    public void StopBall()
    {
        _meetRenderer.enabled = true;
        _ballDisplayTransform.localScale = Vector3.zero;
        StopAllCoroutines();
    }

    /// <summary>
    /// ベジェ曲線の具体的な計算(B(t) = (1-t)³P₀ + 3(1-t)²tP₁ + 3(1-t)t²P₂ + t³P₃)
    /// </summary>
    public Vector3 BezierPoint(float t)
    {
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;

        Vector3 p = uuu * _startPoint; // (1-t)³ * P₀
        p += 3 * uu * t * _controlPoint1; // 3(1-t)² * t * P₁
        p += 3 * u * tt * _controlPoint2; // 3(1-t) * t² * P₂
        p += ttt * _endPos.position; // t³ * P₃

        return p;
    }

    private void OnDrawGizmos()
    {
        // xYMinとxYMaxを対角線とする長方形を描画
        _gizmosPositions[0].Set(_xYMax.position.x, _xYMax.position.y, _endPos.position.z);
        _gizmosPositions[1].Set(_xYMax.position.x, _xYMin.position.y, _endPos.position.z);
        _gizmosPositions[2].Set(_xYMin.position.x, _xYMin.position.y, _endPos.position.z);
        _gizmosPositions[3].Set(_xYMin.position.x, _xYMax.position.y, _endPos.position.z);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(_gizmosPositions[0], _gizmosPositions[1]);
        Gizmos.DrawLine(_gizmosPositions[1], _gizmosPositions[2]);
        Gizmos.DrawLine(_gizmosPositions[2], _gizmosPositions[3]);
        Gizmos.DrawLine(_gizmosPositions[3], _gizmosPositions[0]);

        Gizmos.DrawSphere(BezierPoint(debugTime), 1);
    }
}