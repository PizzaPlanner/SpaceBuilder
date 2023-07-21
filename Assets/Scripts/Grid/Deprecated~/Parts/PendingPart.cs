using Unity.Entities;

public struct PendingPart : IComponentData
{
    public Entity Grid;
    public BuildingPartInfo Data;
}