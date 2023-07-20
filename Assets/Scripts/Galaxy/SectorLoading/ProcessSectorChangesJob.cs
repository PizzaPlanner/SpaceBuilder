using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public partial struct ProcessSectorChangesJob : IJobEntity
{
    public PendingSectors pendingSectors;
    public LoadSectorSystem.SectorLoadSettings sectorLoadSettings;
    void Execute(Entity e, in SectorChange sc)
    {
        if (sc.ChangeType != SectorChangeType.FTL)
        {
            var maxDistance = sectorLoadSettings.MaxLoadedPlanetSectorDistance;
            for (int x = -maxDistance; x <= maxDistance; x++)
            for (int y = -maxDistance; y <= maxDistance; y++)
            for (int z = -maxDistance; z <= maxDistance; z++)
            {
                if(math.abs(x + y + z) > maxDistance)
                    continue;
                
                var newLtg = sc.New + new LocalToGalaxy { PlanetSector = new int3(x, y, z) };
                pendingSectors.PlanetSectors.Enqueue(newLtg);
            }
        }
        if (math.any(sc.New.LightSector != sc.Previous.LightSector))
        {
            var maxDistance = sectorLoadSettings.MaxLoadedLightSectorDistance;
            for (int x = -maxDistance; x < maxDistance; x++)
            for (int y = -maxDistance; y < maxDistance; y++)
            for (int z = -maxDistance; z < maxDistance; z++)
            {
                if(math.abs(x + y + z) > maxDistance)
                    continue;
                
                var newLtg = sc.New + new LocalToGalaxy { LightSector = new int3(x, y, z) };
                pendingSectors.LightSectors.Enqueue(newLtg);
            } 
        }
        if(math.any(sc.New.StarSector != sc.Previous.StarSector))
        {
            var starSectors = GalaxyTools.GetLightSectorData(sc.New).CalculateStarSectors;

            var maxDistance = sectorLoadSettings.MaxLoadedStarSectorDistance;
            for (int x = -maxDistance; x < maxDistance; x++)
            for (int y = -maxDistance; y < maxDistance; y++)
            for (int z = -maxDistance; z < maxDistance; z++)
            {
                if(math.abs(x + y + z) > maxDistance)
                    continue;

                var starSec = new int3(x, y, z);
                foreach (var sector in starSectors)
                {
                    var newLtg = sc.New + new LocalToGalaxy { StarSector = starSec };
                    if (math.all(newLtg.StarSector == sector))
                        pendingSectors.StarSectors.Enqueue(newLtg);
                }
            } 
        }
        pendingSectors.entitiesPerSector.Add(sc.New, e);
        pendingSectors.entitiesPerSector.Remove(sc.Previous, e);
    }
}