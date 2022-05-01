using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public struct SubSector : IComponentData { }

public enum SectorType
{
    Empty,
    Star,
    SubStar,
    StarCompanion,
    AsteroidField,
    Nebula,
}

public struct SectorData : IComponentData
{
    public SectorType Type;
    public float Perlin;
    public uint Seed;
    public LocalToGalaxy ClosestStar;
}

public static class GalaxyTools
{
    public static SectorData GetSectorData(LocalToGalaxy ltg)
    {
        var data = new SectorData();
        var perlinPosition = new float2(ltg.XInTenKm / 100f, ltg.YInTenKm / 100f);
        var perlin = noise.cnoise(perlinPosition);
        data.Perlin = perlin;
        if (perlin < 0.3f)
            data.Type = SectorType.Nebula;
        else if (perlin < 0.5f)
            data.Type = SectorType.Empty;
        else
        {
            var currentMax = perlin;
            var (cX, cY) = (ltg.XInTenKm, ltg.YInTenKm);
            var lastMax = float.MinValue;
            while (lastMax < currentMax)
            {
                lastMax = currentMax;
                var (pX, pY) = (cX, cY);
                for(long x = cX - 1; x <= cX + 1; x++)
                for (long y = cY - 1; y <= cY + 1; y++)
                {
                    var pRelative = noise.cnoise(new float2(x / 100f, y / 100f));
                    if (pRelative <= lastMax) continue;

                    currentMax = pRelative;
                    pX = x;
                    pY = y;
                }

                cX = pX;
                cY = pY;
            }

            var xhash = math.hash(new uint2((uint)( (ulong)ltg.XInTenKm & 0x0000_0000_FFFF_FFFF),
                (uint)(((ulong) ltg.XInTenKm & 0xFFFF_FFFF_0000_0000) >> 16)));
            var yhash = math.hash(new uint2((uint) ((ulong) ltg.YInTenKm & 0x0000_0000_FFFF_FFFF),
                (uint) (((ulong)ltg.YInTenKm & 0xFFFF_FFFF_0000_0000) >> 16)));

            var random = new Unity.Mathematics.Random(math.hash(new uint2(xhash, yhash)));

            var distancesq = math.distancesq(new double2(cX, cY), new double2(ltg.XInTenKm, ltg.YInTenKm));
            var multistarSystem = random.NextFloat() < 0.02f;
            if (distancesq < 1)
                data.Type = multistarSystem ? SectorType.SubStar : SectorType.Star;
            else if (distancesq > (multistarSystem ? 25 : 16) && distancesq < 100 && random.NextFloat() < 0.4f)
                data.Type = random.NextFloat() < 0.2f ? SectorType.StarCompanion : SectorType.AsteroidField;
            else if (distancesq >= 100 && random.NextFloat() < 0.1f)
                data.Type = SectorType.AsteroidField;

            data.ClosestStar = new LocalToGalaxy {XInTenKm = cX, YInTenKm = cY};
            data.Seed = random.NextUInt();
        }

        return data;
    }
}

public partial class SubSectorGeneratorSystem : SystemBase
{
    private EndSimulationEntityCommandBufferSystem bufferSystem;
    protected override void OnCreate()
    {
        bufferSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        var buffer = bufferSystem.CreateCommandBuffer().AsParallelWriter();
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
                    case SectorType.AsteroidField:
                        buffer.AddComponent<GenerateAsteroidField>(nativeThreadIndex, e);
                        break;
                }
            }).ScheduleParallel();

        bufferSystem.AddJobHandleForProducer(Dependency);
    }
}
