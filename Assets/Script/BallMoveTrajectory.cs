using System.Collections.Generic;
using UnityEngine;

public abstract class BallMoveTrajectory : MonoBehaviour
{
    [Header("ã§í ")]
    [SerializeField] protected float _visualSpeedMultiplier = 1.0f;
    [SerializeField] protected bool _enableSpin = true;
    [SerializeField] protected float _spinSpeedMultiplier = 1.0f;

    [Header("ãOìπçƒê∂ê›íË")]
    [SerializeField] protected float _trajectoryDeltaTime = 0.01f;

    protected float _elapsedTime;

    protected List<Vector3> _trajectory;
    protected float _trajectoryProgress;

    protected bool _isMoving;

    protected virtual void Update()
    {
        if (!_isMoving || _trajectory == null) return;
        MoveAlongTrajectory();
        if (_enableSpin) ApplySpin();
    }

    protected void MoveAlongTrajectory()
    {
        _elapsedTime += Time.deltaTime * _visualSpeedMultiplier;

        int index = Mathf.FloorToInt(_elapsedTime / _trajectoryDeltaTime);

        if (index >= _trajectory.Count - 1)
        {
            transform.position = _trajectory[^1];
            OnReachedEnd();
            return;
        }

        float t = (_elapsedTime - index * _trajectoryDeltaTime) / _trajectoryDeltaTime;

        transform.position = Vector3.Lerp(_trajectory[index], _trajectory[index + 1], t);
    }

    protected abstract void ApplySpin();
    protected abstract void OnReachedEnd();
}
