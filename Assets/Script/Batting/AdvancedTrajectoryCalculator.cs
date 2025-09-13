using System.Collections.Generic;
using UnityEngine;

public class AdvancedTrajectoryCalculator : MonoBehaviour
{
    [SerializeField] private PhysicsData _physicsData;
    [SerializeField] private LayerMask _netLayer;
    [SerializeField] private Transform _ballTransform;
    [SerializeField] private FoulChecker _foulChecker;
    [SerializeField] private HomeRunChecker _homeRunChecker;
    [SerializeField] private BaseManager _baseManager;
    [SerializeField] private InitialSpeedData _batttingResultData;

    // 弾道を保存するリスト
    private List<Vector3> _trajectoryPoints = new List<Vector3>();
    private Vector3 _tempPosition = Vector3.zero;
    private float _x, _y, _z; // 一時的な座標変数
    private Vector3 _velocity;
    private Collider[] _colliders;

    [Header("計算間隔（時間）"), SerializeField] private float _calculationInterval = 0.1f;
    [Header("弾道計算の計算時間"), SerializeField] private float _calculationTime;
    [Header("地面の高さ"), SerializeField] private float _groundHeight;

    /// <summary>
    /// 弾道計算
    /// </summary>
    /// <param name="landingpoint">着地した座標</param>
    /// <param name="flightTime"> 滞空時間</param>
    /// <returns>弾道の座標リスト</returns>
    public TrajectoryData TrajectoryCalculate(InitialSpeedData resultData, BattingInputData inputData, out Vector3 landingpoint, out float flightTime)
    {
        TrajectoryData trajectoryData = new TrajectoryData();
        trajectoryData.hitType = TrajectoryData.HitType.LineDrive;
        _trajectoryPoints.Clear();

        Vector3 startPos = inputData.BallPosition;
        float groundTime = 0;

        float t = 0;
        bool isFirstGround = true;
        flightTime = t; // 滞空時間を出力パラメータに設定
        landingpoint = _tempPosition;
        _velocity = resultData.InitialVelocity;

        while (t < _calculationTime)
        {
            t += _calculationInterval;

            float timeFromLastBounce = t - groundTime;

            //x座標の計算(x(t) = x₀ + v₀ₓ × t)
            _tempPosition.x = startPos.x + _velocity.x * timeFromLastBounce;
            //y座標の計算(y(t) = y₀ + v₀ᵧ × t - 1/2gt²)
            _tempPosition.y = startPos.y + _velocity.y * timeFromLastBounce - 0.5f * _physicsData.Gravity * timeFromLastBounce * timeFromLastBounce;
            //z座標の計算(z(t) = z₀ + v₀𝓏 × t)
            _tempPosition.z = startPos.z + _velocity.z * timeFromLastBounce;

            if (_y < 0)
            {
                startPos = _tempPosition;
                startPos.y = _groundHeight;
                _tempPosition.y = _groundHeight;
                groundTime = t; // 地面に着地した時間を記録

                _velocity.x *= _physicsData.FrictionCoefficient;
                _velocity.z *= _physicsData.FrictionCoefficient;

                // 反発係数を適用
                _velocity.y *= -_physicsData.ReboundCoefficient;

                if (isFirstGround)
                {
                    flightTime = t; // 滞空時間を出力パラメータに設定
                    landingpoint = _tempPosition;
                    trajectoryData.LandingPosition = landingpoint;
                    isFirstGround = false;

                    // ファールかフェアかを判定
                    if (_foulChecker.IsFoul(landingpoint))
                    {
                        Debug.Log("ファールボール");
                        resultData.HittingType = HitType.FoulBall;
                    }
                    else
                    {
                        Debug.Log("フェアボール");
                        // ホームランかを判定する
                        if (_homeRunChecker.HomeRunCheck(landingpoint))
                        {
                            resultData.HittingType = HitType.HomeRun;
                        }
                    }
                }
            }

            BounceWall();

            //計算した座標をリストに追加
            _trajectoryPoints.Add(_tempPosition);
        }

        trajectoryData.TrajectoryPoints = _trajectoryPoints;

        return trajectoryData;
    }

    private void BounceWall()
    {
        _colliders = Physics.OverlapSphere(_tempPosition, _ballTransform.localScale.x, _netLayer);

        // ネットに衝突した場合の処理
        if (_colliders.Length > 0)
        {
            Debug.Log("ネットに衝突");
            Collider hitCollider = _colliders[0];
            Vector3 closestPoint = hitCollider.ClosestPoint(_tempPosition);
            Vector3 inDirection = new Vector3(_velocity.x, 0, _velocity.z);
            Vector3 normal = (_tempPosition - closestPoint).normalized;
            Vector3 reflection = Vector3.Reflect(inDirection, normal);
            _velocity.x = reflection.x * _physicsData.WallReboundCoefficient;
            _velocity.z = reflection.z * _physicsData.WallReboundCoefficient;
        }
    }

    
}