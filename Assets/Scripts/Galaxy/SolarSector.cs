// using System;
// using Unity.Entities;
// using Unity.Mathematics;
// using Unity.Transforms;
// using Random = Unity.Mathematics.Random;
//
// public enum SolarSystemPOIType
// {
//     None,
//     SmallAsteroidField,
//     MediumAsteroidField,
//     RichAsteroidField,
//     Wreckage,
//     Station
// }
//
// public struct SolarSystemPOISector : IComponentData
// {
//     public const ulong SectorMask = 0x0000_0000_0000_000F;
//     public SolarSystemPOIType Type;
// }
//
// public partial class SolarSystemPOISectorGeneratorSystem : SystemBase
// {
//     private EndSimulationEntityCommandBufferSystem bufferSystem;
//     protected override void OnCreate()
//     {
//         bufferSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
//     }
//
//     protected override void OnUpdate()
//     {
//         var buffer = bufferSystem.CreateCommandBuffer();
//         var forceGenerate = HasSingleton<ForceGenerateSector>() ? GetSingleton<ForceGenerateSector>().Sector : 0ul;
//         var asteroidPrefab = Entity.Null;
//         Entities
//             .WithAll<ToGenerate>()
//             .ForEach((Entity e, in SolarSystemPOISector poisector, in SubSector subsector) =>
//             {
//                 // var random = new Unity.Mathematics.Random(subsector.Seed);
//                 for (uint i = 0x00; i < 0x10; i++)
//                 {
//                     var poi = buffer.CreateEntity();
//                     buffer.AddComponent(poi, new Parent{Value = e});
//                     switch (poisector.Type)
//                     {
//                         case SolarSystemPOIType.SmallAsteroidField:
//                             buffer.AddComponent(poi, new AsteroidFieldData
//                             {
//                                 density = random.NextFloat(0f, 3f),
//                                 Bounds = random.NextInt3(new int3(200, 100, 200), new int3(500, 200, 500)),
//                                 prefab = asteroidPrefab,
//                                 seed = random.NextUInt(),
//                                 diameterRange = new int2(10, 35)
//                             });
//                             break;
//                         case SolarSystemPOIType.MediumAsteroidField:
//                             buffer.AddComponent(poi, new AsteroidFieldData
//                             {
//                                 density = random.NextFloat(1.5f, 4f),
//                                 Bounds = random.NextInt3(new int3(700, 200, 700), new int3(1200, 300, 1200)),
//                                 prefab = asteroidPrefab,
//                                 seed = random.NextUInt(),
//                                 diameterRange = new int2(15, 64)
//                             });
//                             break;
//                         case SolarSystemPOIType.RichAsteroidField:
//                             buffer.AddComponent(poi, new AsteroidFieldData
//                             {
//                                 density = random.NextFloat(2f, 4.5f),
//                                 Bounds = random.NextInt3(new int3(1000, 200, 1000), new int3(2000, 400, 2000)),
//                                 prefab = asteroidPrefab,
//                                 seed = random.NextUInt(),
//                                 diameterRange = new int2(25, 95)
//                             });
//                             break;
//                     }
//                 }
//
//                 buffer.RemoveComponent<ToGenerate>(e);
//             }).Schedule();
//
//         bufferSystem.AddJobHandleForProducer(Dependency);
//     }
// }
