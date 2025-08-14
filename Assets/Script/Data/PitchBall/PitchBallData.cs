using UnityEngine;

[CreateAssetMenu]
public class PitchBallData : ScriptableObject
{
    [Header("����")] public string Name;
    [Header("��ԓ_�P")] public Vector2 ControlPoint1;
    [Range(0f, 1f)] public float PathControlRatio1;
    [Header("��ԓ_�Q")] public Vector2 ControlPoint2;
    [Range(0f, 1f)] public float PathControlRatio2;
    [Header("�I�_�ւ̓��B����")] public float Time;
}
