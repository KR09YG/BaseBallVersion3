using UnityEngine;

public static class DefenseThrowDecisionCalculator
{
    public static BaseType ThrowDicision()
    {
        // 現在は常にファーストに投げるように固定
        return BaseType.FirstBase;
    }
}
