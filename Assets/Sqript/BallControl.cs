using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
public class BallControl : MonoBehaviour
{
    [SerializeField, Header("始点")] private Transform _releasePoint;
    [SerializeField, Header("終点")] private Transform _endPos;
    private Vector3 _startPoint;

    [SerializeField] private MeshRenderer _meetRenderer;

    [SerializeField] private List<PitchSettings> _controlPointsList;

    [SerializeField] private PitchType _pitchType;

    [SerializeField] private MeshRenderer _moveBallMesh;
    [SerializeField] private MeshRenderer _pitcherBallMesh;

    [SerializeField] private Rigidbody _rb;

    [SerializeField] private BallJudge _bj;
    public int PichTypeCount { get; private set; }
    /// <summary>
    /// 制御点
    /// </summary>
    private Vector3 _controlPoint1, _controlPoint2;
    /// <summary>
    /// 始点から終点にたどり着くまでの時間
    /// </summary>
    private float _pitchDuration;

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
    }

    public void Pitching()
    {
        Debug.Log("スタートピッチ");
        _bj.IsPitching();
        _meetRenderer.enabled = false;
        this.transform.position = _releasePoint.position;
        _startPoint = _releasePoint.position;
        //度の球種を投げるのかをランダムに決定
        int random = UnityEngine.Random.Range(0, PichTypeCount);
        //_endPosのx,y座標をランダムに決定
        float x = UnityEngine.Random.Range(-0.8f, 1.0f);
        float y = UnityEngine.Random.Range(0.6f, 2.0f);
        _endPos.position = new Vector3(x, y,_endPos.position.z);

        switch (random)
        {
            case 0:
                _pitchType = PitchType.Fastball;
                break;
            case 1:
                _pitchType = PitchType.Curveball;
                break;
            case 2:
                _pitchType = PitchType.Slider;
                break;
            case 3:
                _pitchType = PitchType.Fork;
                break;
        }
        SetupControlPoints();
        StartCoroutine(MoveBall());
    }

    /// <summary>
    /// 球種によって制御点と到達時間を設定
    /// </summary>
    private void SetupControlPoints()
    {
        Debug.Log("軌道をセットアップ");
        PitchSettings p;
        switch (_pitchType)
        {
            case PitchType.Fastball:
                Debug.Log("ストレート");
                p = _controlPointsList.Find(x => x.Name == "ストレート");
                break;
            case PitchType.Curveball:
                Debug.Log("カーブ");
                p = _controlPointsList.Find(x => x.Name == "カーブ");
                break;
            case PitchType.Slider:
                Debug.Log("スライダー");
                p = _controlPointsList.Find(x => x.Name == "スライダー");
                break;
            case PitchType.Fork:
                Debug.Log("フォーク");
                p = _controlPointsList.Find(x => x.Name == "フォーク");
                break;
            default:
                //ここにない球種が来た場合はストレートとして扱う
                p = _controlPointsList.Find(x => x.Name == "ストレート");
                break;
        }

        _controlPoint1 = Vector3.Lerp(_startPoint, _endPos.position, p.PathControlRatio1) + p.ControlPoint;
        _controlPoint2 = Vector3.Lerp(_startPoint, _endPos.position, p.PathControlRatio2) + p.ControlPoint2;
        _pitchDuration = p.Time;        
    }

    /// <summary>
    /// ベジェ曲線を用いたボールの挙動
    /// </summary>
    private IEnumerator MoveBall()
    {
        _pitcherBallMesh.enabled = false;
        _moveBallMesh.enabled = true;
        float t = 0;

        while (t < 1.0f)
        {
            t += Time.deltaTime / _pitchDuration;
            t = Mathf.Clamp01(t); // tを0～1の範囲に制限

            this.transform.position = BezierPoint(_startPoint, _controlPoint1, _controlPoint2, _endPos.position, t);

            yield return null; // 次のフレームまで待機
        }

        _rb.angularVelocity = Vector3.zero;
        _rb.linearVelocity = Vector3.zero;
        _bj.IsPitching();
        _bj.StrikeJudge();
        
        _moveBallMesh.enabled = false;
        _pitcherBallMesh.enabled = true;
        _startPoint = Vector3.zero;
        _meetRenderer.enabled = true;
    }

    public void StopBall()
    {
        _meetRenderer.enabled = true;
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

    private void OnDrawGizmos()
    {
        if (_startPoint != Vector3.zero && _endPos != null)
        {
            // 制御点が計算済みの場合のみ表示
            if (_controlPoint1 != Vector3.zero || _controlPoint2 != Vector3.zero)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(_startPoint, 0.05f);
                Gizmos.DrawSphere(_controlPoint1, 0.05f);
                Gizmos.DrawSphere(_controlPoint2, 0.05f);
                Gizmos.DrawSphere(_endPos.position, 0.05f);

                // 制御線を描画
                Gizmos.color = Color.gray;
                Gizmos.DrawLine(_startPoint, _controlPoint1);
                Gizmos.DrawLine(_controlPoint1, _controlPoint2);
                Gizmos.DrawLine(_controlPoint2, _endPos.position);
            }
        }
    }
}