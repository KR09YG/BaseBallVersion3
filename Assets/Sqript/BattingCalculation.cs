using System;
using System.Collections.Generic;
using UnityEngine;

public class BattingCalculation : MonoBehaviour
{
    [SerializeField] private List<BattingTypeParameters> _BattingTypeData;
    [SerializeField] private float _basePower;
    [SerializeField] private float _baseHihgt;

    // 許容範囲の設定
    [Header("各タイミングの判定"),SerializeField] 
                     float _perfectRange;    // 完璧な当たりの許容範囲
    [SerializeField] float _goodRange;      // 良い当たりの許容範囲
    [SerializeField] float _fairRange;       // 普通の当たりの許容範囲
    [SerializeField] float _maxRange;        // これ以上は当たらない

    [Header("各タイミングの倍率"),SerializeField] 
                     private float _perfectMultiplier;
    [SerializeField] private float _goodMultiplier;
    [SerializeField] private float _fairMultiplier;

    [Header("打球のx方向に対する倍率"),SerializeField] private float _battingXMultiplier;
    [Header("打球のy方向に対する倍率"),SerializeField] private float _battingYMultiplier;

    public BattingType CurrentType;
    public enum BattingType
    {
        Normal,       // 通常スイング
        PullHit,      // 引っ張り
        OppositeHit,  // 流し打ち
        AimHomeRun    // 一発狙い
    }

    [Serializable]
    public class BattingTypeParameters
    {
        [Header("バッターのタイプ")] public BattingType Type;
        [Header("X方向の補正値")] public float DirectionXModifier;
        [Header("Y方向（弾道）の補正値")] public float DirectionYModifier;
        [Header("パワーの倍率")] public float PowerMultiplier;
        [Header("ミートの範囲倍率")] public float ContactRangeMultiplier;  // ミート範囲
    }

    /// <summary>
    /// タイミングのジャスト度を計算する
    /// </summary>
    /// <param name="ballPosition"></param>
    /// <param name="batCore"></param>
    /// <returns></returns>
    public float CalculatePositionBasedTiming(Vector3 ballPosition, Vector3 batCore, BattingType type)
    {
        // ボールと理想的なコンタクトポイントの距離を計算
        float distance = Vector3.Distance(ballPosition, batCore);
        BattingTypeParameters p = _BattingTypeData.Find(x => x.Type == type);

        //AimHomeRunの場合はgoodの範囲内であればホームランになるようにする
        //ただしホームラン性のあるボールもしくはファールしか打てない
        if (p.Type == BattingType.AimHomeRun)
        {
            if (distance <= _goodRange * p.ContactRangeMultiplier)
            {
                return Mathf.Lerp(0.9f, 1.0f, 1.0f - (distance / _perfectRange)) * _perfectMultiplier;
            }
            else
            {
                return 0.0f;
            }
        } 

        // 距離に基づいて精度（0.0〜1.0）を計算
        if (distance <= _perfectRange * p.ContactRangeMultiplier)
        {
            // 完璧なコンタクト (0.9〜1.0)
            Debug.Log("perfect");
            return Mathf.Lerp(0.9f, 1.0f, 1.0f - (distance / _perfectRange)) * _perfectMultiplier;
        }
        else if (distance <= _goodRange * p.ContactRangeMultiplier)
        {
            // 良いコンタクト (0.7〜0.9)
            Debug.Log("good");
            return Mathf.Lerp(0.7f, 0.9f, 1.0f - ((distance - _perfectRange) / (_goodRange - _perfectRange))) * _goodMultiplier;
        }
        else if (distance <= _fairRange * p.ContactRangeMultiplier)
        {
            // 普通のコンタクト (0.4〜0.7)
            Debug.Log("fair");
            return Mathf.Lerp(0.4f, 0.7f, 1.0f - ((distance - _goodRange) / (_fairRange - _goodRange))) * _fairMultiplier;
        }
        else if (distance <= _maxRange * p.ContactRangeMultiplier)
        {
            // 悪いコンタクト (0.0〜0.4)
            Debug.Log("bad");
            return Mathf.Lerp(0.0f, 0.4f, 1.0f - ((distance - _fairRange) / (_maxRange - _fairRange)));
        }
        else
        {
            // ミス（コンタクトなし）
            Debug.Log("miss");
            return 0.0f;
        }
    }

    /// <summary>
    /// タイミングやバットの芯とボールの距離を用いて打球方向を計算する
    /// </summary>
    /// <param name="ballPos"></param>
    /// <param name="batCore"></param>
    /// <param name="timing"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    public Vector3 CalculateBattingDirection(Vector3 ballPos, Vector3 batCore, float timing, BattingType type)
    {
        BattingTypeParameters p = _BattingTypeData.Find(x => x.Type == type);

        float xDistance = ballPos.x - batCore.x;
        float yDistance = ballPos.y - batCore.y;
        float battingPower = _basePower * timing * p.PowerMultiplier;
        Vector3 battingDirection = new Vector3(
            xDistance* _battingXMultiplier + p.DirectionXModifier,
            timing * _baseHihgt * p.DirectionYModifier + yDistance * _battingYMultiplier,
            Mathf.Lerp(-5f, 10f, timing) * -1　//-1をかけているのはzが負の時前方であるため
            ).normalized;

        Debug.Log(battingDirection);

        return battingDirection * battingPower;
    }
}
