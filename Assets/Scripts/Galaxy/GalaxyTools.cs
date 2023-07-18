using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.SocialPlatforms;
using Random = Unity.Mathematics.Random;

public static class GalaxyTools
{
    private const float PERLIN_SECTOR_RESOLUTION = 0.01f;
    public const float PLANET_SIZE_MAX = (float)LocalToGalaxy.DISTANCE_PER_PLANETSECTOR * 0.8f;
    public const float STAR_SIZE_MAX = (float)LocalToGalaxy.DISTANCE_PER_PLANETSECTOR * 52;
    private const uint MIN_SECTORS_FROM_STAR_TO_PLANET = 120;
    private const uint MIN_SECTORS_BETWEEN_PLANETS = 7;
    private const int PLANET_MAX_Y_DISTANCE = (int) (LocalToGalaxy.MAX_PLANETSECTOR * 0.003f);
    
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

    public static string ToGalacticDistanceWithUnit(double distance)
    {
        var unit = "m";
        if (distance >= 0.01 * LocalToGalaxy.DISTANCE_PARSEC)
        {
            distance /= LocalToGalaxy.DISTANCE_PARSEC;
            unit = "pc";
        }
        else if (distance >= LocalToGalaxy.DISTANCE_ASTRONOMICAL_UNIT)
        {
            distance /= LocalToGalaxy.DISTANCE_ASTRONOMICAL_UNIT;
            unit = "au";
        }
        else if (distance > 1000_000_000)
        {
            distance *= 0.000_000_001f;
            unit = "Gm";
        }
        else if (distance > 1000_000)
        {
            distance *= 0.000_001f;
            unit = "Mm";
        }
        else if (distance > 1000)
        {
            distance *= 0.001f;
            unit = "km";
        }

        return $"{distance:F1}{unit}";
    }

    public static string ToTimeUnit(double time)
    {
        var unit = "s";
        if (time > 3600 * 24 * 30 * 12)
        {
            return $"> 1y";
        }
        else if (time > 3600 * 24)
        {
            return $"{time / (3600 * 24):F1}d";
        }
        else if (time > 3600)
        {
            return $"{time / 3600:F1}h";
        }
        else if (time > 60)
        {
            return $"{time / 60:F1}m";
        }
        else return $"{time:F1}s";
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
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static uint SubHex(uint i, int part)
    {
        return (i >> (part * 2)) & 0xF;
    }
    
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

    public static readonly int3 StarSectorCenter = LocalToGalaxy.MAX_STARSECTOR >> 1;
    public static readonly int3 PlanetSectorCenter = LocalToGalaxy.MAX_PLANETSECTOR >> 1;

    public static uint GetSectorSeed(in int3 sector)
    {
        return math.hash(sector);
    }
    
    public static uint GetPlanetSectorSeed(LocalToGalaxy ltg)
    {
        return GetPlanetSectorSeed(GetSectorSeed(ltg.StarSector), ltg.PlanetSector);
    }
    
    public static uint GetPlanetSectorSeed(in uint sectorSeed, in int3 subSector)
    {
        return math.hash(new uint2(math.hash(subSector), sectorSeed));
    }
    
    #region SectorType
    public static LightSectorType GetLightSectorType(in LocalToGalaxy pos, out float perlin)
    {
        perlin = Perlin3D((float3)pos.LightSector * 1f / LocalToGalaxy.MAX_LIGHTSECTOR);
        return perlin < 0.0f ? LightSectorType.Empty : LightSectorType.Stars;
    }
    
    public static StarSectorType GetStarSectorType(in LocalToGalaxy ltg)
    {
        return StarSectorType.Star;
    }
    #endregion
    
    #region SectorData
    public static LightSectorData GetLightSectorData(in LocalToGalaxy ltg)
    {
        var data = new LightSectorData();
        var random = new Random(GetSectorSeed(ltg.LightSector));
        data.Type = GetLightSectorType(ltg, out data.Perlin);
        if (data.Type == LightSectorType.Stars)
        {
            data.NumberOfStars = (byte)(data.Perlin * 32 * random.NextFloat(0.7f, 1f));
            data.StarSectorBytes = new FixedList128Bytes<ushort>();

            for (int i = 0; i < data.NumberOfStars; i++)
            {
                var starSector = random.NextInt3(LocalToGalaxy.MAX_STARSECTOR);
                var starLtg = ltg;
                starLtg.StarSector = starSector;
                starLtg.PlanetSector = PlanetSectorCenter;
                data.StarSectorBytes.Add((ushort)starLtg.StarSector.x);
                data.StarSectorBytes.Add((ushort)starLtg.StarSector.y);
                data.StarSectorBytes.Add((ushort)starLtg.StarSector.z);
            }
        }

        return data;
    }

    public static StarSectorData GetStarSectorData(in LocalToGalaxy ltg)
    {
        var random = new Random(GetSectorSeed(ltg.StarSector));
        var data = new StarSectorData();
        data.Type = GetStarSectorType(ltg);
        if (data.Type != StarSectorType.Empty)
        {
            data.NumberOfPlanets = (byte)random.NextInt(9);
            data.PlanetSectorBytes = new FixedList128Bytes<ushort>();
            data.StarGeneratorSeed = random.NextUInt();

            var lastDistance = MIN_SECTORS_FROM_STAR_TO_PLANET - MIN_SECTORS_BETWEEN_PLANETS;
            for (byte i = 0; i <= data.NumberOfPlanets; i++)
            {
                lastDistance += random.NextUInt(11) + MIN_SECTORS_BETWEEN_PLANETS;
                var angle = random.NextFloat(0, math.PI * 2);
                var delta = new LocalToGalaxy
                {
                    PlanetSector = PlanetSectorCenter + new int3(
                        (int)(lastDistance * math.sin(angle)),
                        random.NextInt(-PLANET_MAX_Y_DISTANCE, PLANET_MAX_Y_DISTANCE),
                        (int)(lastDistance * math.cos(angle))
                    )
                };
                var planetLtg = ltg + delta;
                data.PlanetSectorBytes.Add((ushort)planetLtg.StarSector.x);
                data.PlanetSectorBytes.Add((ushort)planetLtg.StarSector.y);
                data.PlanetSectorBytes.Add((ushort)planetLtg.StarSector.z);
            }
        }

        return data;
    }
    #endregion
    
    public static double DistanceInSubsectors(LocalToGalaxy delta)
    {
        var rpos = math.abs(delta.Distance);
        return math.csum(math.abs(rpos / LocalToGalaxy.MAX_POSITION));
    }
    
    public static bool ShouldUnloadSector(LocalToGalaxy delta, int unloadDistances)
    {
        return DistanceInSubsectors(delta) > unloadDistances;
    }
}
