using System;
using System.Collections;
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
    public BattingResultCalculator.AccuracyType Accuracy;    // Perfect/Good/Fair/Bad/Miss

    [Header("位置情報")]
    public Vector3 BallPosition;         // 入力時のボール位置
    public Vector3 AtCorePosition;      // 入力時のバット位置
    public float DistanceFromCore;       // ボールとバットの距離

    [Header("プレイヤー設定")]
    public BattingType BatterType;       // 選択したバッタータイプ

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
    /// <summary>
    /// 打球のパワー
    /// </summary>
    public float BattingPower;
    /// <summary>
    /// 打球方向
    /// </summary>
    public Vector3 ActualDirection;
    /// <summary>
    /// 初速ベクトル
    /// </summary> 
    public Vector3 InitialVelocity;
    /// <summary>
    /// 打球角度（Y）
    /// </summary>
    public float LaunchAngle;
    /// <summary>
    /// 打球方向（水平角度）
    /// </summary>
    public float LaunchDirection;

    public HitType HittingType; // 打球の種類

    public enum HitType
    {
        GroundBall,
        LineDrive,
        FoulBall,
        FlyBall,
        PopFly,
        HomeRun
    }
}

public class BattingInputManager : MonoBehaviour
{
    [SerializeField] BattingResultCalculator.BatterTypeSettings _currentBatterType;

    [SerializeField] bool _debug;

    private BattingResultData _resultData;

    private BattingInputData _inputData;

    public IEnumerator BallMoveCoroutine { get; private set; }

    public List<Vector3> _trajectoryPoints { get; private set; } = new List<Vector3>();

    Vector3 _landingPoint;

    public void StartInput()
    {
        _inputData = InputData();

        if (_inputData.Accuracy != BattingResultCalculator.AccuracyType.Miss) // ボールがバットに当たった場合
        {
            // 投球の処理を停止
            ServiceLocator.BallControlInstance.StopBall();

            // 弾道の計算
            _trajectoryPoints = ServiceLocator.AdvancedTrajectoryCalculatorInstance.TrajectoryCalculate(_resultData, _inputData, out Vector3 landingPoint, out float flightTime);

            if (_debug)
            {
                Debug.Log($"打球の落下点: {landingPoint}, 飛行時間: {flightTime}秒");
                Debug.Log($"打球タイプ: {_resultData.HittingType}");
            }

            // 打球の種類に応じた処理
            if (_resultData.HittingType == BattingResultData.HitType.FoulBall) ServiceLocator.BallJudgeInstance.FoulBall();
            else ServiceLocator.BallJudgeInstance.Hit();

            // 弾道計算の結果をもとに実際にボールを動かす
            BallMoveCoroutine = ServiceLocator.BattingBallMoveInstance.BattingMove(_trajectoryPoints, landingPoint, false);
            _landingPoint = landingPoint;
            StartCoroutine(BallMoveCoroutine);

            // ランナーの計算を開始
            StartCoroutine(ServiceLocator.RunnerCalculationInstance.RunningCalculate(
                6f, 0f, ServiceLocator.BaseManagerInstance.HomeBase, ServiceLocator.BaseManagerInstance.FirstBase));
        }
        else　 // ボールがバットに当たらなかった場合
        {
            ServiceLocator.BallJudgeInstance.SwingStrike();
            if (_debug) Debug.Log("空振り");
        }
    }

    public void RePlayHomeRunBall()
    {
        ServiceLocator.BallControlInstance.StopBall();
        BallMoveCoroutine = ServiceLocator.BattingBallMoveInstance.BattingMove(_trajectoryPoints, _landingPoint, true);
        StartCoroutine(BallMoveCoroutine);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (BallMoveCoroutine != null)
            {
                StopCoroutine(BallMoveCoroutine);
            }
        }
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
        inputData.BallPosition = ServiceLocator.BallControlInstance.transform.position;
        inputData.AtCorePosition = ServiceLocator.CursorControllerInstance.CursorPosition.position;
        inputData.DistanceFromCore = Vector3.Distance(inputData.BallPosition, inputData.AtCorePosition);

        // 精度計算
        inputData.TimingAccuracy = ServiceLocator.BattingResultCalculatorInstance.CalculatePositionBasedTiming(
            ServiceLocator.BallControlInstance.BallPitchProgress, out BattingResultCalculator.AccuracyType accuracyType);
        inputData.Accuracy = accuracyType;
        Debug.Log(inputData.TimingAccuracy);
        Debug.Log(inputData.Accuracy);

        // プレイヤー設定
        inputData.BatterType = _currentBatterType.Type;

        _resultData = ServiceLocator.BattingResultCalculatorInstance.CalculateBattingResult(inputData);
        return inputData;
    }

    private void OnDrawGizmos()
    {
        if (_resultData != null)
            Gizmos.DrawLine(Vector3.zero, _resultData.ActualDirection);
    }
}