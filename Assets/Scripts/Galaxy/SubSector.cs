using System;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

public struct SubSector : IComponentData { }

public enum SectorType : byte
{
    Empty,
    Star,
    BlackHole,
    Nebula,
}

public enum SubSectorType : byte
{
    Unknown,
    Empty,
    Star,
    Planet,
    POI,
    AsteroidField,
}

public struct SectorData : IComponentData
{
    public SectorType Type;
    public uint Seed;
}

public struct SubSectorData : IComponentData
{
    public SubSectorType Type;
    public uint Seed;
}

public struct StarSectorData : IComponentData
{
    public uint PlanetGeneratorSeed;
    public int3 Center;
    public byte NumberOfPlanets;
}

public struct StarSectorPlanetData : IBufferElementData
{
    public int3 SubSector;
}

public static class GalaxyTools
{
    private const float PERLIN_SECTOR_RESOLUTION = 0.01f;

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

    public static StarSectorData GetStarSectorData(in int3 sector)
    {
        if (GetSectorType(sector, out var random) != SectorType.Star)
            return default;

        var center = random.NextInt3(1, 1593);
        center = math.select(center, -center, random.NextBool3());
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
        var lastDistance = 15817u;

        for (byte i = 0; i <= id; i++)
        {
            lastDistance += random.NextUInt(72182, 233010) * id;
            var angle = random.NextFloat(0, math.PI * 2);
            data = new StarSectorPlanetData
            {
                SubSector = sectorData.Center + new int3(
                    (int)(lastDistance * math.sin(angle)),
                    random.NextInt(-81, 82),
                    (int)(lastDistance * math.cos(angle))
                )
            };
        }

        return data;
    }

    [BurstCompile]
    public static uint GetSectorSeed(in LocalToGalaxy ltg, out SectorType type)
    {
        type = GetSectorType(ltg.Sector, out var srandom);
        return srandom.NextUInt();
    }

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
            nextType < 300 ? SectorType.Nebula :
            nextType < 301 ? SectorType.BlackHole :
            SectorType.Empty;
    }

    [BurstCompile]
    public static uint GetSectorSeed(in int3 sector)
    {
        return math.hash(sector);
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
        data.Seed = GetSectorSeed(ltg, out data.Type);
        return data;
    }

    [BurstCompile]
    public static SubSectorData GetSubSectorData(in LocalToGalaxy ltg)
    {
        var sectorData = GetSectorData(ltg);
        var data = new SubSectorData
        {
            Seed = GetSubsectorSeed(sectorData.Seed, ltg.SubSector),
            Type = SubSectorType.Unknown
        };

        if (math.all(ltg.SubSector == 0))
        {
            data.Type = SubSectorType.Empty;
            return data;
        }

        switch (sectorData.Type)
        {
            case SectorType.Star:
                var starsector = GetStarSectorData(ltg.Sector);
                if (math.all(ltg.SubSector == starsector.Center))
                    data.Type = SubSectorType.Star;
                else
                {
                    var distToSunSq = math.distancesq(ltg.SubSector, starsector.Center);
                    for (byte i = 0; i < starsector.NumberOfPlanets; i++)
                    {
                        var planetsector = GetPlanetData(starsector, i);
                        if (math.all(planetsector.SubSector == ltg.SubSector))
                            data.Type = SubSectorType.Planet;
                        else if (math.distancesq(planetsector.SubSector, ltg.Sector) < 25 * 25)
                        {
                            var random = new Random(data.Seed).NextFloat();
                            data.Type = random < 0.021f ? SubSectorType.POI : SubSectorType.Empty;
                        }
                        else
                        {
                            var planetDistToSunSq = math.distancesq(planetsector.SubSector, starsector.Center);
                            if (math.abs(planetDistToSunSq - distToSunSq) < 100 * 100)
                                data.Type = SubSectorType.Empty;
                        }
                    }

                    if (data.Type == SubSectorType.Unknown)
                    {
                        var random = new Random(data.Seed).NextFloat();
                        data.Type = random < 0.0012f ? SubSectorType.AsteroidField : SubSectorType.Empty;
                    }
                }
                break;
            default:
                data.Type = SubSectorType.Empty;
                break;
        }

        return data;
    }
}

public partial class SubSectorGeneratorSystem : SystemBase
{
    private EndSimulationEntityCommandBufferSystem bufferSystem;
    private EntityArchetype forceLoadArchetype;

    protected override void OnCreate()
    {
        bufferSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
        forceLoadArchetype = EntityManager.CreateArchetype(typeof(ForceLoadSector));
    }

    protected override void OnUpdate()
    {
        var buffer = bufferSystem.CreateCommandBuffer().AsParallelWriter();
        var forceLoad = forceLoadArchetype;
        Entities
            .WithAll<ToGenerate>()
            .WithAll<SubSector>()
            .WithNone<SectorData>()
            .ForEach((Entity e, int nativeThreadIndex, in LocalToGalaxy ltg) =>
            {
                var sectorData = GalaxyTools.GetSectorData(ltg);
                buffer.AddComponent(nativeThreadIndex, e, sectorData);
                switch (sectorData.Type)
                {
                    case SectorType.Star when math.all(
                                                  ltg.SubSector == GalaxyTools.GetStarSectorData(ltg.Sector).Center):
                    {
                        var starSectorData = GalaxyTools.GetStarSectorData(ltg.Sector);
                        buffer.AddComponent(nativeThreadIndex, e, starSectorData);
                        break;
                    }
                    case SectorType.Star when math.all(ltg.SubSector == 0):
                    {
                        var starSectorData = GalaxyTools.GetStarSectorData(ltg.Sector);
                        var eStarSector = buffer.CreateEntity(nativeThreadIndex, forceLoad);
                        buffer.SetComponent(nativeThreadIndex, eStarSector, new ForceLoadSector{
                            Sector = new LocalToGalaxy
                            {
                                Sector = ltg.Sector,
                                SubSector = starSectorData.Center
                            }});
                        buffer.AddComponent(nativeThreadIndex, e, starSectorData);
                        var planetBuffer = buffer.AddBuffer<StarSectorPlanetData>(nativeThreadIndex, e);
                        for (byte i = 0; i < starSectorData.NumberOfPlanets; i++)
                        {
                            var planetData = GalaxyTools.GetPlanetData(starSectorData, i);
                            var ePlanetSector = buffer.CreateEntity(nativeThreadIndex, forceLoad);
                            buffer.SetComponent(nativeThreadIndex, ePlanetSector, new ForceLoadSector{
                                Sector = new LocalToGalaxy
                                {
                                    Sector = ltg.Sector,
                                    SubSector = planetData.SubSector
                                }});
                            planetBuffer.Add(planetData);
                        }
                        break;
                    }
                }

                var subsectorData = GalaxyTools.GetSubSectorData(ltg);
                buffer.AddComponent(nativeThreadIndex, e, subsectorData);
                switch (subsectorData.Type)
                {
                    case SubSectorType.AsteroidField:
                        buffer.AddComponent<GenerateAsteroidField>(nativeThreadIndex, e);
                        break;
                    case SubSectorType.Planet:
                        buffer.AddComponent<GeneratePlanet>(nativeThreadIndex, e);
                        break;
                    case SubSectorType.POI:
                        buffer.AddComponent<GeneratePOI>(nativeThreadIndex, e);
                        break;
                    case SubSectorType.Star:
                        buffer.AddComponent<GenerateStar>(nativeThreadIndex, e);
                        break;
                }
            }).ScheduleParallel();

        bufferSystem.AddJobHandleForProducer(Dependency);
    }
}
