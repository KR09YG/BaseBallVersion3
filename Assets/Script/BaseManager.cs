using UnityEngine;

public enum BaseId { None, First, Second, Third, Home }

public class BaseManager : MonoBehaviour
{
    [SerializeField] private Transform _first;
    [SerializeField] private Transform _second;
    [SerializeField] private Transform _third;
    [SerializeField] private Transform _home;

    public Vector3 GetBasePosition(BaseId baseId)
    {
        return baseId switch
        {
            BaseId.First => _first.position,
            BaseId.Second => _second.position,
            BaseId.Third => _third.position,
            BaseId.Home => _home.position,
            _ => Vector3.zero
        };
    }
}
