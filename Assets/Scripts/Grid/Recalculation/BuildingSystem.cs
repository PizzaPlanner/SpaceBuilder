using Unity.Entities;
using Unity.Physics;

internal partial struct BuildingSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        state.RequireForUpdate<IdentifiableMappingSystem.Loaded>();
        state.RequireForUpdate<RoomsLookup>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                           .CreateCommandBuffer(state.WorldUnmanaged);
        new BuildJob
        {
            ecb = ecb,
            colliders = SystemAPI.GetComponentLookup<PhysicsCollider>(true),
            Rooms = SystemAPI.GetSingletonRW<RoomsLookup>().ValueRW
        }.Schedule();

        new UnbuildJob
        {
            ecb = ecb,
            Rooms = SystemAPI.GetSingletonRW<RoomsLookup>().ValueRW
        }.Schedule();
    }
}