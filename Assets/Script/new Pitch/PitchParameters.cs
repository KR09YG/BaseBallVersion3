using UnityEngine;

/// <summary>
/// “Š‹…ƒpƒ‰ƒ[ƒ^
/// </summary>
[System.Serializable]
public class PitchParameters
{
    public Vector3 ReleasePoint;
    public Vector3 TargetPoint;
    public float Velocity = 40f;

    public Vector3 SpinAxis = Vector3.forward;
    public float SpinRate = 2000f;
    public float LiftCoefficient = 0.4f;

    public TrajectorySettings Settings = new TrajectorySettings();

    public PitchParameters(Vector3 releasePoint, Vector3 targetPoint, float velocity)
    {
        this.ReleasePoint = releasePoint;
        this.TargetPoint = targetPoint;
        this.Velocity = velocity;
        this.Settings.StopPosition = targetPoint;
    }
}