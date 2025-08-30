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

    public bool CanStartInput { get; private set; } = true;

    public Action _startReplay;
    public Action _endReplay;

    private void Start()
    {
        ServiceLocator.Register(this);
        _startReplay += () =>
        {
            ServiceLocator.Get<BallControl>().StopBall();
            BallMoveCoroutine = ServiceLocator.Get<BattingBallMove>().BattingMove(_trajectoryPoints, _landingPoint, true);
            CanStartInput = false;
            StartCoroutine(BallMoveCoroutine);
        };
        _endReplay += () => 
        {
            CanStartInput = true;
            ResetData();
        };
    }

    public void StartInput()
    {
        _inputData = InputData();

        if (_inputData.Accuracy != BattingResultCalculator.AccuracyType.Miss) // ボールがバットに当たった場合
        {
            // 投球の処理を停止
            ServiceLocator.Get<BallControl>().StopBall();

            // 弾道の計算
            _trajectoryPoints = ServiceLocator.Get<AdvancedTrajectoryCalculator>().TrajectoryCalculate(_resultData, _inputData, out Vector3 landingPoint, out float flightTime);

            if (_debug)
            {
                Debug.Log($"打球の落下点: {landingPoint}, 飛行時間: {flightTime}秒");
                Debug.Log($"打球タイプ: {_resultData.HittingType}");
            }

            // 打球の種類に応じた処理
            if (_resultData.HittingType == BattingResultData.HitType.FoulBall) ServiceLocator.Get<BallJudge>().FoulBall();
            else ServiceLocator.Get<BallJudge>().Hit();

            // 弾道計算の結果をもとに実際にボールを動かす
            BallMoveCoroutine = ServiceLocator.Get<BattingBallMove>().BattingMove(_trajectoryPoints, landingPoint, false);
            _landingPoint = landingPoint;
            StartCoroutine(BallMoveCoroutine);

            // ランナーの計算を開始
            StartCoroutine(ServiceLocator.Get<RunnerCalculation>().RunningCalculate(
                6f, 0f, ServiceLocator.Get<BaseManager>().HomeBase, ServiceLocator.Get<BaseManager>().FirstBase));
        }
        else　 // ボールがバットに当たらなかった場合
        {
            ServiceLocator.Get<BallJudge>().SwingStrike();
            if (_debug) Debug.Log("空振り");
        }
    }

    /// <summary>
    /// 一度打球を動かした後空振りしても勝手に以前の打球を再生しないようにするための設定
    /// </summary>
    public void ResetData()
    {
        _inputData = null;
        _trajectoryPoints.Clear();
        _landingPoint = Vector3.zero;
        _resultData = null;
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
        inputData.BallPosition = ServiceLocator.Get<BallControl>().transform.position;
        inputData.AtCorePosition = ServiceLocator.Get<CursorController>().CursorPosition.position;
        inputData.DistanceFromCore = Vector3.Distance(inputData.BallPosition, inputData.AtCorePosition);

        // 精度計算
        inputData.TimingAccuracy = ServiceLocator.Get<BattingResultCalculator>().CalculatePositionBasedTiming(
            ServiceLocator.Get<BallControl>().BallPitchProgress, out BattingResultCalculator.AccuracyType accuracyType);
        inputData.Accuracy = accuracyType;
        Debug.Log(inputData.TimingAccuracy);
        Debug.Log(inputData.Accuracy);

        // プレイヤー設定
        inputData.BatterType = _currentBatterType.Type;

        _resultData = ServiceLocator.Get<BattingResultCalculator>().CalculateBattingResult(inputData);
        return inputData;
    }

    private void OnDrawGizmos()
    {
        if (_resultData != null)
            Gizmos.DrawLine(Vector3.zero, _resultData.ActualDirection);
    }
}