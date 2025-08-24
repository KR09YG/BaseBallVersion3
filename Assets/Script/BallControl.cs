using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
public class BallControl : MonoBehaviour
{
    [SerializeField, Header("始点")] private Transform _releasePoint;
    [SerializeField, Header("終点")] private Transform _endPos;
    private Vector3 _startPoint;

    public bool IsMoveBall { get; private set; } = false;

    [SerializeField] private MeshRenderer _meetRenderer;

    [SerializeField] private List<PitchSettings> _controlPointsList;

    [SerializeField] private PitchType _pitchType;

    [SerializeField] private Transform _ballDisplayTransform;
    Vector3 _ballDisplaySize;

    [SerializeField] private MeshRenderer _moveBallMesh;
    [SerializeField] private MeshRenderer _pitcherBallMesh;


    public float BallPitchProgress {get; private set; }
    public int PichTypeCount { get; private set; }
    /// <summary>
    /// 制御点
    /// </summary>
    private Vector3 _controlPoint1, _controlPoint2;
    /// <summary>
    /// 始点から終点にたどり着くまでの時間
    /// </summary>
    private float _pitchDuration;

    public float MoveBallTime { get; private set; }

    public PitchSettings PitchBallType { get; private set; }

    public IEnumerator PitchBallMoveCoroutine { get; private set; }

    [Serializable]
    public class PitchSettings
    {
        public string Name;
        public Vector3 ControlPoint;
        [SerializeField, Range(0f, 1f)] public float PathControlRatio1;
        public Vector3 ControlPoint2;
        [SerializeField, Range(0f, 1f)] public float PathControlRatio2;
        public float Time;
    }
    [SerializeField]
    public enum PitchType
    {
        Fastball,
        Curveball,
        Slider,
        Fork
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

    public void SetBallState(bool isMove)
    {
        IsMoveBall = isMove;
    }

    public void Pitching()
    {
        Debug.Log("スタートピッチ");
        //　ピッチングが始まるタイミングで弾道データなどを初期化
        ServiceLocator.BattingInputManagerInstance.ResetData();
        ServiceLocator.BallJudgeInstance.IsPitching();
        
        //　ミートゾーンを見えなくする
        _meetRenderer.enabled = false;
        this.transform.position = _releasePoint.position;
        _startPoint = _releasePoint.position;
        //　どの球種を投げるのかをランダムに決定
        int random = UnityEngine.Random.Range(0, PichTypeCount);
        //　_endPosのx,y座標をランダムに決定
        float x = UnityEngine.Random.Range(-0.4f, 1.0f);
        float y = UnityEngine.Random.Range(0.8f, 2.2f);
        _endPos.position = new Vector3(x, y, _endPos.position.z);

        _pitchType = (PitchType)random;

        // ベジェ曲線の制御点と到達時間を設定
        SetupControlPoints();

        //　実際にボールを動かす
        PitchBallMoveCoroutine = MoveBall();
        StartCoroutine(PitchBallMoveCoroutine);
    }

    /// <summary>
    /// 球種によって制御点と到達時間を設定
    /// </summary>
    private void SetupControlPoints()
    {
        Debug.Log("軌道をセットアップ");
        switch (_pitchType)
        {
            case PitchType.Fastball:
                Debug.Log("ストレート");
                PitchBallType = _controlPointsList.Find(x => x.Name == "ストレート");
                break;
            case PitchType.Curveball:
                Debug.Log("カーブ");
                PitchBallType = _controlPointsList.Find(x => x.Name == "カーブ");
                break;
            case PitchType.Slider:
                Debug.Log("スライダー");
                PitchBallType = _controlPointsList.Find(x => x.Name == "スライダー");
                break;
            case PitchType.Fork:
                Debug.Log("フォーク");
                PitchBallType = _controlPointsList.Find(x => x.Name == "フォーク");
                break;
            default:
                //ここにない球種が来た場合はストレートとして扱う
                PitchBallType = _controlPointsList.Find(x => x.Name == "ストレート");
                break;
        }

        _controlPoint1 = Vector3.Lerp(_startPoint, _endPos.position, PitchBallType.PathControlRatio1) + PitchBallType.ControlPoint;
        _controlPoint2 = Vector3.Lerp(_startPoint, _endPos.position, PitchBallType.PathControlRatio2) + PitchBallType.ControlPoint2;
        _pitchDuration = PitchBallType.Time;
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

        while (MoveBallTime < 1.0f)
        {
            MoveBallTime += Time.deltaTime / _pitchDuration;
            BallPitchProgress = MoveBallTime;
            this.transform.position = BezierPoint(_startPoint, _controlPoint1, _controlPoint2, _endPos.position, MoveBallTime);

            yield return null; // 次のフレームまで待機
        }

        ServiceLocator.BallJudgeInstance.IsPitching();
        ServiceLocator.BallJudgeInstance.StrikeJudge();

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
    private Vector3 BezierPoint(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;

        Vector3 p = uuu * p0; // (1-t)³ * P₀
        p += 3 * uu * t * p1; // 3(1-t)² * t * P₁
        p += 3 * u * tt * p2; // 3(1-t) * t² * P₂
        p += ttt * p3; // t³ * P₃

        return p;
    }
}