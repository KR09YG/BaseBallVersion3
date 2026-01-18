using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class FielderThrowBallMove : BallMoveTrajectory
{
    [Header("送球(山なり)設定")]
    [SerializeField] private float _arcHeight = 3.0f;      // 山の高さ（調整用）
    [SerializeField] private float _minFlightTime = 0.35f; // 近距離でも不自然に速すぎない
    [SerializeField] private float _maxFlightTime = 1.20f; // 遠距離でも間延びしない

    private UniTaskCompletionSource _tcs;
    private CancellationTokenRegistration _ctr;

    /// <summary>
    /// 山なり送球を開始し、終点到達まで await できる
    /// speed: m/s想定（ThrowStep.ThrowSpeed）
    /// </summary>
    public UniTask ThrowToAsync(Vector3 from, Vector3 to, float speed, CancellationToken ct)
    {
        StopInternal(); // 前の移動があれば止める

        transform.position = from;
        _elapsedTime = 0f;

        // 距離→飛行時間（直線距離/速度をベースに、上下限を付ける）
        float dist = Vector3.Distance(from, to);
        float flightTime = dist / Mathf.Max(0.01f, speed);
        flightTime = Mathf.Clamp(flightTime, _minFlightTime, _maxFlightTime);

        // 点列を生成（ベジェ）
        _trajectory = BuildArcBezierTrajectory(from, to, flightTime, _trajectoryDeltaTime, _arcHeight);

        _isMoving = true;

        _tcs = new UniTaskCompletionSource();

        if (ct.CanBeCanceled)
        {
            _ctr = ct.Register(() =>
            {
                StopInternal();
                _tcs?.TrySetCanceled(ct);
            });
        }

        return _tcs.Task;
    }

    /// <summary>
    /// ベジェ曲線で山なり弾道の点列を作る
    /// </summary>
    private List<Vector3> BuildArcBezierTrajectory(
        Vector3 from,
        Vector3 to,
        float flightTime,
        float dt,
        float arcHeight)
    {
        int sampleCount = Mathf.Max(2, Mathf.CeilToInt(flightTime / dt) + 1);

        // 中点を持ち上げた制御点（高さは距離に応じて少し増やすと自然）
        Vector3 mid = (from + to) * 0.5f;

        float dist = Vector3.Distance(from, to);
        float height = arcHeight + dist * 0.05f; // 係数は好みで調整
        Vector3 control = new Vector3(mid.x, Mathf.Max(from.y, to.y) + height, mid.z);

        var traj = new List<Vector3>(sampleCount);
        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / (sampleCount - 1);

            // 二次ベジェ: (1-t)^2*P0 + 2(1-t)t*P1 + t^2*P2
            Vector3 p =
                (1 - t) * (1 - t) * from +
                2 * (1 - t) * t * control +
                t * t * to;

            traj.Add(p);
        }

        return traj;
    }

    private void StopInternal()
    {
        _ctr.Dispose();
        _isMoving = false;
        _trajectory = null;

        // 前回の待ちが残っていたらキャンセル扱いに（多重呼び出し対策）
        _tcs?.TrySetCanceled();
        _tcs = null;
    }

    protected override void ApplySpin()
    {
        // 投球っぽく：進行方向に対して回転（簡易）
        float spin = 720f * _spinSpeedMultiplier;
        transform.Rotate(spin * Time.deltaTime, 0f, 0f, Space.Self);
    }

    protected override void OnReachedEnd()
    {
        _isMoving = false;
        _trajectory = null;
        _ctr.Dispose();

        _tcs?.TrySetResult();
        _tcs = null;
    }

    private void OnDisable()
    {
        StopInternal();
    }
}
