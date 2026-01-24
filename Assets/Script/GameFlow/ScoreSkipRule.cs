using System;
using UnityEngine;

[Serializable]
public class ScoreSkipRule
{
    [Tooltip("trueなら試合状況に応じて得点を変動させる")]
    public bool UseDynamicScore = false;

    [Min(0)]
    [Tooltip("UseDynamicScore=false のときにそのまま加算される点")]
    public int BaseRuns = 0;
}
