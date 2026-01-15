using UnityEngine;
using System;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
public class Ball : MonoBehaviour
{
    [Header("スピン設定")]
    [SerializeField] private bool _enableSpin = true;
    [SerializeField] private float _spinSpeedMultiplier = 1.0f;

    [Header("速度調整")]
    [Tooltip("1.0 = 通常速度, 0.5 = 半分の速度, 2.0 = 2倍速")]
    [SerializeField] private float _visualSpeedMultiplier = 1.0f;

    [SerializeField] private SwingEvent _swingEvent;
    [SerializeField] private BattingHitEvent _battingHitEvent;

    private List<Vector3> _trajectory;
    private PitchPreset _pitchPreset;
    private float _trajectoryProgress = 0f;
    private float _trajectorySpeed = 1.0f;
    private bool _isMoving = false;
    private bool _hasReachedTarget = false;
    private Vector3 _finalPosition;
    private Rigidbody _rigidbody;

    public event Action<Ball> OnBallReachedTarget;

    public List<Vector3> Trajectory => _trajectory;

    public float VisualSpeedMultiplier
    {
        get => _visualSpeedMultiplier;
        set => _visualSpeedMultiplier = Mathf.Max(0.1f, value);
    }

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _rigidbody.useGravity = false;
        _rigidbody.isKinematic = true;
        _swingEvent.RegisterListener(SwingEvent);
    }

    public void Initialize(List<Vector3> trajectory, PitchPreset preset)
    {
        _trajectory = trajectory;
        _pitchPreset = preset;
        _trajectoryProgress = 0f;
        _isMoving = false;
        _hasReachedTarget = false;

        if (_trajectory != null && _trajectory.Count > 0)
        {
            transform.position = _trajectory[0];
        }

        if (preset != null)
        {
            CalculateTrajectorySpeed(preset.Velocity);
        }

        Debug.Log($"[Ball] Initialize完了: Trajectory={trajectory?.Count}, VisualSpeed={_visualSpeedMultiplier}x");
    }

    public void StartMoving()
    {
        _isMoving = true;
        Debug.Log($"[Ball] StartMoving - Speed={_visualSpeedMultiplier}x");
    }

    public void ResetBall()
    {
        _isMoving = false;
        _hasReachedTarget = false;
        _trajectory = null;
        _pitchPreset = null;
        _trajectoryProgress = 0f;
        OnBallReachedTarget = null;
        transform.rotation = Quaternion.identity;
    }

    private void Update()
    {
        if (_hasReachedTarget) return;

        if (!_isMoving || _trajectory == null || _trajectory.Count == 0)
            return;

        MoveAlongTrajectory();

        if (_enableSpin && _pitchPreset != null)
        {
            ApplySpin();
        }
    }

    private void LateUpdate()
    {
        // ✅ 投球中（_isMoving が true）の場合のみ位置固定
        if (!_isMoving) return;

        if (_hasReachedTarget)
        {
            transform.position = _finalPosition;
        }
    }

    private void MoveAlongTrajectory()
    {
        _trajectoryProgress += _trajectorySpeed * _visualSpeedMultiplier * Time.deltaTime;

        float floatIndex = _trajectoryProgress * (_trajectory.Count - 1);
        int currentIndex = Mathf.FloorToInt(floatIndex);

        if (currentIndex >= _trajectory.Count - 1 || _trajectoryProgress >= 1.0f)
        {
            _finalPosition = _trajectory[_trajectory.Count - 1];
            transform.position = _finalPosition;
            ReachTarget();
            return;
        }

        float t = floatIndex - currentIndex;
        Vector3 currentPos = _trajectory[currentIndex];
        Vector3 nextPos = _trajectory[currentIndex + 1];

        transform.position = Vector3.Lerp(currentPos, nextPos, t);
    }

    private void ApplySpin()
    {
        Vector3 spinAxis = _pitchPreset.NormalizedSpinAxis;
        float spinRate = _pitchPreset.SpinRate;

        float degreesPerSecond = spinRate * 360f / 60f * _spinSpeedMultiplier * _visualSpeedMultiplier;
        transform.Rotate(spinAxis, degreesPerSecond * Time.deltaTime, Space.World);
    }

    private void CalculateTrajectorySpeed(float velocityMs)
    {
        if (_trajectory == null || _trajectory.Count < 2)
        {
            _trajectorySpeed = 1.0f;
            return;
        }

        float totalDistance = 0f;
        for (int i = 0; i < _trajectory.Count - 1; i++)
        {
            totalDistance += Vector3.Distance(_trajectory[i], _trajectory[i + 1]);
        }

        float timeToComplete = totalDistance / velocityMs;
        _trajectorySpeed = 1.0f / timeToComplete;
    }

    private void ReachTarget()
    {
        _isMoving = false;
        _hasReachedTarget = true;
        OnBallReachedTarget?.Invoke(this);
    }

    public void SetVisualSpeed(float multiplier)
    {
        _visualSpeedMultiplier = Mathf.Max(0.1f, multiplier);
        Debug.Log($"[Ball] 速度変更: {_visualSpeedMultiplier}x");
    }

    private void SwingEvent()
    {
        _isMoving = false;
        _battingHitEvent.RaiseEvent(this);
    }

    private void OnDestroy()
    {
        _swingEvent.UnregisterListener(SwingEvent);
    }
}