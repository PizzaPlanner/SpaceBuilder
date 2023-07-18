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
        EntitiesById = new NativeParallelHashMap<ulong, Entity>(0, Allocator.Persistent);
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
        EntitiesById.Capacity = entities.Length;
        IconById = new Dictionary<ulong, Texture2D>();
        foreach (var (id, e) in SystemAPI.Query<Identifieable>()
                                         .WithOptions(EntityQueryOptions.IncludePrefab)
                                         .WithAll<Prefab>()
                                         .WithEntityAccess())
        {
                if (SystemAPI.HasComponent<Parent>(e) && SystemAPI.GetComponent<Parent>(e).Value != Entity.Null)
                    return;

                if(id.ItemId == 0)
                    Debug.LogError($"Entity {e} has invalid ID 0");
                else if (EntitiesById.ContainsKey(id.ItemId))
                    Debug.LogError($"ID {id.ItemId} used by {e} is already taken!");
                else
                    EntitiesById.Add(id.ItemId, e);

                if (SystemAPI.ManagedAPI.HasComponent<IconReference>(e))
                    IconById[id.ItemId] = SystemAPI.ManagedAPI.GetComponent<IconReference>(e).Icon;

        }

        Enabled = false;
    }
}
