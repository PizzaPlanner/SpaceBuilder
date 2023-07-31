using Grid.Logic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct Thruster : IComponentData
{
    public float MaxStrength;
    public float3 Forward;
}

public partial struct ThrusterSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        foreach(var (thrust, ports, parent) in SystemAPI.Query<Thruster, DynamicBuffer<LogicPort>, Parent>())
        {
            var accellerationForce = thrust.Forward * thrust.MaxStrength;
            foreach (var port in ports)
            {
                if (port.Type == PortType.Ratio)
                    accellerationForce *= (float)port.Data.AsDouble;
            }

            var ship = SystemAPI.GetComponentRW<MoveableShip>(parent.Value);
            ship.ValueRW.LinearAcceleration += accellerationForce;
        };

        foreach(var (thrust, transform) in SystemAPI.Query<RefRW<Thruster>, LocalTransform>().WithChangeFilter<LocalTransform>())
        {
            thrust.ValueRW.Forward = -transform.Forward();
        }
    }
}