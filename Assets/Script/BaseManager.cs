using UnityEngine;

public enum BaseId { First, Second, Third, Home }

public class BaseManager : MonoBehaviour
{
    [SerializeField] private Transform first;
    [SerializeField] private Transform second;
    [SerializeField] private Transform third;
    [SerializeField] private Transform home;

    public Vector3 GetBasePosition(BaseId baseId)
    {
        return baseId switch
        {
            BaseId.First => first.position,
            BaseId.Second => second.position,
            BaseId.Third => third.position,
            BaseId.Home => home.position,
            _ => Vector3.zero
        };
    }
}
