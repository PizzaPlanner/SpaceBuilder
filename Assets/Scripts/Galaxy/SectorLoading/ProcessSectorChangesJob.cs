using Galaxy.SectorLoading;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public partial struct ProcessSectorChangesJob : IJobEntity
{
    public PendingSectors pendingSectors;
    public LoadSectorSystem.SectorLoadSettings sectorLoadSettings;
    void Execute(Entity e, ref CanTravelSectors cts, in LocalToGalaxy ltg)
    {
        var previous = cts.PreviousSector;
        cts.PreviousSector = ltg;
        cts.SectorChanged = SectorUpdateType.None;
        if (previous == ltg)
            return;
        
        if (cts.CurrentTravelType != SectorChangeType.FTL)
        {
            var maxDistance = sectorLoadSettings.MaxLoadedPlanetSectorDistance;
            for (int x = -maxDistance; x <= maxDistance; x++)
            for (int y = -maxDistance; y <= maxDistance; y++)
            for (int z = -maxDistance; z <= maxDistance; z++)
            {
                if(math.lengthsq(new int3(x, y, z)) > maxDistance * maxDistance)
                    continue;
                
                var newLtg = ltg + new LocalToGalaxy { PlanetSector = new int3(x, y, z) };
                pendingSectors.PlanetSectors.Enqueue(newLtg);
            }
        }
        if (math.any(ltg.LightSector != previous.LightSector))
        {
            var maxDistance = sectorLoadSettings.MaxLoadedLightSectorDistance;
            for (int x = -maxDistance; x < maxDistance; x++)
            for (int y = -maxDistance; y < maxDistance; y++)
            for (int z = -maxDistance; z < maxDistance; z++)
            {
                if(math.lengthsq(new int3(x, y, z)) > maxDistance * maxDistance)
                    continue;
                
                var newLtg = ltg + new LocalToGalaxy { LightSector = new int3(x, y, z) };
                pendingSectors.LightSectors.Enqueue(newLtg);
            } 
            cts.SectorChanged |= SectorUpdateType.Light;
        }
        if(math.any(ltg.StarSector != previous.StarSector))
        {
            var starSectors = GalaxyTools.GetLightSectorData(ltg).CalculateStarSectors;

            var maxDistance = sectorLoadSettings.MaxLoadedStarSectorDistance;
            for (int x = -maxDistance; x < maxDistance; x++)
            for (int y = -maxDistance; y < maxDistance; y++)
            for (int z = -maxDistance; z < maxDistance; z++)
            {
                if(math.lengthsq(new int3(x, y, z)) > maxDistance * maxDistance)
                    continue;

                var starSec = new int3(x, y, z);
                foreach (var sector in starSectors)
                {
                    var newLtg = ltg + new LocalToGalaxy { StarSector = starSec };
                    if (math.all(newLtg.StarSector == sector))
                        pendingSectors.StarSectors.Enqueue(newLtg);
                }
            } 
            cts.SectorChanged |= SectorUpdateType.Star;
        }
        pendingSectors.entitiesPerSector.Add(ltg, e);
        pendingSectors.entitiesPerSector.Remove(previous, e);
    }
}