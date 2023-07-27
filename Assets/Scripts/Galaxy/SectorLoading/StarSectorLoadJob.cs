using Galaxy;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

public partial struct StarSectorLoadJob : IJob
{
    public PendingSectors pendingSectors;
    public Entity prefab;
    public EntityCommandBuffer ecb;

    const double MAX_LIGHT_EMISSION_DISTANCE = LocalToGalaxy.DISTANCE_PER_STARSECTOR * 20;
    const double MAX_LIGHT_EMISSION_DISTANCE_SQ = MAX_LIGHT_EMISSION_DISTANCE * MAX_LIGHT_EMISSION_DISTANCE;
    
    public void Execute()
    {
        while (pendingSectors.LightSectors.ToLoad.TryDequeue(out var newSector))
        {
            var isLoaded = pendingSectors.LightSectors.Contains(newSector);
            if(isLoaded)
                continue;

            var sectorData = GalaxyTools.GetLightSectorData(newSector);
            var starSectors = sectorData.CalculateStarSectors;
            for (byte i = 0; i < sectorData.NumberOfStars; i++)
            {
                var ltg = new LocalToGalaxy
                {
                    LightSector = newSector.LightSector,
                    StarSector = starSectors[i],
                    PlanetSector = GalaxyTools.StarSectorCenter
                };
                GenerateStar(ltg, newSector);
            }
            pendingSectors.LightSectors.Load(newSector);
        }
    }

    void GenerateStar(LocalToGalaxy ltg, LocalToGalaxy registerInSector)
    {
        var data = GalaxyTools.GetStarSectorData(ltg);
        var random = new Random(data.StarGeneratorSeed);
        random.NextFloat();

        var hue = random.NextFloat();
        var saturation = random.NextFloat(0.7f, 0.9f);
        var colorRgb = Color.HSVToRGB(hue, saturation, 1);
        var lightRgb = Color.HSVToRGB(hue, saturation * 0.3f, 1);
        var size = data.Type switch
        {
            StarSectorType.Star => GalaxyTools.STAR_SIZE_MAX * random.NextFloat(0.4f, 1f),
            StarSectorType.BigStar => GalaxyTools.GIANTSTAR_SIZE_MAX * random.NextFloat(0.3f, 1f),
            StarSectorType.BlackHole => GalaxyTools.STAR_SIZE_MAX * random.NextFloat(0.1f, 0.2f),
        };
        var brightness = data.Type switch
        {
            StarSectorType.Star => random.NextFloat(3, 20),
            StarSectorType.BigStar => random.NextFloat(30, 50),
            StarSectorType.BlackHole => 0,
        };
        var colorStar = new float4(colorRgb.r, colorRgb.g, colorRgb.b, brightness * size);
        var colorLight = new float4(lightRgb.r, lightRgb.g, lightRgb.b, brightness * size);
        var star = ecb.Instantiate(prefab);
        ecb.SetComponent(star, ltg);
        ecb.SetComponent(star, new CelestialBody { Size = size });
        ecb.SetComponent(star, new URPStarFlareIntensity { Value = brightness });
        ecb.SetComponent(star, new URPStarColor { Value = colorStar });
        ecb.SetComponent(star, new GenerateIntoSector { LinkedSector = registerInSector });
        ecb.SetComponent(star, new LightEmitter { Color = colorLight, MaxDistanceSq = MAX_LIGHT_EMISSION_DISTANCE_SQ });
    }
}