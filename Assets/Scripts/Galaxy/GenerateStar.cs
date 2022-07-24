using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

public struct GenerateStar : IComponentData { }

public partial class GenerateStarSystem : SystemBase
{
    private EndSimulationEntityCommandBufferSystem bufferSystem;
    private Entity prefab;

    public const float STAR_SIZE_BASE = 5_000_000;

    protected override void OnCreate()
    {
        bufferSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        if(prefab == Entity.Null)
            prefab = GetEntityQuery(typeof(Prefab), typeof(Star)).GetSingletonEntity();

        Entities
            .WithAll<GenerateStar, ToGenerate>()
            .ForEach((Entity e, in LocalToGalaxy ltg, in SectorData data) =>
            {
                var random = new Random(data.Seed);
                var star = EntityManager.Instantiate(prefab);
                var starData = GetComponent<Star>(star);
                starData.Brightness = random.NextFloat(3, 9);
                starData.Color = Color.HSVToRGB(random.NextFloat(), random.NextFloat(0.8f, 1),
                    random.NextFloat(0.8f, 1));
                starData.Size = random.NextFloat(0.3f, 12f);
                var starLtg = ltg;
                starLtg.LocalPosition = random.NextFloat3(-2000, 2000);
                SetComponent(star, starData);
                SetComponent(star, starLtg);
                SetComponent(star, new URPMaterialPropertyEmissionColor
                {
                    Value = new float4(starData.Color.a, starData.Color.g, starData.Color.b, 0) * starData.Brightness
                });
                EntityManager.AddComponentData(star, new Scale {Value = starData.Size * STAR_SIZE_BASE});
                EntityManager.RemoveComponent<GenerateStar>(e);
            }).WithStructuralChanges().WithoutBurst().Run();

        bufferSystem.AddJobHandleForProducer(Dependency);
    }
}
//
// public partial class StarScannerSystem : SystemBase
// {
//     private NativeList<LocalToGalaxy> scannedLtgs;
//     private int3 nextScanSector;
//     private int3 packageSize;
//     private byte direction;
//     protected override void OnCreate()
//     {
//         scannedLtgs = new NativeList<LocalToGalaxy>(Allocator.Persistent);
//         RequireSingletonForUpdate<LocalPlayer>();
//     }
//
//     protected override void OnDestroy()
//     {
//         scannedLtgs.Dispose();
//     }
//
//     protected override void OnStartRunning()
//     {
//         nextScanSector = GetComponent<LocalToGalaxy>(GetSingletonEntity<LocalPlayer>()).Sector;
//         packageSize = new int3(3, 3, 3);
//         direction = 0;
//     }
//
//     protected override void OnUpdate()
//     {
//         var random = new Random(GlobalSystemVersion + 176231482u);
//         var startSector = nextScanSector + random.NextInt3(-1000, 1000);
//         for (int x = startSector.x; x < startSector.x + packageSize.x; x++)
//         for (int y = startSector.y; y < startSector.y + packageSize.y; y++)
//         for (int z = startSector.z; z < startSector.z + packageSize.z; z++)
//         {
//             var ltg = new LocalToGalaxy {Sector = new int3(x, y, z)};
//             var sector = GalaxyTools.GetSectorData(ltg);
//             if (sector.Type != SectorType.Empty)
//             {
//                 var e = EntityManager.CreateEntity(typeof(SectorChange));
//                 SetComponent(e, new SectorChange{New = ltg});
//
//             }
//         }
//
//         nextScanSector += new int3(packageSize.x, 0, 0);
//     }
// }
