using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public struct UpdateRotation : IComponentData
{
    public quaternion Value;
}

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class GridSubItemInitializationSystem : SystemBase
{
    private EndInitializationEntityCommandBufferSystem bufferSystem;

    protected override void OnCreate()
    {
        bufferSystem = World.GetExistingSystem<EndInitializationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        var buffer = bufferSystem.CreateCommandBuffer();
        Entities.WithNone<GridItem>().ForEach((Entity e, in GridSubItem subItem) =>
        {
            var parentGrid = e;
            var parentGridItem = e;
            while (HasComponent<Parent>(parentGrid) && !HasComponent<Grid>(parentGrid))
            {
                parentGrid = GetComponent<Parent>(parentGrid).Value;
                if (HasComponent<GridItem>(parentGrid))
                    parentGridItem = parentGrid;
            }

            if (!HasComponent<Grid>(parentGrid) || !HasComponent<GridItem>(parentGridItem))
            {
                buffer.DestroyEntity(e);
                return;
            }

            if (HasComponent<Ghost>(parentGridItem))
                return;

            var subItemRotation = new quaternion(GetComponent<LocalToParent>(e).Value);
            var parentRotation = new quaternion(GetComponent<LocalToParent>(parentGridItem).Value);
            var gridItem = GetComponent<GridItem>(parentGridItem);
            var parentCenter = (float3)(gridItem.Area.Max - gridItem.Area.Min + new int3(1)) * 0.5f;
            var relativePos = (subItem.RelativeCell + new float3(0.5f)) * Grid.CellSize(subItem.Size) - parentCenter;
            relativePos = math.mul(parentRotation, relativePos);
            relativePos = (relativePos + parentCenter) / Grid.CellSize(subItem.Size) - new float3(0.5f);
            var relativeCell = (int3)math.round(relativePos);

            var offset = Grid.CellFromPosition(subItem.Size, Grid.PositionFromCell(gridItem.Type, gridItem.Area.Origin));
            var position = offset + relativeCell;


            buffer.SetComponent(e, new Parent{Value = parentGrid});
            buffer.AddComponent(e, new UpdateRotation{Value = math.mul(parentRotation, subItemRotation)});
            buffer.AddComponent(e, new GridItem
            {
                Area = new BoundingBox{Origin = position},
                Type = subItem.Size,
                Occupied = (GridItem.Side)byte.MaxValue
            });
            buffer.SetComponent(e, new Translation{Value = Grid.PositionFromCell(subItem.Size, position)});
        }).Run();
        bufferSystem.AddJobHandleForProducer(Dependency);
    }
}
