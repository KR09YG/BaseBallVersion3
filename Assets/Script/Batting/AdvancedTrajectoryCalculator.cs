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

    // 弾道を保存するリスト
    private List<Vector3> _trajectoryPoints = new List<Vector3>();
    private Vector3 _tempPosition = Vector3.zero;
    private float _x, _y, _z; // 一時的な座標変数
    private Vector3 _velocity;
    private Collider[] _colliders;

    [Header("計算間隔（時間）"), SerializeField] private float _calculationInterval = 0.1f;
    [Header("弾道計算の計算時間"), SerializeField] private float _calculationTime;
    [Header("地面の高さ"), SerializeField] private float _groundHeight;

    [SerializeField] private GameObject _g;
    [SerializeField] private Transform t;

    /// <summary>
    /// 弾道計算
    /// </summary>
    /// <param name="landingpoint">着地した座標</param>
    /// <param name="flightTime"> 滞空時間</param>
    /// <returns>弾道の座標リスト</returns>
    public List<Vector3> TrajectoryCalculate(BattingResultData resultData, BattingInputData inputData, out Vector3 landingpoint, out float flightTime)
    {
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

            //x座標の計算(x(t) = x₀ + v₀ₓ × t)
            _x = startPos.x + _velocity.x * (t - groundTime);
            //y座標の計算(y(t) = y₀ + v₀ᵧ × t - 1/2gt²)
            _y = startPos.y + _velocity.y * (t - groundTime) - 0.5f * _physicsData.Gravity * (t - groundTime) * (t - groundTime);
            //z座標の計算(z(t) = z₀ + v₀𝓏 × t)
            _z = startPos.z + _velocity.z * (t - groundTime);

            _tempPosition.x = _x; _tempPosition.y = _y; _tempPosition.z = _z;

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
                    isFirstGround = false;
                    Instantiate(_g, landingpoint, Quaternion.identity);

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

        return _trajectoryPoints;
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
            Vector3 normal = (_tempPosition - closestPoint).normalized;
            Vector3 horizontalVelocity = new Vector3(_velocity.x, 0, _velocity.z);
            Vector3 reflection = Vector3.Reflect(horizontalVelocity, normal);
            Debug.Log(reflection);
            _velocity.x *= reflection.x * _physicsData.WallReboundCoefficient;
            _velocity.z *= reflection.z * _physicsData.WallReboundCoefficient;
        }
    }

    
}