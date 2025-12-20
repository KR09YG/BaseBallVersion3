using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
public class Ball : MonoBehaviour
{
    [SerializeField] private bool _enableSpin = true;
    [SerializeField] private float _spinSpeedMultiplier = 1.0f;

    private List<Vector3> _trajectory;
    private PitchPreset _pitchPreset;
    private float _trajectoryProgress = 0f;
    private float _trajectorySpeed = 1.0f;
    private bool _isMoving = false;
    private bool _hasReachedTarget = false;
    private Vector3 _finalPosition;
    private Rigidbody _rigidbody;

    public event System.Action<Ball> OnBallReachedTarget;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _rigidbody.useGravity = false;
        _rigidbody.isKinematic = true;
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

    public void StartMoving()
    {
        _isMoving = true;
        _hasReachedTarget = false;
    }

    private void Update()
    {
        if (_hasReachedTarget)
        {
            return;
        }

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
        if (_hasReachedTarget)
        {
            transform.position = _finalPosition;
        }
    }

    private void MoveAlongTrajectory()
    {
        _trajectoryProgress += _trajectorySpeed * Time.deltaTime;

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
        float degreesPerSecond = spinRate * 360f / 60f * _spinSpeedMultiplier;
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
}