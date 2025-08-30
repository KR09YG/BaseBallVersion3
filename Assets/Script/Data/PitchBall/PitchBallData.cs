using UnityEngine;

[CreateAssetMenu(fileName = "PitchBallData", menuName = "Scriptable Objects/PitchBallData")]
public class PitchBallData : ScriptableObject
{
    public string Name;
    public Vector3 ControlPoint;
    [SerializeField, Range(0f, 1f)] public float PathControlRatio1;
    public Vector3 ControlPoint2;
    [SerializeField, Range(0f, 1f)] public float PathControlRatio2;
    public float Time;
}
