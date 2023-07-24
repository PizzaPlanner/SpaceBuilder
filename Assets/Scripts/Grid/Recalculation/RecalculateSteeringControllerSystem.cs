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

    EntityQuery _thrusterQuery;
    protected override void OnCreate()
    {
        _thrusterQuery = GetEntityQuery(typeof(Thruster), typeof(Parent), typeof(LocalTransform));
        RequireForUpdate<GridUpdate>();
        RequireForUpdate(_thrusterQuery);
    }

    protected override void OnUpdate()
    {

        new SteeringJob
        {
            entities = _thrusterQuery.ToEntityArray(Allocator.TempJob),
            thrusters = _thrusterQuery.ToComponentDataArray<Thruster>(Allocator.TempJob),
            parents = _thrusterQuery.ToComponentDataArray<Parent>(Allocator.TempJob),
            rotations = _thrusterQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob)
        }.Schedule();
    }
}
