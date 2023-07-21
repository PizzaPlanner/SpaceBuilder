using Unity.Entities;

namespace Galaxy.SectorLoading
{
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
}