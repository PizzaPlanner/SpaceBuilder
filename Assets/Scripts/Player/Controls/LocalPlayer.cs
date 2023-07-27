using Unity.Entities;

public struct LocalPlayer : IComponentData { }

public struct LocalShip : IComponentData
{
    public Entity Controller;
}
