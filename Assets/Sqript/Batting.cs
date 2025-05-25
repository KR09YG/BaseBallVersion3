using UnityEngine;

public class Batting : MonoBehaviour
{
    [SerializeField] private BattingCalculation _battingCalculation;
    [SerializeField] private Rigidbody _ballRb;
    [SerializeField] private Transform _batCoreTransform;
    [SerializeField] private BallControl _ballControl;
    [SerializeField] private float _swingInterval;
    private float _timer = 0;

    private void Update()
    {
        _timer += Time.deltaTime;

        if (Input.GetMouseButtonDown(0) && _timer > _swingInterval)
        {
            Debug.Log("Swing");
            float timing = _battingCalculation.CalculatePositionBasedTiming(
                _ballControl.transform.position, _batCoreTransform.position);
            Debug.Log(timing);

            if (timing != 0)
            {
                Vector3 battingDirection = _battingCalculation.CalculateBattingDirection(
                    _ballControl.transform.position,
                    _batCoreTransform.position,
                    timing,
                    _battingCalculation.CurrentType
                    );

                Debug.DrawRay(_ballRb.position, battingDirection * 5f, Color.red, 1f);
                _ballControl.StopBall();
                _ballRb.angularVelocity = Vector3.zero;
                _ballRb.linearVelocity = Vector3.zero;
                _ballControl.enabled = false;
                _ballRb.AddForce(battingDirection, ForceMode.Impulse);
            }
            _timer = 0;
        }
    }
}
