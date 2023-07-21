using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

public struct GridChangedEvent : IComponentData
{
    public Entity Grid;
    public int3 Position;
}

[ExecuteAlways]
[UpdateInGroup(typeof(TransformSystemGroup), OrderFirst = true)]
public partial class BeforeReparentingEntitiyCommandBufferSystem : EntityCommandBufferSystem { }

public partial struct BuildingSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        state.RequireForUpdate<IdentifiableMappingSystem.Loaded>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                           .CreateCommandBuffer(state.WorldUnmanaged);
        new BuildJob
        {
            ecb = ecb,
            gridItems = state.GetComponentLookup<GridItem>(true),
            colliders = state.GetComponentLookup<PhysicsCollider>(true),
            prefabMap = IdentifiableMappingSystem.EntitiesById,
        }.Schedule();
    }
}