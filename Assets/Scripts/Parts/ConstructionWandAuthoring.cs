using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public struct ConstructionWandInitialize : IComponentData { }

public class ConstructionWandAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        var buffer = dstManager.AddBuffer<BuildableBuffer>(entity);
        dstManager.AddComponent<ConstructionWandInitialize>(entity);
        var query = dstManager.CreateEntityQuery(typeof(Identifieable), typeof(Prefab), typeof(GridItem));
        foreach (var part in query.ToComponentDataArray<Identifieable>(Allocator.Temp))
            buffer.Add(new BuildableBuffer{ PrefabId = part.ItemId });
        query.Dispose();
    }
}

[UpdateInGroup(typeof(InitializationSystemGroup))]
[UpdateAfter(typeof(IdentifiableMappingSystem))]
public partial class ConstructionWandInitializationSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireSingletonForUpdate<IdentifiableMappingSystem.Loaded>();
    }

    protected override void OnUpdate()
    {
        var query = GetEntityQuery(typeof(Identifieable), typeof(Prefab), typeof(GridItem));
        var ids = query.ToComponentDataArray<Identifieable>(Allocator.Temp);
        Entities
            .WithReadOnly(ids)
            .WithEntityQueryOptions(EntityQueryOptions.IncludePrefab)
            .WithAll<ConstructionWandInitialize>()
            .ForEach((Entity e, ref DynamicBuffer<BuildableBuffer> buildables) =>
            {
                foreach (var id in ids)
                    buildables.Add(new BuildableBuffer {PrefabId = id.ItemId});
                EntityManager.RemoveComponent<ConstructionWandInitialize>(e);
            }).WithStructuralChanges().WithoutBurst().Run();
    }
}
