using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

public static class GalaxyTools
{
    private const float PERLIN_SECTOR_RESOLUTION = 0.01f;
    private const int SECTOR_CENTRAL_SUBSECTOR = (int) Constants.MAX_SUBSECTOR >> 1;
    private const uint SUN_MAX_DIST_FROM_CENTER = (int) (SECTOR_CENTRAL_SUBSECTOR * 0.024f);
    private const uint MIN_DIST_FROM_SUN_TO_PLANET = (int) (10 * GenerateStarSystem.STAR_SIZE_BASE * GenerateStarSystem.STAR_MAX_SIZE_FACTOR / Constants.MAX_POSITION);
    private const uint MIN_DIST_BETWEEN_PLANETS = (int) (22 * GeneratePlanetSystem.PLANET_SIZE_BASE / Constants.MAX_POSITION * GeneratePlanetSystem.PLANET_MAX_SIZE_FACTOR );
    private const int PLANET_MAX_Y_DISTANCE = (int) (Constants.MAX_SUBSECTOR * 0.003f);

    [BurstCompile]
    public static float Perlin3D(in float3 xyz)
    {
        float xy = noise.cnoise(xyz.xy);
        float xz = noise.cnoise(xyz.xz);
        float yz = noise.cnoise(xyz.xz);
        float yx = noise.cnoise(xyz.yx);
        float zx = noise.cnoise(xyz.zx);
        float zy = noise.cnoise(xyz.zy);

        return (xy + xz + yz + yx + zx + zy) / 6;
    }

    [BurstCompile]
    public static FixedString32Bytes ToRomanNumeral(byte b)
    {
        return b switch
        {
            0 => "I",
            1 => "II",
            2 => "III",
            3 => "IV",
            4 => "V",
            5 => "VI",
            6 => "VII",
            7 => "VIII",
            8 => "IX",
            9 => "X",
            _ => "0"
        };
    }

    static FixedString32Bytes iToS(in uint x, int rank)
    {
        var y = (x >> (rank * 2)) & 0xF;
        return y switch
        {
            0u => "",
            1u => "be",
            2u => "rho",
            3u => "kha",
            4u => "xi",
            5u => "bo",
            6u => "ku",
            7u => "ne",
            8u => "ni",
            9u => "ta",
            10u => "ga",
            11u => "to",
            12u => "ox",
            13u => "ur",
            14u => "",
            15u => "bu",
            _ => $""
        };
    }

    [BurstCompile]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static uint SubHex(uint i, int part)
    {
        return (i >> (part * 2)) & 0xF;
    }

    [BurstCompile]
    public static FixedString32Bytes GetStarName(in uint seed)
    {
        var seed1 = seed;
        byte bonus = 0;
        for(int i = 0; i < 5; i++)
            if (SubHex(seed1, i) == SubHex(seed1, i + 1) && SubHex(seed1, i) == SubHex(seed1, i + 2))
            {
                bonus++;
                seed1 ^= (uint)(0xF << (i * 2));
            }
        FixedString32Bytes it1 = $"{iToS(seed1, 0)}{iToS(seed1, 1)}{iToS(seed1, 2)}{iToS(seed1, 3)}{iToS(seed1, 4)}{iToS(seed1, 5)}{iToS(seed1, 6)}{iToS(seed1, 7)}";
        if(bonus > 0)
            it1.Append((FixedString32Bytes)$" {bonus + 1}");
        unsafe
        {
            it1.GetUnsafePtr()[0] -= 32;
        }

        return it1;
    }

    public static int3 GetCelestialSectorCenter(in int3 sector, out Random random)
    {
        if (!GetSectorType(sector, out random).IsCelestialScale())
            return default;

        var center = (int3)random.NextUInt3(0, SUN_MAX_DIST_FROM_CENTER);
        return SECTOR_CENTRAL_SUBSECTOR + math.select(center, -center, random.NextBool3());
    }

    public static StarSectorData GetStarSectorData(in int3 sector)
    {
        if (GetSectorType(sector, out var random) != SectorType.Star)
            return default;

        var center = GetCelestialSectorCenter(sector, out random);
        var planetRandom = random.NextFloat();
        return new StarSectorData
        {
            Center = center,
            PlanetGeneratorSeed = GetSubsectorSeed(GetSectorSeed(sector), center),
            NumberOfPlanets = (byte)(
                planetRandom < 0.001f ? 0u :
                planetRandom < 0.2f ? random.NextUInt(1, 4) :
                planetRandom < 0.95f ? random.NextUInt(4, 8) :
                random.NextUInt(8, 12))
        };
    }

    public static StarSectorPlanetData GetPlanetData(in StarSectorData sectorData, in byte id)
    {
        var random = new Random(sectorData.PlanetGeneratorSeed);
        var data = new StarSectorPlanetData();
        var lastDistance = MIN_DIST_FROM_SUN_TO_PLANET;

        for (byte i = 0; i <= id; i++)
        {
            lastDistance += random.NextUInt(MIN_DIST_BETWEEN_PLANETS, MIN_DIST_BETWEEN_PLANETS * 2) * id;
            var angle = random.NextFloat(0, math.PI * 2);
            data = new StarSectorPlanetData
            {
                SubSector = sectorData.Center + new int3(
                    (int)(lastDistance * math.sin(angle)),
                    random.NextInt(-PLANET_MAX_Y_DISTANCE, PLANET_MAX_Y_DISTANCE),
                    (int)(lastDistance * math.cos(angle))
                )
            };
        }

        return data;
    }

    [BurstCompile]
    public static bool IsCelestialScale(this SubSectorType type) =>
        type is SubSectorType.Star or SubSectorType.Nebula;

    [BurstCompile]
    public static bool IsCelestialScale(this SectorType type) =>
        type is SectorType.Star or SectorType.Nebula;

    [BurstCompile]
    public static SectorType GetSectorType(int3 sector) => GetSectorType(sector, out _);

    [BurstCompile]
    static SectorType GetSectorType(int3 sector, out Random random)
    {
        var hash = GetSectorSeed(sector);
        random = new Random(hash);
        var nextType = random.NextInt(0, 10000);
        return
            nextType < 226 ? SectorType.Star :
            nextType < 229 ? SectorType.Nebula :
            nextType < 230 ? SectorType.BlackHole :
            SectorType.Empty;
    }

    [BurstCompile]
    public static uint GetSectorSeed(in int3 sector)
    {
        return math.hash(sector);
    }

    [BurstCompile]
    public static uint GetSubsectorSeed(LocalToGalaxy ltg)
    {
        return GetSubsectorSeed(GetSectorSeed(ltg.Sector), ltg.SubSector);
    }

    [BurstCompile]
    public static uint GetSubsectorSeed(in uint sectorSeed, in int3 subSector)
    {
        return math.hash(new uint2(math.hash(subSector), sectorSeed));
    }

    [BurstCompile]
    public static SectorData GetSectorData(in LocalToGalaxy ltg)
    {
        var data = new SectorData();
        data.Seed = GetSectorSeed(ltg.Sector);
        data.Type = GetSectorType(ltg.Sector);
        return data;
    }

    [BurstCompile]
    public static SubSectorData GetSubSectorData(in LocalToGalaxy ltg)
    {
        var sectorType = GetSectorType(ltg.Sector);
        var data = new SubSectorData
        {
            Seed = GetSubsectorSeed(ltg),
            Type = SubSectorType.Unknown
        };

        switch (sectorType)
        {
            case SectorType.Star:
                var starsector = GetStarSectorData(ltg.Sector);
                if (math.all(ltg.SubSector == starsector.Center))
                    data.Type = SubSectorType.Star;
                else
                {
                    for (byte i = 0; i < starsector.NumberOfPlanets; i++)
                    {
                        var planetsector = GetPlanetData(starsector, i);
                        var distToPlanetSq = math.distancesq(planetsector.SubSector, ltg.SubSector);
                        // Is a planet ?
                        if (math.all(planetsector.SubSector == ltg.SubSector))
                            data.Type = SubSectorType.Planet;
                        // Is in Planet POI Range ?
                        else if (distToPlanetSq < MIN_DIST_BETWEEN_PLANETS * 0.04f * MIN_DIST_BETWEEN_PLANETS * 0.04f)
                        {
                            var random = new Random(data.Seed).NextFloat();
                            data.Type = random < 0.0021f ? SubSectorType.POI : SubSectorType.Empty;
                        }
                        // non-POI space close to planets is empty
                        else if(distToPlanetSq < MIN_DIST_BETWEEN_PLANETS * 0.15f * MIN_DIST_BETWEEN_PLANETS * 0.15f)
                        {
                            data.Type = SubSectorType.Empty;
                        }
                    }

                    // Is not next to a planet or a planet's orbit
                    if (data.Type == SubSectorType.Unknown)
                    {
                        var random = new Random(data.Seed).NextFloat();
                        data.Type = random < 0.00027f ? SubSectorType.AsteroidField : SubSectorType.Empty;
                    }
                }
                break;
            case SectorType.Nebula:
            {
                data.Type = math.all(ltg.SubSector == GetCelestialSectorCenter(ltg.Sector, out _))
                    ? SubSectorType.Nebula
                    : SubSectorType.Empty;
                break;
            }
            default:
            {
                var perlin = Perlin3D((float3)ltg.SubSector / Constants.MAX_SUBSECTOR + ltg.Sector);
                data.Type = perlin > 0.2f ? SubSectorType.AsteroidField : SubSectorType.Empty;
                break;
            }
        }

        return data;
    }


    [BurstCompile]
    public static bool ShouldUnloadSector(LocalToGalaxy sector, LocalToGalaxy playerSector, int unloadDistances)
    {
        var rpos = math.abs(LocalToGalaxy.RelativePosition(playerSector, sector));
        var distInSubsectors = math.csum(math.abs(rpos / Constants.MAX_POSITION));
        return distInSubsectors > unloadDistances;
    }
}
