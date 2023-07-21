using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct SteeringController : IComponentData
{
    public float3 SteeringAngle;
    public float3 Thrust;
}

public partial class SteeringSystem : SystemBase
{
    protected override void OnUpdate()
    {
        foreach(var(steer, parent, items) in 
                SystemAPI.Query<SteeringController, Parent, DynamicBuffer<ControlledItems>>())
        {
            var grid = parent.Value;
            var transform = SystemAPI.GetComponentRW<LocalTransform>(grid);
            transform.ValueRW.Rotation = math.mul(transform.ValueRW.Rotation, quaternion.Euler(steer.SteeringAngle));
            for (int i = 0; i < items.Length; i++)
            {
                var target = items[i].Target;
                if (!SystemAPI.HasComponent<Thruster>(target))
                    continue;

                var thruster = SystemAPI.GetComponentRW<Thruster>(target);
                if (items[i].Type == ToggleType.ThrusterForward)
                    thruster.ValueRW.Strength = steer.Thrust.z > 0 ? steer.Thrust.z : 0;
                if (items[i].Type == ToggleType.ThrusterBackward)
                    thruster.ValueRW.Strength = steer.Thrust.z < 0 ? -steer.Thrust.z : 0;
                if (items[i].Type == ToggleType.ThrusterUp)
                    thruster.ValueRW.Strength = steer.Thrust.y > 0 ? steer.Thrust.y : 0;
                if (items[i].Type == ToggleType.ThrusterDown)
                    thruster.ValueRW.Strength = steer.Thrust.y < 0 ? -steer.Thrust.y : 0;
                if (items[i].Type == ToggleType.ThrusterRight)
                    thruster.ValueRW.Strength = steer.Thrust.x > 0 ? steer.Thrust.x : 0;
                if (items[i].Type == ToggleType.ThrusterLeft)
                    thruster.ValueRW.Strength = steer.Thrust.x < 0 ? -steer.Thrust.x : 0;
                thruster.ValueRW.Strength = math.clamp(thruster.ValueRW.Strength, 0, 1);
            }
        }
    }
}