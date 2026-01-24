using System;

[Serializable]
public class HalfInningPlan
{
    public bool PlayThisHalf = false;

    // PlayThisHalf == true
    public DefenseSituation StartDefenseSituation;

    // PlayThisHalf == false
    public ScoreSkipRule SkipRule;
}