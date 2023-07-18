using Unity.Entities;

public struct CanTravelSectors : IComponentData
{
    public LocalToGalaxy PreviousSector;
    public SectorChangeType CurrentTravelType;
    public double CurrentSpeed;
}

public enum SectorChangeType
{
    Normal,
    FTL,
    Teleport,
    Spawn
}

public struct SectorChange : IComponentData
{
    public Entity Entity;
    public LocalToGalaxy Previous;
    public LocalToGalaxy New;
    public SectorChangeType ChangeType;
}