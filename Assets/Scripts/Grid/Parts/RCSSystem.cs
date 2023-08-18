using Grid.Logic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct RCS : IComponentData
{
    public float Strength;
}

[UpdateInGroup(typeof(LogicSystemGroup))]
public partial struct RCSSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        foreach(var (rcs, ports, parent) in SystemAPI.Query<RCS, DynamicBuffer<LogicPort>, Parent>())
        {
            var accellerationForce = new float3(rcs.Strength);
            foreach (var port in ports)
            {
                if (port.Type == PortType.Pitch)
                    accellerationForce.x *= (float)port.Data.AsDouble;
                else if (port.Type == PortType.Yaw)
                    accellerationForce.y *= (float)port.Data.AsDouble;
                else if (port.Type == PortType.Roll)
                    accellerationForce.z *= (float)port.Data.AsDouble;
            }

            var ship = SystemAPI.GetComponentRW<MoveableShip>(parent.Value);
            ship.ValueRW.AngularAcceleration += accellerationForce / SystemAPI.Time.DeltaTime;
        };
    }
}