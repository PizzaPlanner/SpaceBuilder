using Unity.Entities;
using Unity.Physics;


public partial struct BuildingSystem : ISystem
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
            colliders = state.GetComponentLookup<PhysicsCollider>(true),
            prefabMap = IdentifiableMappingSystem.EntitiesById,
            Rooms = SystemAPI.GetSingletonRW<RoomsLookup>().ValueRW
        }.Schedule();
    }
}