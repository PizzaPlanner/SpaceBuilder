using Grid.Logic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct RCS : IComponentData
{
    public float Strength;
}

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
                    accellerationForce.x *= port.Data;
                else if (port.Type == PortType.Yaw)
                    accellerationForce.y *= port.Data;
                else if (port.Type == PortType.Roll)
                    accellerationForce.z *= port.Data;
            }

            var ship = SystemAPI.GetComponentRW<MoveableShip>(parent.Value);
            ship.ValueRW.AngularAcceleration += accellerationForce;
        };
    }
}