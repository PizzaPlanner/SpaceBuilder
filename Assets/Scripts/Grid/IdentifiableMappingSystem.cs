using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class IdentifiableMappingSystem : SystemBase
{
    public struct Loaded : IComponentData { }
    public static NativeParallelHashMap<ulong, Entity> EntitiesById;
    public static Dictionary<ulong, Texture2D> IconById;
    private EntityQuery query;

    protected override void OnCreate()
    {
        query = GetEntityQuery(typeof(Identifieable), typeof(Prefab));
        RequireForUpdate(query);
    }

    protected override void OnDestroy()
    {
        EntitiesById.Dispose();
    }

    protected override void OnStopRunning()
    {
        EntityManager.CreateEntity(typeof(Loaded));
    }

    protected override void OnUpdate()
    {
        var entities = query.ToEntityArray(Allocator.Temp);
        Debug.Log($"Found {entities.Length} ID Prefabs");
        EntitiesById = new NativeParallelHashMap<ulong, Entity>(entities.Length, Allocator.Persistent);
        Entities
            .WithEntityQueryOptions(EntityQueryOptions.IncludePrefab)
            .WithAll<Prefab>()
            .ForEach((Entity e, in Identifieable id) =>
            {
                if (HasComponent<Parent>(e) && GetComponent<Parent>(e).Value != Entity.Null)
                    return;

                if (EntitiesById.ContainsKey(id.ItemId))
                {
#if UNITY_EDITOR
                    Debug.LogError($"ID {id.ItemId} used by {EntityManager.GetName(e)} is already taken!");
#else
                    Debug.LogError($"ID {id.ItemId} used by {e} is already taken!");
#endif
                }
                else
                    EntitiesById.Add(id.ItemId, e);

                // if (HasComponent<LocalToWorld>(e))
                //     EntityManager.RemoveComponent<LocalToWorld>(e);
                // if (HasComponent<Translation>(e))
                //     EntityManager.RemoveComponent<Translation>(e);
                // if (HasComponent<Rotation>(e))
                //     EntityManager.RemoveComponent<Rotation>(e);
            }).WithStructuralChanges().WithoutBurst().Run();

        IconById = new Dictionary<ulong, Texture2D>();
        Entities
            .WithEntityQueryOptions(EntityQueryOptions.IncludePrefab)
            .WithAll<Prefab>()
            .ForEach((Entity iconEntity, in IconReference icon, in Identifieable id) =>
        {
            IconById[id.ItemId] = icon.Icon;
            // EntityManager.RemoveComponent<IconReference>(iconEntity);
        }).WithStructuralChanges().WithoutBurst().Run();

        Enabled = false;
    }
}
