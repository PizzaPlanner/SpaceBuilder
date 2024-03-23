using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class IdentifiableMappingSystem : SystemBase
{
    public struct Loaded : IComponentData { }

    private EntityQuery query;
    
        
    internal static NativeParallelHashMap<Identifieable, Entity> EntitiesById;
    internal static Dictionary<Identifieable, Texture2D> IconById;
    internal static NativeParallelHashMap<Identifieable, FixedString64Bytes> NameById;

    protected override void OnCreate()
    {
        query = GetEntityQuery(typeof(Identifieable), typeof(DefaultName), typeof(Prefab));
        RequireForUpdate(query);
        EntitiesById = new NativeParallelHashMap<Identifieable, Entity>(0, Allocator.Persistent);
        NameById = new NativeParallelHashMap<Identifieable, FixedString64Bytes>(0, Allocator.Persistent);
    }

    protected override void OnDestroy()
    {
        EntitiesById.Dispose();
        NameById.Dispose();
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
        IconById = new Dictionary<Identifieable, Texture2D>();
        foreach (var (id, defaultName, e) in SystemAPI.Query<Identifieable, DefaultName>()
                                         .WithOptions(EntityQueryOptions.IncludePrefab)
                                         .WithAll<Prefab>()
                                         .WithEntityAccess())
        {
                if (SystemAPI.HasComponent<Parent>(e) && SystemAPI.GetComponent<Parent>(e).Value != Entity.Null)
                    return;

                if(id.ItemId == 0)
                    Debug.LogError($"Entity {e} has invalid ID 0");
                else if (EntitiesById.ContainsKey(id))
                    Debug.LogError($"ID {id.ItemId} used by {e} is already taken!");
                else
                {
                    EntitiesById.Add(id, e);
                    NameById.Add(id, defaultName.Value);
#if UNITY_EDITOR
                    EntityManager.SetName(e, defaultName.Value);
#endif
                }

                if (SystemAPI.ManagedAPI.HasComponent<IconReference>(e))
                    IconById[id] = SystemAPI.ManagedAPI.GetComponent<IconReference>(e).Icon;

        }
        EntityManager.RemoveComponent<DefaultName>(query);
        EntityManager.RemoveComponent<IconReference>(GetEntityQuery(typeof(IconReference), typeof(Identifieable)));
        Enabled = false;
    }
}
