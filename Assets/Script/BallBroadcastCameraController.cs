using UnityEngine;

[RequireComponent(typeof(Camera))]
public class BallBroadcastCameraController : MonoBehaviour
{
    [Header("Target")]
    private Transform _ball;

    [SerializeField] private OnBallSpawnedEvent _ballSpawnedEvent;

    [Header("Tripod (fixed position)")]
    [SerializeField] private Transform _tripod;

    [Header("Rotation (human-like)")]
    [SerializeField] private float _yawSmoothTime = 0.18f;
    [SerializeField] private float _pitchSmoothTime = 0.18f;
    [SerializeField] private float _maxYawSpeedDeg = 220f;
    [SerializeField] private float _maxPitchSpeedDeg = 160f;

    [Header("Lead (look ahead)")]
    [SerializeField] private float _leadSeconds = 0.10f;

    [Header("Zoom (FOV)")]
    [SerializeField] private float _minFov = 18f;
    [SerializeField] private float _maxFov = 55f;
    [SerializeField] private float _zoomSmoothTime = 0.25f;
    [SerializeField] private float _referenceDistance = 35f;
    [SerializeField] private float _zoomSensitivity = 0.9f;

    private Camera _cam;

    private float _yawVel;
    private float _pitchVel;
    private float _zoomVel;

    private Vector3 _lastBallPos;
    private Vector3 _ballVel;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
    }

    private void OnEnable()
    {
        _ballSpawnedEvent?.UnregisterListener(SetTarget);
        _ballSpawnedEvent?.RegisterListener(SetTarget);
    }

    private void OnDisable()
    {
        _ballSpawnedEvent?.UnregisterListener(SetTarget);
    }

    public void SetTarget(GameObject ball)
    {
        if (ball == null)
        {
            _ball = null;
            return;
        }

        _ball = ball.transform;
        _lastBallPos = _ball.position;
        _ballVel = Vector3.zero;
    }

    private void LateUpdate()
    {
        if (_ball == null) return;

        if (_tripod != null)
            transform.position = _tripod.position;

        Vector3 pos = _ball.position;

        _ballVel = (pos - _lastBallPos) / Mathf.Max(Time.deltaTime, 0.0001f);
        _lastBallPos = pos;

        Vector3 aimPos = pos + _ballVel * _leadSeconds;

        Vector3 dir = aimPos - transform.position;
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        Vector3 targetEuler = targetRot.eulerAngles;

        float newYaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetEuler.y, ref _yawVel, _yawSmoothTime, _maxYawSpeedDeg);
        float newPitch = Mathf.SmoothDampAngle(transform.eulerAngles.x, targetEuler.x, ref _pitchVel, _pitchSmoothTime, _maxPitchSpeedDeg);
        transform.rotation = Quaternion.Euler(newPitch, newYaw, 0f);

        float dist = Vector3.Distance(transform.position, pos);
        float ratio = dist / Mathf.Max(0.01f, _referenceDistance);

        float desiredFov = Mathf.Lerp(_maxFov, _minFov, Mathf.Clamp01((ratio - 0.5f) * _zoomSensitivity));
        desiredFov = Mathf.Clamp(desiredFov, _minFov, _maxFov);

        _cam.fieldOfView = Mathf.SmoothDamp(_cam.fieldOfView, desiredFov, ref _zoomVel, _zoomSmoothTime);
    }
}
