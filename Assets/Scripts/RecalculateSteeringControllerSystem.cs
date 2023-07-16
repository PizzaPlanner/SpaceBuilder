using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

[UpdateInGroup(typeof(GridRecalculationSystemGroup))]
public partial class RecalculateSteeringControllerSystem : SystemBase
{


    
    partial struct SteeringJob : IJobEntity
    {
        [ReadOnly] public ComponentLookup<GridItem> gridLookup;
        [ReadOnly] public NativeArray<Entity> entities;
        [ReadOnly] public NativeArray<GridItem> gridItems;
        [ReadOnly] public NativeArray<Thruster> thrusters;
        [ReadOnly] public NativeArray<LocalTransform> rotations;

        public void Execute(Entity e, ref DynamicBuffer<ControlledItems> controller, in SteeringController steering)
        {
            var grid = gridLookup[e].Grid;
            controller.Clear();
            for(int i = 0; i < entities.Length; i++)
                if (gridItems[i].Grid == grid)
                {
                    var rotation = thrusters[i].Direction(rotations[i].Rotation);
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
        }
    }
    
    protected override void OnCreate()
    {
        RequireForUpdate(GetEntityQuery(typeof(GridUpdate)));
    }

    protected override void OnUpdate()
    {
        var query = GetEntityQuery(typeof(Thruster), typeof(GridItem), typeof(LocalTransform));

        new SteeringJob
        {
            entities = query.ToEntityArray(Allocator.TempJob),
            thrusters = query.ToComponentDataArray<Thruster>(Allocator.TempJob),
            gridItems = query.ToComponentDataArray<GridItem>(Allocator.TempJob),
            rotations = query.ToComponentDataArray<LocalTransform>(Allocator.TempJob),
            gridLookup = SystemAPI.GetComponentLookup<GridItem>(true)
        }.Schedule();
    }
}
