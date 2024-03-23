using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

public static partial class GalaxyTools
{
    private const float PERLIN_SECTOR_RESOLUTION = 0.0001f;
    public const float PLANET_SIZE_MAX = (float)LocalToGalaxy.DISTANCE_PER_PLANETSECTOR * 0.7f;
    public const float STAR_SIZE_MAX = (float)(LocalToGalaxy.DISTANCE_PER_STARSECTOR / 33);
    public const float GIANTSTAR_SIZE_MAX = (float)(LocalToGalaxy.DISTANCE_PER_STARSECTOR / 3);
    private const uint MIN_SECTORS_FROM_STAR_TO_PLANET = (uint)(3 * STAR_SIZE_MAX / LocalToGalaxy.DISTANCE_PER_PLANETSECTOR);
    private const uint MIN_SECTORS_BETWEEN_PLANETS = (int) (LocalToGalaxy.MAX_PLANETSECTOR * 0.02f);
    const float LIGHTSECTOR_MAX_DENSITY = 0.0005f;
    private const uint MAX_STARS_PER_LIGHTSECTOR = (int)(LIGHTSECTOR_MAX_DENSITY * LocalToGalaxy.MAX_STARSECTOR * LocalToGalaxy.MAX_STARSECTOR * LocalToGalaxy.MAX_STARSECTOR);

    public static float Perlin3D(in float3 xyz)
    {
        var intermediate = noise.cnoise(xyz);
        return math.clamp(intermediate * 2 + 0.5f, 0, 1);
    }

    public static readonly int3 StarSectorCenter = LocalToGalaxy.MAX_PLANETSECTOR >> 1;
    public static readonly int3 PlanetSectorCenter = LocalToGalaxy.MAX_LOCALSECTOR >> 1;

    public static uint GetSectorSeed(in LocalToGalaxy ltg)
    {
        return math.hash(new int3x4(ltg.LightSector, ltg.StarSector, ltg.PlanetSector, ltg.LocalSector));
    }
    
    #region SectorData

    public static float GetSectorStarFrequency(in LocalToGalaxy ltg)
    {
        return Perlin3D((float3)((double3)ltg.LightSector * PERLIN_SECTOR_RESOLUTION));
    }
    
    public static LightSectorData GetLightSectorData(in LocalToGalaxy ltg)
    {
        var lightLtg = new LocalToGalaxy
        {
            LightSector = ltg.LightSector
        };
        var data = new LightSectorData();
        var starFrequency = GetSectorStarFrequency(lightLtg);
        data.NumberOfStars = (byte)math.max(0, starFrequency * MAX_STARS_PER_LIGHTSECTOR);
        data.StarGenerationSeed = GetSectorSeed(lightLtg);

        return data;
    }

    public static Hash128 GetLightSectorCluster(in LocalToGalaxy ltg)
    {
        var selfFreq = GetSectorStarFrequency(ltg);
        var result = GetLightSectorCluster(ltg, selfFreq);
        var hash = new Hash128(new uint4(
            math.hash(result.xyzx),
            math.hash(result.yzxy),
            math.hash(result.zxyz),
            math.hash(result.yxzx)
            ));
        return hash; 
    }

    static int3 GetLightSectorCluster(in LocalToGalaxy ltg, in float starFrequency)
    {
        return ltg.LightSector;
    }

    public static StarSectorData GetStarSectorData(in LocalToGalaxy ltg)
    {
        var starLtg = new LocalToGalaxy
        {
            LightSector = ltg.LightSector,
            StarSector = ltg.StarSector
        };
        var random = new Random(GetSectorSeed(starLtg));
        var data = new StarSectorData();
        var bhTypeChance = random.NextFloat() * GetSectorStarFrequency(ltg) * GetSectorStarFrequency(ltg);
        var bigTypeChance = random.NextFloat();
        data.Type = bhTypeChance < 0.001f ? StarSectorType.BlackHole : 
                    bigTypeChance < 0.001f ? StarSectorType.BigStar : 
                    StarSectorType.Star;
        data.StarGeneratorSeed = random.NextUInt();
        data.NumberOfPlanets = data.Type switch
        {
            StarSectorType.Star => (byte)random.NextInt(9),
            _ => 0
        };

        return data;
    }
    
    public static PlanetSectorData GetPlanetSectorData(in LocalToGalaxy ltg)
    {
        var planetLtg = new LocalToGalaxy
        {
            LightSector = ltg.LightSector,
            StarSector = ltg.StarSector,
            PlanetSector = ltg.PlanetSector
        };
        var random = new Random(GetSectorSeed(planetLtg));
        var data = new PlanetSectorData();
        data.Type = PlanetSectorType.Planet;
        data.Seed = random.NextUInt();
        return data;
    }

    public static NativeArray<int3> GetStarSectorsOfLightSector(LightSectorData data)
    {
        var array = new NativeArray<int3>(data.NumberOfStars, Allocator.Temp);
        var random = new Random(data.StarGenerationSeed);
        for (int i = 0; i < data.NumberOfStars; i++)
            array[i] = random.NextInt3(1, LocalToGalaxy.MAX_STARSECTOR - 1);
        return array;
    }

    public struct PlanetSectorOffsetData
    {
        public byte Index;
        public uint DistanceInPlanetSectors;
        public float Angle;
        public float3 OrbitForward;
        public float3 OrbitRight;

        public double3 Distance => new LocalToGalaxy{PlanetSector = PlanetSector - StarSectorCenter}.Distance;
        public int3 PlanetSector => StarSectorCenter +
                                    (int3)((float3)DistanceInPlanetSectors * (OrbitRight * math.cos(Angle) + OrbitForward * math.sin(Angle)));
    }
    
    public static NativeArray<PlanetSectorOffsetData> GetPlanetSectorsOfStarSector(StarSectorData data)
    {
        var array = new NativeArray<PlanetSectorOffsetData>(data.NumberOfPlanets, Allocator.Temp);
        var random = new Random(data.StarGeneratorSeed);
        
        var lastDistance = MIN_SECTORS_FROM_STAR_TO_PLANET - MIN_SECTORS_BETWEEN_PLANETS;
        for (int i = 0; i < data.NumberOfPlanets; i++)
        {
            lastDistance += (uint)(random.NextFloat(1, 3) * MIN_SECTORS_BETWEEN_PLANETS);
            var angle = random.NextFloat(0, math.PI * 2);
            var tilt = math.step(.8f, random.NextFloat()) * random.NextFloat(-0.2f, 0.2f);
            array[i] = new PlanetSectorOffsetData
            {
                Index = (byte)i,
                Angle = angle,
                OrbitForward = math.forward(),
                OrbitRight = math.right() + math.up() * tilt,
                DistanceInPlanetSectors = lastDistance,
            };
        }
        return array;
    }
    #endregion
}
