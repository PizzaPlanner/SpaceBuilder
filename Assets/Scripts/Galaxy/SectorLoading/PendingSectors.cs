using System;
using Unity.Collections;
using Unity.Entities;

public struct PendingSectors : IComponentData, IDisposable
{
    public SectorLoadState LightSectors;
    public SectorLoadState StarSectors;
    public SectorLoadState PlanetSectors;
    public NativeParallelMultiHashMap<LocalToGalaxy, Entity> entitiesPerSector;

    public void Allocate()
    {
        StarSectors.Allocate();
        PlanetSectors.Allocate();
        LightSectors.Allocate();
        entitiesPerSector = new NativeParallelMultiHashMap<LocalToGalaxy, Entity>(0, Allocator.Persistent);
    }
        
    public void Dispose()
    {
        StarSectors.Dispose();
        PlanetSectors.Dispose();
        LightSectors.Dispose();
        entitiesPerSector.Dispose();
    }
}