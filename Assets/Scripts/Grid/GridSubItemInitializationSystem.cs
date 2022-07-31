// using Unity.Collections;
// using Unity.Entities;
// using Unity.Mathematics;
// using Unity.Transforms;
// using UnityEngine;
//
// public struct UpdateRotation : IComponentData
// {
//     public quaternion Value;
//     public Entity ReferencePrefab;
// }
//
// [ExecuteAlways]
// [UpdateInGroup(typeof(LateSimulationSystemGroup), OrderFirst = true)]
// public class BeginLateSimulationEntitiyCommandBufferSystem : EntityCommandBufferSystem { }
//
// [UpdateInGroup(typeof(LateSimulationSystemGroup))]
// public partial class GridSubItemInitializationSystem : SystemBase
// {
//     private EntityCommandBufferSystem bufferSystem;
//
//     protected override void OnCreate()
//     {
//         bufferSystem = World.GetExistingSystem<BeginInitializationEntityCommandBufferSystem>();
//     }
//
//     protected override void OnUpdate()
//     {
//         var buffer = bufferSystem.CreateCommandBuffer();
//         var identifiablies = GetEntityQuery(typeof(Identifieable));
//         var idEntities = identifiablies.ToEntityArray(Allocator.Temp);
//         var idIds = identifiablies.ToComponentDataArray<Identifieable>(Allocator.Temp);
//
//         Entities
//             .WithReadOnly(idEntities)
//             .WithReadOnly(idIds)
//             .WithNone<GridItem, Blueprinted>()
//             .ForEach((Entity e, in GridSubItem subItem, in Identifieable id) =>
//         {
//             var parentGridItem = e;
//             while (HasComponent<Parent>(parentGridItem) && !HasComponent<GridItem>(parentGridItem))
//             {
//                 parentGridItem = GetComponent<Parent>(parentGridItem).Value;
//             }
//
//             if (!HasComponent<GridItem>(parentGridItem))
//             {
//                 buffer.DestroyEntity(e);
//                 return;
//             }
//
//             if (HasComponent<Ghost>(parentGridItem))
//                 return;
//
//             var subItemRotation = new quaternion(GetComponent<LocalToParent>(e).Value);
//             var parentRotation = new quaternion(GetComponent<LocalToParent>(parentGridItem).Value);
//             var gridItem = GetComponent<GridItem>(parentGridItem);
//             var parentCenter = (float3)(gridItem.Area.Max - gridItem.Area.Min + new int3(1)) * 0.5f;
//             var relativePos = (subItem.RelativeCell + new float3(0.5f)) * Grid.CELL_SIZE - parentCenter;
//             relativePos = math.mul(parentRotation, relativePos);
//             relativePos = (relativePos + parentCenter) / Grid.CELL_SIZE - new float3(0.5f);
//             var relativeCell = (int3)math.round(relativePos);
//
//             var offset = gridItem.Area.Origin;
//             var position = offset + relativeCell;
//
//             var prefab = Entity.Null;
//             for(int i = 0; i < idIds.Length; i++)
//                 if (idIds[i].ItemId == id.ItemId)
//                     prefab = idEntities[i];
//
//             buffer.AddComponent(e, new UpdateRotation{Value = math.mul(parentRotation, subItemRotation), ReferencePrefab = prefab});
//             buffer.AddComponent(e, new NeedsUpdate());
//             buffer.AddComponent(e, new GridItem
//             {
//                 Area = new BoundingBox{Origin = position},
//                 Occupied = (GridItem.Side)byte.MaxValue,
//                 Grid = gridItem.Grid
//             });
//
//             // This only gets into effect at the end of next frame!
//             buffer.SetComponent(e, new Parent{Value = gridItem.Grid});
//             buffer.SetComponent(e, new Translation{Value = Grid.PositionFromCell(position)});
//         }).Run();
//
//         bufferSystem.AddJobHandleForProducer(Dependency);
//     }
// }
