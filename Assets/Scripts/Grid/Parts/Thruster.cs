using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Transforms;

public struct Thruster : IComponentData
{
    public bool On;
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
        foreach(var (thrust, parent) in SystemAPI.Query<Thruster, Parent>())
        {
            if (!SystemAPI.HasComponent<PhysicsVelocity>(parent.Value) ||
                !SystemAPI.HasComponent<PhysicsMass>(parent.Value))
                return;

            var velocity = SystemAPI.GetComponent<PhysicsVelocity>(parent.Value);
            var mass = SystemAPI.GetComponent<PhysicsMass>(parent.Value);
            velocity.ApplyLinearImpulse(mass, thrust.ForwardThrust);
            SystemAPI.SetComponent(parent.Value, velocity);
        };

        foreach(var (thrust, ltw) in SystemAPI.Query<RefRW<Thruster>, LocalToWorld>())
        {
            thrust.ValueRW.ForwardThrust = thrust.ValueRW.On ? 
                -ltw.Forward * thrust.ValueRW.Strength * thrust.ValueRW.MaxStrength : float3.zero;
        }
    }
}
