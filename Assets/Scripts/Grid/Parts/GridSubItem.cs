using Unity.Entities;
using Unity.Mathematics;

public struct GridSubItem : IComponentData
{
    public int3 RelativeCell;
}