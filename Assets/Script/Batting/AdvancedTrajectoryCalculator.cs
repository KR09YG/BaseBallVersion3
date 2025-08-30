using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor.Build.Content;
using UnityEngine;

public class AdvancedTrajectoryCalculator : MonoBehaviour
{
    [SerializeField] private PhysicsData _physicsData;
    [SerializeField] private LayerMask _netLayer;
    [SerializeField] private Transform _ballTransform;
    private List<Vector3> _trajectoryPoints = new List<Vector3>();
    private Vector3 _tempPosition = Vector3.zero;
    private Vector3 _beforPosition = Vector3.zero;
    private float _x, _y, _z; // 一時的な座標変数
    private Vector3 _velocity;
    private Collider[] _colliders;
    [Header("計算間隔（時間）"), SerializeField] private float _calculationInterval = 0.1f;
    [Header("弾道計算の計算時間"), SerializeField] private float _calculationTime;
    [Header("地面の高さ"), SerializeField] private float _groundHeight;
    [SerializeField] private GameObject _g;
    [SerializeField] private Transform t;

    private void Start()
    {
        ServiceLocator.Register(this);
    }

    /// <summary>
    /// 弾道計算
    /// </summary>
    /// <param name="landingpoint">着地した座標</param>
    /// <param name="flightTime"> 滞空時間</param>
    /// <returns>弾道の座標リスト</returns>
    public List<Vector3> TrajectoryCalculate(BattingResultData resultData, BattingInputData inputData, out Vector3 landingpoint, out float flightTime)
    {
        _trajectoryPoints.Clear();

        _beforPosition = inputData.BallPosition;

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

            //計算した座標をテンプレートに格納(ガベージコレクションを避けるために毎回新しいVector3を生成しない)
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
                    Instantiate(_g, landingpoint,Quaternion.identity);
                    if (IsFoul(landingpoint))
                    {
                        Debug.Log("ファールボール");
                        resultData.HittingType = BattingResultData.HitType.FoulBall;
                    }
                    else
                    {
                        Debug.Log("フェアボール");
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
            _velocity.x *= -_physicsData.WallReboundCoefficient;
            _velocity.z *= -_physicsData.WallReboundCoefficient;
        }
    }

    public bool IsFoul(Vector3 landingpoint)
    {
        landingpoint -= ServiceLocator.Get<BaseManager>().HomeBase.position;
        Vector3 firstDir = (ServiceLocator.Get<BaseManager>().FirstBase.position - ServiceLocator.Get<BaseManager>().HomeBase.position).normalized;
        Vector3 thirdDir = (ServiceLocator.Get<BaseManager>().ThirdBase.position - ServiceLocator.Get<BaseManager>().HomeBase.position).normalized;

        float angleToFirst = Vector3.Angle(firstDir, landingpoint);
        float angleToThird = Vector3.Angle(thirdDir, landingpoint);
        float fieldAngle = Vector3.Angle(firstDir, thirdDir); 

        // フェア：両方の角度がフィールド角度以下
        bool isFair = angleToFirst <= fieldAngle && angleToThird <= fieldAngle;
        return !isFair;
    }
}