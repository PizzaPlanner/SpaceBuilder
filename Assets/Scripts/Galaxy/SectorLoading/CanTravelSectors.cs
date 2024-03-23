using System;
using Unity.Entities;

namespace Galaxy.SectorLoading
{
    public struct CanTravelSectors : IComponentData
    {
        public LocalToGalaxy PreviousSector;
        public SectorChangeType CurrentTravelType;
        public SectorUpdateType SectorChanged;
    }

    [Flags]
    public enum SectorUpdateType : byte
    {
        None = 0,
        Local = 1,
        Planet = 2 | Local,
        Star = 4 | Planet,
        Light = 8 | Star 
    }

    public enum SectorChangeType
    {
        Normal,
        FTL,
        Teleport,
        Spawn
    }
}