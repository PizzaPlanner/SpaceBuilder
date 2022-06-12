using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

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
    public uint Seed;
}

public static class GalaxyTools
{
    private const float PERLIN_SECTOR_RESOLUTION = 0.01f;
    public static float Perlin3D(float3 xyz)
    {
        var (x,y,z) = (xyz.x, xyz.y, xyz.z);
        float xy = noise.cnoise(xyz.xy);
        float xz = noise.cnoise(xyz.xz);
        float yz = noise.cnoise(xyz.xz);
        float yx = noise.cnoise(xyz.yx);
        float zx = noise.cnoise(xyz.zx);
        float zy = noise.cnoise(xyz.zy);

        return (xy + xz + yz + yx + zx + zy) / 6;
    }

    public static SectorData GetSectorData(LocalToGalaxy ltg)
    {
        var data = new SectorData();
        // data is parent sector data
        var shash = math.hash(ltg.Sector);
        var srandom = new Random(shash);
        var nextType = srandom.NextInt(0, 10000);
        data.Type =
            nextType < 10 ? SectorType.Star :
            nextType == 10 ? SectorType.AsteroidField :
            SectorType.Empty;
        data.Seed = srandom.NextUInt();

        if(math.any(ltg.SubSector != int3.zero) && data.Type != SectorType.Empty)
        {
            var hash = math.hash(new uint2(math.hash(ltg.SubSector), math.hash(ltg.Sector)));
            var random = new Random(hash);
            nextType = random.NextInt(0, 10000);
            data.Type =
                nextType < 10 ? SectorType.AsteroidField :
                nextType < 15 ? SectorType.StarCompanion :
                SectorType.Empty;
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
                // var random = new Random(sectorData.Seed);
                Debug.Log($"Generating {ltg.Sector}:{ltg.SubSector}");
                switch (sectorData.Type)
                {
                    case SectorType.Star:
                        if(math.all(ltg.SubSector == int3.zero))
                           buffer.AddComponent<GenerateStar>(nativeThreadIndex, e);
                        break;
                    case SectorType.AsteroidField:
                        buffer.AddComponent<GenerateAsteroidField>(nativeThreadIndex, e);
                        break;
                    case SectorType.StarCompanion:
                        buffer.AddComponent<GeneratePOI>(nativeThreadIndex, e);
                        break;
                }
            }).ScheduleParallel();

        bufferSystem.AddJobHandleForProducer(Dependency);
    }
}
