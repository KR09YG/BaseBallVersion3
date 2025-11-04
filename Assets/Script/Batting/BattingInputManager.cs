using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattingInputManager : MonoBehaviour
{
    [SerializeField] BattingInitialSpeedCalculator.BatterTypeSettings _currentBatterType;
    [SerializeField] GameStateManager _gameStateManager;
    [SerializeField] RePlayManager _rePlayManager;
    [SerializeField] BallControl _ballControl;
    [SerializeField] BattingTimingCalculator _timingCalculator;
    [SerializeField] BattingInitialSpeedCalculator _initialSpeedCalculator;
    [SerializeField] BattingResultDataManager _resultDataManager;
    [SerializeField] AdvancedTrajectoryCalculator _trajectoryCalculator;
    [SerializeField] HomeRunChecker _homerunChecker;
    [SerializeField] BallJudge _ballJudge;
    [SerializeField] BatterAnimationEvents _batterAnimEvent;

    [SerializeField, Header("アニメーションがボールが当たるタイミングまでかかる時間")] float _swingAnimTime;

    private List<IBattingCallObserver> _observers = new List<IBattingCallObserver>();

    public IEnumerator BallMoveCoroutine { get; private set; }

    private Vector3 a;

    private Vector3 _landingPoint;

    public bool CanStartInput { get; private set; } = true;

    public Action _startReplay;
    public Action _endReplay;

    [Header("デバッグ用")]
    public bool InputTime;
    public bool BallPosition;
    public bool BallCorePosition;
    public bool DistanceFromCore;
    public bool TimingAccuracy;
    public bool AccuracyEvaluation;

    private void Start()
    {
        _startReplay += () =>
        {
            _ballControl.StopBall();
            CanStartInput = false;
            
            StartCoroutine(BallMoveCoroutine);
        };
        _endReplay += () =>
        {
            Debug.Log("リプレイ終了");
            CanStartInput = true;
            ResetData();
        };
    }

    public void RegisterObserver(IBattingCallObserver observer)
    {
        if (!_observers.Contains(observer))
        {
            _observers.Add(observer);
        }
    }


    public void StartInput()
    {
        foreach (var observer in _observers)
        {
            observer.BattingCall();
        }
        _resultDataManager.InputData = InputData();

        // Input時のデータをもとに初速を計算
        _resultDataManager.ResultData = _initialSpeedCalculator.InitialSpeedCalculate(_resultDataManager.InputData);

        if (_resultDataManager.InputData.Accuracy != AccuracyType.Miss) // ボールがバットに当たった場合
        {
            // 投球の処理を停止
            //_ballControl.StopBall();

            // 弾道の計算
            _resultDataManager.TrajectoryData = _trajectoryCalculator.TrajectoryCalculate(
                _resultDataManager.ResultData, _resultDataManager.InputData, out Vector3 landingPoint, out float flightTime);
            _landingPoint = landingPoint;

            if (_homerunChecker.HomeRunCheck(landingPoint)) _resultDataManager.ResultData.HittingType = HitType.HomeRun;

            Debug.Log($"打球の落下点: {landingPoint}, 飛行時間: {flightTime}秒");
            Debug.Log($"打球タイプ: {_resultDataManager.ResultData.HittingType}");

            // 打球の種類に応じた処理
            if (_resultDataManager.ResultData.HittingType == HitType.FoulBall) _ballJudge.FoulBall();
            else _ballJudge.Hit();
        }
        else　 // ボールがバットに当たらなかった場合
        {
            _resultDataManager.TrajectoryData = new TrajectoryData();
            _resultDataManager.TrajectoryData.hitType = TrajectoryData.HitType.Miss;
        }
    }

    /// <summary>
    /// 一度打球を動かした後空振りしても勝手に以前の打球を再生しないようにするための設定
    /// </summary>
    public void ResetData()
    {
        _resultDataManager.InputData = null;
        _resultDataManager.TrajectoryData.TrajectoryPoints.Clear();
        _landingPoint = Vector3.zero;
        _resultDataManager.ResultData = null;
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            //StartInput();
            _rePlayManager.ClickTimeSet(_ballControl.MoveBallTime);
            //StartCoroutine(_batterAnimEvent.PositionReset());
        }

        if (Input.GetMouseButtonDown(1))
        {
            _gameStateManager.SetState(GameState.Batting);
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

        // スイングしたアニメーションでボールが当たるタイミングまで進めたボールの座標で計算する
        inputData.BallPosition = _ballControl.BezierPoint(_ballControl.MoveBallTime + _swingAnimTime);
        a = inputData.BallPosition;
        inputData.DistanceFromCore = Vector3.Distance(inputData.BallPosition, inputData.AtCorePosition);

        // 精度計算
        inputData.TimingAccuracy = _timingCalculator.BattingTimingCalsulate(_ballControl.MoveBallTime, out AccuracyType accuracyType);
        inputData.Accuracy = accuracyType;

        // プレイヤー設定
        inputData.BatterType = _currentBatterType.Type;

        return inputData;
    }

    private void OnDrawGizmos()
    {
        if (_resultDataManager.ResultData != null)
            Gizmos.DrawLine(Vector3.zero, _resultDataManager.ResultData.ActualDirection);

        Gizmos.DrawSphere(a, 0.1f);
    }
}