using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

[UpdateInGroup(typeof(GridRecalculationSystemGroup))]
public partial class RecalculateSteeringControllerSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate(GetEntityQuery(typeof(GridNeedsUpdate)));
    }

    protected override void OnUpdate()
    {
        var buffer = GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        var query = GetEntityQuery(typeof(Thruster), typeof(GridItem), typeof(Rotation));
        var entities = query.ToEntityArray(Allocator.TempJob);
        var thrusters = query.ToComponentDataArray<Thruster>(Allocator.TempJob);
        var gridItems = query.ToComponentDataArray<GridItem>(Allocator.TempJob);
        var rotations = query.ToComponentDataArray<Rotation>(Allocator.TempJob);

        Entities
            .WithReadOnly(entities).WithDisposeOnCompletion(entities)
            .WithReadOnly(thrusters).WithDisposeOnCompletion(thrusters)
            .WithReadOnly(gridItems).WithDisposeOnCompletion(gridItems)
            .WithReadOnly(rotations).WithDisposeOnCompletion(rotations)
            .ForEach((Entity e, ref DynamicBuffer<ControlledItems> controller, in SteeringController steering) =>
        {
            var grid = GetComponent<GridItem>(e).Grid;
            controller.Clear();
            for(int i = 0; i < entities.Length; i++)
                if (gridItems[i].Grid == grid)
                {
                    var rotation = thrusters[i].Direction(rotations[i].Value);
                    var type =
                        rotation == GridItem.Side.Back ? ToggleType.ThrusterForward :
                        rotation == GridItem.Side.Front ? ToggleType.ThrusterBackward :
                        rotation == GridItem.Side.Left ? ToggleType.ThrusterRight :
                        rotation == GridItem.Side.Right ? ToggleType.ThrusterLeft :
                        rotation == GridItem.Side.Up ? ToggleType.ThrusterDown :
                        rotation == GridItem.Side.Down ? ToggleType.ThrusterUp :
                        0;
                    if (type == 0)
                        continue;

                    controller.Add(new ControlledItems
                    {
                        Target = entities[i],
                        Type = type
                    });
                }
        }).Schedule();
    }
}
