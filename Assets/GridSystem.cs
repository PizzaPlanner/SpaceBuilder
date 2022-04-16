// using Unity.Burst;
// using Unity.Collections;
// using Unity.Entities;
// using Unity.Jobs;
// using Unity.Mathematics;
// using Unity.Physics;
// using Unity.Physics.Systems;
// using Unity.Transforms;
//
// [UpdateInGroup(typeof(InitializationSystemGroup))]
// public class GridSystem : SystemBase
// {
//     private EndInitializationEntityCommandBufferSystem bufferSystem;
//     private BlobAssetReference<Unity.Physics.Collider> boxBlob;
//     public CollisionFilter filter = new CollisionFilter {BelongsTo = 1<<5, CollidesWith = 1<<5, GroupIndex = 1};
//     protected override void OnCreate()
//     {
//         bufferSystem = World.GetExistingSystem<EndInitializationEntityCommandBufferSystem>();
//         boxBlob = Unity.Physics.BoxCollider.Create(new BoxGeometry
//             {
//                 Size = new float3(0.75f, 0.75f, 0.75f),
//                 Center = float3.zero,
//                 Orientation = quaternion.identity
//             },
//             filter,
//             new Material
//             {
//                 CollisionResponse = CollisionResponsePolicy.RaiseTriggerEvents
//             });
//     }
//
//     protected override void OnUpdate()
//     {
//         var buffer = bufferSystem.CreateCommandBuffer();
//         var blob = boxBlob;
//         Entities
//             .WithNone<Initialized>()
//             .ForEach((Entity e, in Grid grid) =>
//         {
//             for (int x = 0; x < grid.Size.x; x++)
//             for (int y = 0; y < grid.Size.y; y++)
//             for (int z = 0; z < grid.Size.z; z++)
//             {
//                 var ent = buffer.CreateEntity();
//                 buffer.AddComponent(ent, new GridItem());
//                 buffer.AddComponent(ent, new Translation {Value = new float3(x, y, z)});
//                 buffer.AddComponent(ent, new NeedsUpdate());
//                 buffer.AddComponent(ent, new PhysicsCollider{Value = blob});
//                 buffer.AddComponent(ent, new Temperature{Value = 0});
//                 buffer.AddComponent<ReceiveTemperature>(ent);
//                 buffer.AddBuffer<NeighbourData>(ent);
//             }
//
//             var y2 = grid.Size.y;
//             for (int x = 0; x < grid.Size.x; x++)
//             for (int z = 0; z < grid.Size.z; z++)
//             {
//                 var ent = buffer.CreateEntity();
//                 buffer.AddComponent(ent, new GridItem());
//                 buffer.AddComponent(ent, new Translation {Value = new float3(x, y2, z)});
//                 buffer.AddComponent(ent, new PhysicsCollider{Value = blob});
//                 buffer.AddComponent(ent, new Temperature{Value = 30});
//                 buffer.AddComponent<Sun>(ent);
//                 buffer.AddBuffer<NeighbourData>(ent);
//             }
//
//             buffer.AddComponent<Initialized>(e);
//         }).Schedule();
//         bufferSystem.AddJobHandleForProducer(Dependency);
//     }
//
//     protected override void OnDestroy()
//     {
//         boxBlob.Dispose();
//     }
// }
//
// public class TemperatureExchangeSystem : SystemBase
// {
//     protected override void OnUpdate()
//     {
//         Entities.WithAll<Sun>().ForEach((ref Temperature t) =>
//         {
//             t.Value = 30;
//         }).ScheduleParallel();
//
//         Entities
//             .ForEach((DynamicBuffer<NeighbourData> neighbours, ref ReceiveTemperature rtemp) =>
//         {
//             var mean = 0f;
//             for (int i = 0; i < neighbours.Length; i++)
//                 mean += GetComponent<Temperature>(neighbours[i].Neighbour).Value;
//             rtemp.Value = mean;
//             rtemp.Parties = (byte)neighbours.Length;
//         }).ScheduleParallel();
//
//         Entities.ForEach((ref Temperature temp,in ReceiveTemperature rtemp) =>
//         {
//             temp.Value = (rtemp.Value + temp.Value) / (rtemp.Parties + 1);
//         }).ScheduleParallel();
//     }
// }
//
// public struct NeedsUpdate : IComponentData { }
//
// public class UpdateNeighboursSystem : SystemBase
// {
//     EndSimulationEntityCommandBufferSystem bufferSystem;
//     StepPhysicsWorld stepPhysicsWorld;
//     BuildPhysicsWorld physicsWorld;
//     private GridSystem gridSystem;
//
//     private NativeArray<float3> floats;
//
//      protected override void OnCreate()
//     {
//         bufferSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
//         stepPhysicsWorld = World.GetExistingSystem<StepPhysicsWorld>();
//         physicsWorld = World.GetExistingSystem<BuildPhysicsWorld>();
//         gridSystem = World.GetExistingSystem<GridSystem>();
//
//         floats = new NativeArray<float3>(6, Allocator.Persistent);
//         floats[0] = new float3(1,0,0);
//         floats[1] = new float3(-1,0,0);
//         floats[2] = new float3(0,1,0);
//         floats[3] = new float3(0,-1,0);
//         floats[4] = new float3(0,0,1);
//         floats[5] = new float3(0,0,-1);
//     }
//
//      protected override void OnDestroy()
//      {
//          floats.Dispose();
//      }
//
//      protected override void OnUpdate()
//     {
//         var buffer = bufferSystem.CreateCommandBuffer();
//         var filter = gridSystem.filter;
//         var collisionWorld = physicsWorld.PhysicsWorld.CollisionWorld;
//         var f = floats;
//         Entities
//             .WithAll<NeedsUpdate>()
//             .WithAll<GridItem>()
//             .WithReadOnly(f)
//             .ForEach((Entity e, ref DynamicBuffer<NeighbourData> neighbours, in Translation translation) =>
//             {
//                 for (int i = 0; i < f.Length; i++)
//                 {
//                     var input = new RaycastInput
//                     {
//                         Start = translation.Value + f[i] * 0.5f,
//                         End = translation.Value + f[i],
//                         Filter = filter
//                     };
//                     if (collisionWorld.CastRay(input, out var hit))
//                     {
//                         bool hasNb = false;
//                         for (int j = 0; j < neighbours.Length; j++)
//                             hasNb |= neighbours[j].Neighbour == hit.Entity;
//                         if (!hasNb)
//                         {
//                             neighbours.Add(new NeighbourData{Neighbour = hit.Entity});
//                             buffer.AddComponent<NeedsUpdate>(hit.Entity);
//                         }
//                     }
//                 }
//                 buffer.RemoveComponent<NeedsUpdate>(e);
//             }).Run();
//         // bufferSystem.AddJobHandleForProducer(Dependency);
//     }
// }
