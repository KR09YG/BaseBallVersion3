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
    [SerializeField] private BallControl _ballControl;
    [SerializeField] private CursorController _cursorController;
    [SerializeField] private BattingResultCalculator _bc;
    [SerializeField] private BattingBallMove _bbm;
    [SerializeField] private RunnerCalculation _runnerCalculation;
    [SerializeField] private BaseManager _baseManager;
    [SerializeField] private BallJudge _ballJudge;


    [SerializeField] BattingResultCalculator.BatterTypeSettings _currentBatterType;

    [SerializeField] private AdvancedTrajectoryCalculator _atc;

    private BattingResultData _resultData;

    private BattingInputData _inputData;

    

    IEnumerator _tempCoroutine;

    public List<Vector3> _trajectoryPoints { get; private set; } = new List<Vector3>();

    public void StartInput()
    {
        _inputData = InputData();
        if (_inputData.Accuracy != BattingResultCalculator.AccuracyType.Miss)
        {
            _ballControl.StopBall();
            _trajectoryPoints = _atc.TrajectoryCalculate(_resultData, _inputData, out Vector3 landingPoint, out float flightTime);
            Debug.Log($"打球の落下点: {landingPoint}, 飛行時間: {flightTime}秒");
            Debug.Log($"打球タイプ: {_resultData.HittingType}");
            if (_resultData.HittingType == BattingResultData.HitType.FoulBall) _ballJudge.FoulBall();
            else _ballJudge.Hit();
            _tempCoroutine = _bbm.BattingMove(_trajectoryPoints, landingPoint);
            StartCoroutine(_tempCoroutine);
            StartCoroutine(_runnerCalculation.RunningCalculate(6f, 0f, _baseManager.HomeBase, _baseManager.FirstBase));
        }
        else
        {
            _ballJudge.SwingStrike();
            Debug.Log("空振り");
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (_tempCoroutine != null)
            {
                StopCoroutine(_tempCoroutine);
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
        inputData.BallPosition = _ballControl.transform.position;
        inputData.AtCorePosition = _cursorController.CursorPosition.position;
        inputData.DistanceFromCore = Vector3.Distance(inputData.BallPosition, inputData.AtCorePosition);

        // 精度計算
        inputData.TimingAccuracy = _bc.CalculatePositionBasedTiming(
            _ballControl.BallPitchProgress, out BattingResultCalculator.AccuracyType accuracyType);
        inputData.Accuracy = accuracyType;
        Debug.Log(inputData.TimingAccuracy);
        Debug.Log(inputData.Accuracy);

        // プレイヤー設定
        inputData.BatterType = _currentBatterType.Type;

        _resultData = _bc.CalculateBattingResult(inputData);
        return inputData;
    }

    private void OnDrawGizmos()
    {
        if (_resultData != null)
            Gizmos.DrawLine(Vector3.zero, _resultData.ActualDirection);
    }
}