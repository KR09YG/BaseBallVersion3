using UnityEngine;

[CreateAssetMenu]
public class PhysicsData : ScriptableObject
{
    [Header("d—Í")] public float Gravity;
    [Header("‹ó‹C’ïR")] public float AirResistance;
    [Header("”½”­ŒW”"), Range(0, 1)] public float ReboundCoefficient;
}
