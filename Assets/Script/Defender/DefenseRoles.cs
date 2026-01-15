using System.Collections.Generic;

public struct DefenseRoles
{
    public FielderController CutoffMan;
    public Dictionary<BaseType, FielderController> BaseCovers;
}

public enum BaseType
{
    FirstBase,
    SecondBase,
    ThirdBase,
    HomePlate
}