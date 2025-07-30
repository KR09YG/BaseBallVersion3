using UnityEngine;

[CreateAssetMenu]
public class PhysicsData : ScriptableObject
{
    [Header("�d��")] public float Gravity;
    [Header("��C��R")] public float AirResistance;
    [Header("�����W��"), Range(0, 1)] public float ReboundCoefficient;
}
