using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Transforms;

[GenerateAuthoringComponent]
public struct Thruster : IComponentData
{
    public Entity Port;
    public float RequiredPower;
    public float Strength;
    public float3 ForwardThrust;
}

public partial class ThrusterSystem : SystemBase
{
    private EndSimulationEntityCommandBufferSystem bufferSystem;
    protected override void OnCreate()
    {
        bufferSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        var buffer = bufferSystem.CreateCommandBuffer();
        Entities.ForEach((in Thruster thrust, in GridItem grid) =>
        {
            if (!HasComponent<Flyable>(grid.Grid) ||
                !HasComponent<PhysicsVelocity>(grid.Grid) ||
                !HasComponent<PhysicsMass>(grid.Grid))
                return;

            var velocity = GetComponent<PhysicsVelocity>(grid.Grid);
            var mass = GetComponent<PhysicsMass>(grid.Grid);
            velocity.ApplyLinearImpulse(mass, thrust.ForwardThrust);
            SetComponent(grid.Grid, velocity);
        }).Schedule();

        Entities.ForEach((ref Thruster thrust, in LocalToWorld ltw) =>
        {
            var port = GetComponent<ElectricityPort>(thrust.Port);
            port.Value = -thrust.RequiredPower;
            SetComponent(thrust.Port, port);

            thrust.ForwardThrust = port.NetworkOperational && port.On ? -ltw.Forward * thrust.Strength : float3.zero;
        }).Schedule();

        bufferSystem.AddJobHandleForProducer(Dependency);
    }
}
