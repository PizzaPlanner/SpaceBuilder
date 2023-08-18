using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;
using Random = Unity.Mathematics.Random;

public partial struct PlanetSectorLoadJob : IJob
{
    public PendingSectors pendingSectors;
    public Entity prefab;
    public EntityCommandBuffer ecb;
    
    public void Execute()
    {
        while (pendingSectors.StarSectors.ToLoad.TryDequeue(out var newSector))
        {
            var isLoaded = pendingSectors.StarSectors.Contains(newSector);
            if(isLoaded)
                continue;

            var sectorData = GalaxyTools.GetStarSectorData(newSector);
            var planetSectors = sectorData.CalculatePlanetSectors;
            for (byte i = 0; i < sectorData.NumberOfPlanets; i++)
            {
                var ltg = new LocalToGalaxy
                {
                    LightSector = newSector.LightSector,
                    StarSector = newSector.StarSector,
                    PlanetSector = planetSectors[i].PlanetSector,
                    LocalSector = GalaxyTools.PlanetSectorCenter
                };
                GeneratePlanet(ltg, newSector);
            }
            pendingSectors.StarSectors.Load(newSector);
        }
    }

    void GeneratePlanet(LocalToGalaxy ltg, LocalToGalaxy registerInSector)
    {
        var data = GalaxyTools.GetPlanetSectorData(ltg);
        var random = new Random(data.Seed);
        
        var colorRgb = Color.HSVToRGB(random.NextFloat(), random.NextFloat(0.6f, 1f), random.NextFloat(0.8f, 1));
        var color = new float4(colorRgb.r, colorRgb.g, colorRgb.b, 1);
        var size = GalaxyTools.PLANET_SIZE_MAX * random.NextFloat(0.3f, 1f);
        var planet = ecb.Instantiate(prefab);
        ecb.SetComponent(planet, ltg);
        ecb.SetComponent(planet, new CelestialBody { Size = size });
        ecb.SetComponent(planet, new URPMaterialPropertyBaseColor { Value = color });
        ecb.SetComponent(planet, new GenerateIntoSector { LinkedSector = registerInSector });
    }
}