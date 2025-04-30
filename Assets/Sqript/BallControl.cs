using UnityEngine;
using System.Collections;
using System.Collections.Generic;
public class BallControl : MonoBehaviour
{
    [SerializeField, Header("始点")] Transform _startPos;
    [SerializeField, Header("終点")] Transform _endPos;
    [SerializeField] List<PitchSettings> _controlPointsList;
    [SerializeField] PitchType _pitchType;
    /// <summary>
    /// 制御点
    /// </summary>
    Vector3 _controlPoint1, _controlPoint2;
    /// <summary>
    /// 始点から終点にたどり着くまでの時間
    /// </summary>
    private float _pitchDuration;

    [System.Serializable]
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
    private enum PitchType
    {
        Fastball,
        Curveball,
        Slider,
        Fork
    }

    private void Start()
    {
        this.transform.position = _startPos.position;
        SetupControlPoints();
        StartCoroutine(MoveBall());
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.S))
        {
            Start();
        }
    }

    /// <summary>
    /// 球種によって制御点と到達時間を設定
    /// </summary>
    private void SetupControlPoints()
    {
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

        _controlPoint1 = Vector3.Lerp(_startPos.position, _endPos.position, p.PathControlRatio1) + p.ControlPoint;
        _controlPoint2 = Vector3.Lerp(_startPos.position, _endPos.position, p.PathControlRatio2) + p.ControlPoint2;
        _pitchDuration = p.Time;
    }

    /// <summary>
    /// ベジェ曲線を用いたボールの挙動
    /// </summary>
    private IEnumerator MoveBall()
    {
        float t = 0;

        while (t < 1.0f)
        {
            t += Time.deltaTime / _pitchDuration;
            t = Mathf.Clamp01(t); // tを0～1の範囲に制限

            this.transform.position = BezierPoint(_startPos.position, _controlPoint1, _controlPoint2, _endPos.position, t);

            yield return null; // 次のフレームまで待機
        }
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
        if (_startPos != null && _endPos != null)
        {
            // 制御点が計算済みの場合のみ表示
            if (_controlPoint1 != Vector3.zero || _controlPoint2 != Vector3.zero)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(_startPos.position, 0.05f);
                Gizmos.DrawSphere(_controlPoint1, 0.05f);
                Gizmos.DrawSphere(_controlPoint2, 0.05f);
                Gizmos.DrawSphere(_endPos.position, 0.05f);

                // 制御線を描画
                Gizmos.color = Color.gray;
                Gizmos.DrawLine(_startPos.position, _controlPoint1);
                Gizmos.DrawLine(_controlPoint1, _controlPoint2);
                Gizmos.DrawLine(_controlPoint2, _endPos.position);
            }
        }
    }
}