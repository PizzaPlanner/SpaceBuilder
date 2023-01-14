using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class IconDisplayManager : MonoBehaviour
{
    public struct MoveWorldTag : IComponentData { }
    private ModelDisplayWorld iconWorld;
    void Start()
    {
        // iconWorld = new ModelDisplayWorld();
        GetComponent<Camera>().enabled = false;
    }

    public void Display(Entity prefabToShow, World fromWorld, float3 position, RenderTexture rt)
    {
        var cam = GetComponent<Camera>();
        cam.targetTexture = rt;
        cam.enabled = true;
        if (!iconWorld.HasDisplayEntity(prefabToShow))
        {
            var e = fromWorld.EntityManager.Instantiate(prefabToShow);
            fromWorld.EntityManager.AddComponent<MoveWorldTag>(e);
            using (var fromQuery = fromWorld.EntityManager.CreateEntityQuery(typeof(MoveWorldTag)))
            using (var toQuery = iconWorld.EntityManager.CreateEntityQuery(typeof(MoveWorldTag)))
            {
                iconWorld.EntityManager.MoveEntitiesFrom(fromWorld.EntityManager, fromQuery);
                var movedEntity = toQuery.ToEntityArray(Allocator.Temp)[0];
                iconWorld.MapDisplayEntity(prefabToShow, movedEntity);
            }
        }
        iconWorld.Display(prefabToShow, position);
        cam.Render();
        cam.enabled = false;
        cam.targetTexture = null;
    }

    void Update()
    {
        // iconWorld.Update();
    }
}

public class ModelDisplayWorld : World
{
    private Dictionary<Entity, Entity> ObjectToEntity;
    private EntityQuery notDisabledQuery, disabledQuery;
    private BlobAssetStore blobStore;
    public ModelDisplayWorld() : base("ModelDisplay", WorldFlags.Simulation)
    {
        blobStore = new BlobAssetStore();
        ObjectToEntity = new Dictionary<Entity, Entity>();
        DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(this,
            DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default));
        foreach(var system in GetExistingSystemManaged<SimulationSystemGroup>().Systems)
            if (!(system is TransformSystemGroup) &&
                !(system is EntityCommandBufferSystem))
            {
                GetExistingSystemManaged<SimulationSystemGroup>().RemoveSystemFromUpdateList(system);
                DestroySystem(system.SystemHandle);
            }

        disabledQuery = EntityManager.CreateEntityQuery(typeof(Disabled));
        notDisabledQuery = EntityManager.CreateEntityQuery(ComponentType.Exclude<Disabled>(), ComponentType.ReadOnly<LocalToWorld>());
    }

    public void Display(Entity prefab, float3 position)
    {
        if (!ObjectToEntity.ContainsKey(prefab))
            return;

        EntityManager.RemoveComponent<Disabled>(disabledQuery);
        EntityManager.SetComponentData(ObjectToEntity[prefab], new LocalTransform { Position = position });
        Update();
        EntityManager.AddComponent<Disabled>(notDisabledQuery);
    }

    public new void Dispose()
    {
        blobStore.Dispose();
        base.Dispose();
    }

    public void MapDisplayEntity(Entity prefab, Entity display)
    {
        ObjectToEntity[prefab] = display;
    }

    public bool HasDisplayEntity(Entity prefab) => ObjectToEntity.ContainsKey(prefab);
}
