using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

[UpdateInGroup(typeof(GridRecalculationSystemGroup))]
public partial class RecalculateSteeringControllerSystem : SystemBase
{
    partial struct SteeringJob : IJobEntity
    {
        [ReadOnly] public NativeArray<Entity> entities;
        [ReadOnly] public NativeArray<Parent> parents;
        [ReadOnly] public NativeArray<Thruster> thrusters;
        [ReadOnly] public NativeArray<LocalTransform> rotations;

        public void Execute(ref DynamicBuffer<ControlledItems> controller, in SteeringController steering, in Parent parent)
        {
            var grid = parent.Value;
            controller.Clear();
            for(int i = 0; i < entities.Length; i++)
                if (parents[i].Value == grid)
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
        RequireForUpdate<GridUpdate>();
    }

    protected override void OnUpdate()
    {
        var query = GetEntityQuery(typeof(Thruster), typeof(Parent), typeof(LocalTransform));

        new SteeringJob
        {
            entities = query.ToEntityArray(Allocator.TempJob),
            thrusters = query.ToComponentDataArray<Thruster>(Allocator.TempJob),
            parents = query.ToComponentDataArray<Parent>(Allocator.TempJob),
            rotations = query.ToComponentDataArray<LocalTransform>(Allocator.TempJob)
        }.Schedule();
    }
}
