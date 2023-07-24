using Unity.Entities;
using Unity.Mathematics;


public struct Target : IComponentData
{
    public Entity Value;
    public Entity PreviousValue;
    public float3 Hit;
    public float3 PreviousHit;
    public GridHitInfo GridHit;
    public bool CanInteract;
    public bool CanConstruct;
}

public struct GridHitInfo
{
    public int3 Normal;
    public int3 Cell;
    public Entity Grid;
    
    public static bool operator ==(GridHitInfo lhs, GridHitInfo rhs) => 
        lhs.Grid == rhs.Grid &&
        math.all(lhs.Cell == rhs.Cell) &&
        math.all(lhs.Normal == rhs.Normal);

    public static bool operator !=(GridHitInfo lhs, GridHitInfo rhs) => !(lhs == rhs);
}