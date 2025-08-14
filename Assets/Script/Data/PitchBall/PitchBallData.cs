using UnityEngine;

[CreateAssetMenu]
public class PitchBallData : ScriptableObject
{
    [Header("球種")] public string Name;
    [Header("補間点１")] public Vector2 ControlPoint1;
    [Range(0f, 1f)] public float PathControlRatio1;
    [Header("補間点２")] public Vector2 ControlPoint2;
    [Range(0f, 1f)] public float PathControlRatio2;
    [Header("終点への到達時間")] public float Time;
}
