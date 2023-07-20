using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Transforms;

public struct Thruster : IComponentData
{
    public Entity Port;
    public float RequiredPower;
    public float Strength;
    public float MaxStrength;
    public float3 ForwardThrust;
    public GridItem.Side DefaultDirection;

    public GridItem.Side Direction(quaternion rotation) => DefaultDirection.Rotate(rotation);
}

public partial class ThrusterSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var buffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                              .CreateCommandBuffer(World.Unmanaged);
        Entities.ForEach((in Thruster thrust, in GridItem grid) =>
        {
            if (!SystemAPI.HasComponent<Flyable>(grid.Grid) ||
                !SystemAPI.HasComponent<PhysicsVelocity>(grid.Grid) ||
                !SystemAPI.HasComponent<PhysicsMass>(grid.Grid))
                return;

            var velocity = SystemAPI.GetComponent<PhysicsVelocity>(grid.Grid);
            var mass = SystemAPI.GetComponent<PhysicsMass>(grid.Grid);
            velocity.ApplyLinearImpulse(mass, thrust.ForwardThrust);
            SystemAPI.SetComponent(grid.Grid, velocity);
        }).Schedule();

        Entities.ForEach((ref Thruster thrust, in LocalToWorld ltw) =>
        {
            var port = SystemAPI.GetComponent<ElectricityPort>(thrust.Port);
            port.Value = -thrust.RequiredPower;
            SystemAPI.SetComponent(thrust.Port, port);

            thrust.ForwardThrust = port.NetworkOperational && port.On ? -ltw.Forward * thrust.Strength * thrust.MaxStrength : float3.zero;
        }).Schedule();
    }
}
