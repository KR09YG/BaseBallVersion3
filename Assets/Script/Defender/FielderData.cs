
using System;
using UnityEngine;

[Serializable]
public class FielderData
{
    public PositionType Position;
    public PositionGroupType PositionGroupType;
    [Tooltip("Task—p‚Ì‚½‚ß1•b = 1000")]
    public int ThrowDelay = 1000;      
    public float MoveSpeed;        // m/s
    public float ReactionTime;     // •b
    public float CatchHeight;      // •ß‹…‰Â”\‚‚³
    public float ThrowSpeed;       // m/s
}

