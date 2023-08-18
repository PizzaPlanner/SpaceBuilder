using Unity.Entities;

namespace Galaxy.SectorLoading
{
    public struct CanTravelSectors : IComponentData
    {
        public LocalToGalaxy PreviousSector;
        public SectorChangeType CurrentTravelType;
    }

    public enum SectorChangeType
    {
        Normal,
        FTL,
        Teleport,
        Spawn
    }
}