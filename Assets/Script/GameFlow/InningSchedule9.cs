using UnityEngine;

[CreateAssetMenu(menuName = "Baseball/Game/InningSchedule9")]
public class InningSchedule9 : ScriptableObject
{
    public const int Innings = 9;

    [SerializeField] private HalfInningPlan[] _top = new HalfInningPlan[Innings];
    [SerializeField] private HalfInningPlan[] _bottom = new HalfInningPlan[Innings];

    public HalfInningPlan GetTop(int inningIndex0)
        => _top[inningIndex0];

    public HalfInningPlan GetBottom(int inningIndex0)
        => _bottom[inningIndex0];

    public HalfInningPlan Get(int inningNumber1, bool isTop)
    {
        int idx = inningNumber1 - 1;
        if ((uint)idx >= Innings) throw new System.ArgumentOutOfRangeException(nameof(inningNumber1));
        return isTop ? _top[idx] : _bottom[idx];
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureArray(ref _top, Innings);
        EnsureArray(ref _bottom, Innings);

        // null—v‘f‚ğ–„‚ß‚éiInspector‚ÅŒ©‚â‚·‚­‚·‚éj
        for (int i = 0; i < Innings; i++)
        {
            if (_top[i] == null) _top[i] = new HalfInningPlan();
            if (_bottom[i] == null) _bottom[i] = new HalfInningPlan();
        }
    }

    private static void EnsureArray<T>(ref T[] array, int length)
    {
        if (array == null || array.Length != length)
            System.Array.Resize(ref array, length);
    }
#endif
}
