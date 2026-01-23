using UnityEngine;

[CreateAssetMenu(fileName = "New Pitch Preset", menuName = "Baseball/Pitch Preset", order = 0)]
public class PitchPreset : ScriptableObject
{
    private const float KMH_TO_MS = 1f / 3.6f;

    [Header("球種情報")]
    public string PitchName = "新しい変化球";

    [Header("回転パラメータ")]
    public Vector3 SpinAxis = Vector3.forward;

    [Range(0f, 3000f)]
    public float SpinRate = 2000f;

    [Header("速度パラメータ")]
    [Range(70f, 180f)]
    [SerializeField] private float _velocityKmh = 150f;

    [Header("物理パラメータ")]
    [Range(0f, 1f)]
    public float LiftCoefficient = 0.4f;

    [Header("ビジュアル設定")]
    public Color TrajectoryColor = Color.white;

    public float VelocityKmh
    {
        get => _velocityKmh;
        set => _velocityKmh = Mathf.Clamp(value, 70f, 180f);
    }

    public float Velocity => _velocityKmh * KMH_TO_MS;

    public Vector3 NormalizedSpinAxis => SpinAxis.normalized;

    public PitchParameters CreateParameters(Vector3 releasePoint, Vector3 targetPoint)
    {
        var parameters = new PitchParameters(releasePoint, targetPoint, Velocity)
        {
            SpinAxis = SpinAxis,
            SpinRate = SpinRate,
            LiftCoefficient = LiftCoefficient
        };

        parameters.Settings.StopPosition = targetPoint;
        parameters.Settings.StopAtTarget = true;

        return parameters;
    }
}