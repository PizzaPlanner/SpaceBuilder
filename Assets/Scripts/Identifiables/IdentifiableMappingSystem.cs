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
    internal static NativeParallelHashMap<Identifieable, IdTag> TagsById;
    internal static Dictionary<Identifieable, Texture2D> IconById;
    internal static NativeParallelHashMap<Identifieable, FixedString64Bytes> NameById;
    internal static NativeArray<IdentifiableLink> IdRelations;

    protected override void OnCreate()
    {
        query = SystemAPI.QueryBuilder()
                         .WithAll<Prefab, PreIdentifiable>()
                         .WithOptions(EntityQueryOptions.IncludePrefab).Build();
        RequireForUpdate(query);
        EntitiesById = new NativeParallelHashMap<Identifieable, Entity>(0, Allocator.Persistent);
        NameById = new NativeParallelHashMap<Identifieable, FixedString64Bytes>(0, Allocator.Persistent);
        TagsById = new NativeParallelHashMap<Identifieable, IdTag>(0, Allocator.Persistent);
    }

    protected override void OnDestroy()
    {
        EntitiesById.Dispose();
        NameById.Dispose();
        TagsById.Dispose();
        IdRelations.Dispose();
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
        List<FixedString64Bytes> allNames =new List<FixedString64Bytes>();
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (preId, e) in SystemAPI.Query<PreIdentifiable>()
                                            .WithOptions(EntityQueryOptions.IncludePrefab)
                                            .WithAll<Prefab>()
                                            .WithEntityAccess())
        {
                if (SystemAPI.HasComponent<Parent>(e) && SystemAPI.GetComponent<Parent>(e).Value != Entity.Null)
                    return;
                
                var id = preId.Id;

                if(id.ItemId == 0)
                    Debug.LogError($"Entity {e} has invalid ID 0");
                else if (EntitiesById.ContainsKey(id))
                    Debug.LogError($"ID {id.ItemId} used by {e} is already taken!");
                else if(allNames.Contains(preId.Name.Value))
                    Debug.LogError($"ID {id.ItemId} name {preId.Name.Value} is already taken!");
                else
                {
                    ecb.AddComponent(e, id);
                    EntitiesById.Add(id, e);
                    NameById.Add(id, preId.Name.Value);
                    TagsById.Add(id, preId.Tags);
                    allNames.Add(preId.Name.Value);
#if UNITY_EDITOR
                    EntityManager.SetName(e, preId.Name.Value);
#endif
                }

                if (SystemAPI.ManagedAPI.HasComponent<IconReference>(e))
                    IconById[id] = SystemAPI.ManagedAPI.GetComponent<IconReference>(e).Icon;

        }
        ecb.Playback(EntityManager);
        EntityManager.RemoveComponent<PreIdentifiable>(query);

        var links = SystemAPI.GetSingletonBuffer<DefaultIdentifiableLink>();
        var allRelations = new NativeList<IdentifiableLink>(Allocator.Temp);
        foreach (var link in links)
        {
            allRelations.Add(new IdentifiableLink{From = link.From, To = link.To, Relation = link.RelationFrom});
            allRelations.Add(new IdentifiableLink{From = link.To, To = link.From, Relation = link.RelationTo});
        }
        IdRelations = allRelations.ToArray(Allocator.Persistent);
        EntityManager.DestroyEntity(SystemAPI.GetSingletonEntity<DefaultIdentifiableLink>());
        Debug.Log($"Loaded {IdRelations.Length} ID Links");
        
        Enabled = false;
    }
}
