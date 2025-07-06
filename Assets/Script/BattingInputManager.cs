using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 打球に関するデータ
/// </summary>
[Serializable]
public class TrajectoryData
{
    public Vector3 DropPoint;//落下点
    public float FlightTime;//飛行時間
    public float MaxHeight;//最高到達点の高さ
    public float FlightDistance;//飛距離
    public List<Vector3> TrajectoryPoints;//軌道の点群

    public HitType hitType;//打球の種類
    public bool isHomeRun;

    /// <summary>
    /// 打球の種類を表す列挙型
    /// </summary>
    public enum HitType
    {
        GroundBall,
        LineDrive,
        FlyBall,
        PopFly,
        HomeRun
    }
}

/// <summary>
/// バッティングの入力データ
/// </summary>
[Serializable]
public class BattingInputData
{
    public float InputTime;              // 入力した時刻
    public float TimingAccuracy;         // タイミングの精度（0.0～1.0）
    public BattingCalculation.AccuracyType Accuracy;    // Perfect/Good/Fair/Bad/Miss

    [Header("位置情報")]
    public Vector3 BallPosition;         // 入力時のボール位置
    public Vector3 AtCorePosition;      // 入力時のバット位置
    public float DistanceFromCore;       // ボールとバットの距離

    [Header("プレイヤー設定")]
    public BattingType BatterType;       // 選択したバッタータイプ
    public Vector3 CursorPosition;       // カーソル位置

    public enum BattingType
    {
        Normal, PullHit, OppositeHit, AimHomeRun
    }
}

/// <summary>
/// 打撃結果のデータ
/// </summary>
[Serializable]
public class BattingResultData
{
    public float battingPower; //打球パワー

    public Vector3 initialVelocity; //初速
    public float launchAngle; // 打ち上げ角度
    public float launchDirection; // 打球方向（水平角度）
}

public class BattingInputManager : MonoBehaviour
{
    [SerializeField] private BallControl _ballControl;
    [SerializeField] private CursorController _cursorController;
    [SerializeField] private BattingCalculation _battingCalculation;

    [SerializeField] private float _inputCooldown = 0.1f;

    private float _lastInputTime;

    public event System.Action<BattingInputData> OnBattingInput;

    private void Update()
    {
        if (CanInput())
        {
            var inputData = InputData();
            OnBattingInput?.Invoke(inputData);
            _lastInputTime = Time.time;
        }
    }

    private bool CanInput()
    {
        // クールダウン時間を超えているかつ、カーソルがゾーン内にあるかをチェック
        return Time.time - _lastInputTime > _inputCooldown && _cursorController.IsCursorInZone;
    }

    /// <summary>
    /// BattingInputDataをインプットする
    /// </summary>
    /// <returns>
    /// 打球に関するデータ
    /// </returns>
    private BattingInputData InputData()
    {
        var inputData = new BattingInputData();

        // タイミング情報
        inputData.InputTime = Time.time;
        inputData.BallPosition = _ballControl.transform.position;
        inputData.AtCorePosition = _cursorController.CursorPosition.position;
        inputData.DistanceFromCore = Vector3.Distance(inputData.BallPosition, inputData.AtCorePosition);

        // 精度計算
        inputData.TimingAccuracy = _battingCalculation.CalculatePositionBasedTiming(
            inputData.BallPosition, inputData.AtCorePosition, _battingCalculation.CurrentType);
        inputData.Accuracy = _battingCalculation.PassResultAccuracy();

        // プレイヤー設定
        inputData.BatterType = (BattingInputData.BattingType)(int)_battingCalculation.CurrentType;
        inputData.CursorPosition = _cursorController != null ? _cursorController.transform.position : Vector3.zero;

        return inputData;
    }
}