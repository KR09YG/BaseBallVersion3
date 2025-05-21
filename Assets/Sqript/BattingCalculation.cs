using System;
using System.Collections.Generic;
using UnityEngine;

public class BattingCalculation : MonoBehaviour
{
    [SerializeField] private List<BattingTypeParameters> _BattingTypeData;
    [SerializeField] private float _basePower;
    [SerializeField] private float _maxHihgt;

    // 許容範囲の設定
    [SerializeField] float _perfectRange;    // 完璧な当たりの許容範囲
    [SerializeField] float _goodRange;      // 良い当たりの許容範囲
    [SerializeField] float _fairRange;       // 普通の当たりの許容範囲
    [SerializeField] float _maxRange;        // これ以上は当たらない

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
        public BattingType Type;
        public float DirectionXModifier; // X方向の補正値
        public float DirectionYModifier; // Y方向（弾道）の補正値
        public float PowerMultiplier;    // パワー倍率
        public float ContactRangeBonus;  // ミート範囲
    }

    public float CalculatePositionBasedAccuracy(Vector3 ballPosition, Vector3 batCore)
    {
        // ボールと理想的なコンタクトポイントの距離を計算
        float distance = Vector3.Distance(ballPosition, batCore);

        

        // 距離に基づいて精度（0.0〜1.0）を計算
        if (distance <= _perfectRange)
        {
            // 完璧なコンタクト (0.9〜1.0)
            return Mathf.Lerp(0.9f, 1.0f, 1.0f - (distance / _perfectRange));
        }
        else if (distance <= _goodRange)
        {
            // 良いコンタクト (0.7〜0.9)
            return Mathf.Lerp(0.7f, 0.9f, 1.0f - ((distance - _perfectRange) / (_goodRange - _perfectRange)));
        }
        else if (distance <= _fairRange)
        {
            // 普通のコンタクト (0.4〜0.7)
            return Mathf.Lerp(0.4f, 0.7f, 1.0f - ((distance - _goodRange) / (_fairRange - _goodRange)));
        }
        else if (distance <= _maxRange)
        {
            // 悪いコンタクト (0.0〜0.4)
            return Mathf.Lerp(0.0f, 0.4f, 1.0f - ((distance - _fairRange) / (_maxRange - _fairRange)));
        }
        else
        {
            // ミス（コンタクトなし）
            return 0.0f;
        }
    }

    public Vector3 CalculateBatting(Vector3 ballPos, Vector3 batCore, float timing, BattingType type)
    {
        BattingTypeParameters p = _BattingTypeData.Find(x => x.Type == type);
        float battingPower = _basePower * timing * p.PowerMultiplier;
        Vector3 battingDirection  = new Vector3(
            ballPos.x -  batCore.x + p.DirectionXModifier,
            timing * _maxHihgt + p.DirectionYModifier,
            Mathf.Lerp(-0.5f, 1.0f, timing) * - 1
            ).normalized;

        return battingDirection * battingPower;
    }
}
