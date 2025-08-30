using System;
using System.Collections.Generic;
using UnityEngine;

public class BattingResultCalculator : MonoBehaviour
{
    [SerializeField] private BattingData _battingData;
    [Header("バッタータイプ別設定")]
    [SerializeField] private List<BatterTypeSettings> _batterTypeSettings = new List<BatterTypeSettings>();

    [Header("デバッグ")]
    [SerializeField] private bool _showCalculationLogs = false;


    public enum AccuracyType
    {
        Perfect,      // 完璧な当たり
        Good,         // 良い当たり
        Fair,         // 普通の当たり
        Bad,          // 悪い当たり
        Miss,          // ミス（当たらない）
        Default
    }

    [Serializable]
    public class BatterTypeSettings
    {
        [Header("バッタータイプ")]
        public BattingInputData.BattingType Type;

        [Header("補正値")]
        public float PowerMultiplier;
        public float DirectionXModifier;
        public float DirectionYPlusCorrection;
        public float ContactRangeMultiplier;
    }

    private void Start()
    {
        ServiceLocator.Register(this);
    }

    /// <summary>
    /// タイミングのジャスト度を計算する
    /// </summary>
    /// <param name="ballProgress">ボールの始点から終点までの到達度（0から1）</param>
    /// <returns>タイミング精度（0～1）</returns>
    public float CalculatePositionBasedTiming(float ballProgress, out BattingResultCalculator.AccuracyType accuracyType)
    {
        float difference = Mathf.Abs(ballProgress * 100 - _battingData.TimingReferenceValue);
        Debug.Log(ballProgress + " " + difference);
        if (difference <= _battingData.PerfectTimingRange)
        {
            accuracyType = AccuracyType.Perfect;
            return Mathf.Lerp(_battingData.PerfectMin, 1f, difference / _battingData.PerfectTimingRange);
        }
        else if (difference <= _battingData.GoodTimingRange)
        {
            accuracyType = AccuracyType.Good;
            return Mathf.Lerp(_battingData.GoodMin, _battingData.PerfectMin, difference / _battingData.GoodTimingRange);
        }
        else if (difference <= _battingData.FairTimingRange)
        {
            accuracyType = AccuracyType.Fair;
            return Mathf.Lerp(_battingData.FairMin, _battingData.GoodMin, difference / _battingData.FairTimingRange);
        }
        else if (difference <= _battingData.BadTimingRange)
        {
            accuracyType = AccuracyType.Bad;
            return Mathf.Lerp(_battingData.Maxtolerable, _battingData.FairMin, difference / _battingData.BadTimingRange);
        }
        else
        {
            accuracyType = AccuracyType.Miss;
            return 0f;
        }
    }


    /// <summary>
    /// BattingInputDataからBattingResultDataを計算
    /// </summary>
    public BattingResultData CalculateBattingResult(BattingInputData inputData)
    {
        if (_showCalculationLogs)
        {
            Debug.Log($"=== バッティング結果計算開始 ===");
            Debug.Log($"入力データ: タイミング={inputData.TimingAccuracy:F3}, タイプ={inputData.BatterType}");
            Debug.Log($"精度タイプ: {inputData.Accuracy}, 距離={inputData.DistanceFromCore:F3}");
        }

        var resultData = new BattingResultData();
        var typeSettings = GetBatterTypeSettings(inputData.BatterType);

        //パワーの計算
        resultData.BattingPower = CalculateBattingPower(inputData, typeSettings);
        //打球方向の計算
        resultData.ActualDirection = CalculateActualDirection(inputData, typeSettings);
        //初速の計算
        resultData.InitialVelocity = resultData.ActualDirection * resultData.BattingPower;
        resultData.LaunchAngle = CalculateLaunchAngle(resultData.ActualDirection);
        resultData.LaunchDirection = CalculateLaunchDirection(resultData.ActualDirection);

        resultData.HittingType = DetermineHittingType(resultData);

        if (_showCalculationLogs)
        {
            Debug.Log($"角度: 打ち上げ={resultData.LaunchAngle:F1}°, 方向={resultData.LaunchDirection:F1}°");
            Debug.Log($"初速ベクトル: {resultData.InitialVelocity}");
        }

        return resultData;
    }

    /// <summary>
    /// バッティングパワーを計算
    /// </summary>
    private float CalculateBattingPower(BattingInputData inputData, BatterTypeSettings typeSettings)
    {
        float power = _battingData.BasePower * inputData.TimingAccuracy * typeSettings.PowerMultiplier;
        return Mathf.Max(power, 0f); // パワーは0以上に制限
    }

    private BattingResultData.HitType DetermineHittingType(BattingResultData resultData)
    {
        if ( resultData.LaunchAngle < _battingData.GroundBallAngle)
        {
            return BattingResultData.HitType.GroundBall;
        }
        else if (resultData.LaunchAngle < _battingData.LineDriveAngle)
        {
            return BattingResultData.HitType.LineDrive;
        }
        else
        {
            return BattingResultData.HitType.FlyBall;
        }
    }
    /// <summary>
    /// 実際の打球方向を計算
    /// </summary>
    private Vector3 CalculateActualDirection(BattingInputData inputData, BatterTypeSettings typeSettings)
    {
        //ボールとバットの位置の差
        float xDistance = inputData.BallPosition.x - inputData.AtCorePosition.x;
        float yDistance = inputData.BallPosition.y - inputData.AtCorePosition.y;

        //　タイミングの精度ごとに打球の高さを決定
        float baseHight = inputData.Accuracy switch { 
            AccuracyType.Perfect => _battingData.PerfectHeight,
            AccuracyType.Good => _battingData.GoodHeight,
            AccuracyType.Fair => _battingData.FairHeight,
            AccuracyType.Bad => _battingData.BadHeight,
            AccuracyType.Miss => 0f,
            _ => 0f
        };

        Debug.Log($"打球高さ: {baseHight}");

        //実際の打球方向を計算
        Vector3 direction = new Vector3(
            xDistance * _battingData.BattingXMultiplier * typeSettings.DirectionXModifier,
            inputData.TimingAccuracy * _battingData.BattingYMultiplier * baseHight + typeSettings.DirectionYPlusCorrection,
            Mathf.Lerp(_battingData.MinBattingZRange, _battingData.MaxBattingZRange, inputData.TimingAccuracy) * -1  // Z軸は前方向（負の値）
        ).normalized;

    Debug.Log(Mathf.Lerp(_battingData.MinBattingZRange, _battingData.MaxBattingZRange, inputData.TimingAccuracy)* -1);

        return direction;
    }

/// <summary>
/// 打ち上げ角度を計算
/// </summary>
private float CalculateLaunchAngle(Vector3 direction)
{
    //高さを無視した平面上の方向
    Vector3 horizontalDirection = new Vector3(direction.x, 0, direction.z);

    if (horizontalDirection.magnitude < 0.001f)
    {
        return direction.y > 0 ? 90f : -90f; // 垂直方向
    }

    float angle = Vector3.Angle(horizontalDirection, direction);
    return direction.y < 0 ? -angle : angle; // 下向きの場合は負の角度
}

/// <summary>
/// 水平方向の角度を計算（左右の方向）
/// </summary>
private float CalculateLaunchDirection(Vector3 direction)
{
    float angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
    return angle;
}

/// <summary>
/// バッタータイプ設定を取得
/// </summary>
private BatterTypeSettings GetBatterTypeSettings(BattingInputData.BattingType type)
{
    var settings = _batterTypeSettings.Find(x => x.Type == type);
    return settings;
}
}
