using Unity.Entities;
using Unity.Jobs;
using Random = Unity.Mathematics.Random;

public partial struct StarSectorLoadJob : IJob
{
    public PendingSectors pendingSectors;
    public Entity prefab;
    public EntityCommandBuffer ecb;
    
    public void Execute()
    {
        while (pendingSectors.LightSectors.ToLoad.TryDequeue(out var newSector))
        {
            var isLoaded = GalaxyTools.GetLightSectorType(newSector, out var _) == LightSectorType.Empty ||
                           pendingSectors.LightSectors.Contains(newSector);
            if(isLoaded)
                continue;

            var sectorData = GalaxyTools.GetLightSectorData(newSector);
            var starSectors = sectorData.CalculateStarSectors;
            for (byte i = 0; i < sectorData.NumberOfStars; i++)
            {
                var ltg = newSector;
                ltg.StarSector = starSectors[i];
                GenerateStar(ltg);
                pendingSectors.StarSectors.Load(ltg);
            }
            pendingSectors.LightSectors.Load(newSector);
        }
    }

    void GenerateStar(LocalToGalaxy ltg)
    {
        var data = GalaxyTools.GetStarSectorData(ltg);
        var random = new Random(data.StarGeneratorSeed);
        random.NextFloat();
        
        var size = GalaxyTools.STAR_SIZE_MAX * random.NextFloat(0.3f, 1f);
        var colorTemperature = random.NextFloat(-2.1f, 2.06f);
        var brightness = random.NextFloat(200 + 40 * colorTemperature, 300);

        var star = ecb.Instantiate(prefab);
        ecb.SetComponent(star, ltg);
        ecb.SetComponent(star, new CelestialBody { Size = size });
        ecb.SetComponent(star, new URPStarTemperature { Value = colorTemperature });
        ecb.SetComponent(star, new URPStarFlareIntensity { Value = brightness });
    }
}