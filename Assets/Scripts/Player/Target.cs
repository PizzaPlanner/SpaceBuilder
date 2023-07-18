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
}