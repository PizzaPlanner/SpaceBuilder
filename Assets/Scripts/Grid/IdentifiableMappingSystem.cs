using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class IdentifiableMappingSystem : SystemBase
{
    public struct Loaded : IComponentData { }
    public static NativeHashMap<long, Entity> EntitiesById;
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
        var ids = query.ToComponentDataArray<Identifieable>(Allocator.Temp);
        Debug.Log($"Found {entities.Length} ID Prefabs");
        EntitiesById = new NativeHashMap<long, Entity>(entities.Length, Allocator.Persistent);
        for (int i = 0; i < entities.Length; i++)
        {
            var e = entities[i];
            if (HasComponent<Parent>(e) && GetComponent<Parent>(e).Value != Entity.Null)
                continue;

#if UNITY_EDITOR
            if(HasComponent<Name>(entities[i]))
                EntityManager.SetName(entities[i], GetComponent<Name>(e).Value.Value);
#endif
            var id = ids[i].ItemId;
            if(EntitiesById.ContainsKey(id))
#if UNITY_EDITOR
                Debug.LogError($"ID {id} used by {EntityManager.GetName(e)} is already taken!");
#else
                Debug.LogError($"ID {id} used by {entities[i]} is already taken!");
#endif
            else
                EntitiesById.Add(id, e);
        }

        Enabled = false;
    }
}
