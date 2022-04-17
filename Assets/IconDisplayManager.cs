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
    private ModelDisplayWorld iconWorld;
    void Start()
    {
        iconWorld = new ModelDisplayWorld();
        GetComponent<Camera>().enabled = false;
    }

    public void Display(GameObject prefabToShow, float3 position, RenderTexture rt)
    {
        var cam = GetComponent<Camera>();
        cam.targetTexture = rt;
        cam.enabled = true;
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
    private Dictionary<GameObject, Entity> ObjectToEntity;
    private EntityQuery notDisabledQuery, disabledQuery;
    private BlobAssetStore blobStore;
    public ModelDisplayWorld() : base("ModelDisplay", WorldFlags.Simulation)
    {
        blobStore = new BlobAssetStore();
        ObjectToEntity = new Dictionary<GameObject, Entity>();
        DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(this,
            DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default));
        // var layersystem = CreateSystem<AssignUILayerSystem>();
        // GetExistingSystem<PresentationSystemGroup>().AddSystemToUpdateList(layersystem);
        foreach(var system in GetExistingSystem<SimulationSystemGroup>().Systems)
            if (system is not TransformSystemGroup &&
                system is not EntityCommandBufferSystem)
            {
                GetExistingSystem<SimulationSystemGroup>().RemoveSystemFromUpdateList(system);
                DestroySystem(system);
            }

        disabledQuery = EntityManager.CreateEntityQuery(typeof(Disabled));
        notDisabledQuery = EntityManager.CreateEntityQuery(ComponentType.Exclude<Disabled>(), ComponentType.ReadOnly<LocalToWorld>());
    }

    public Entity Display(GameObject prefab, float3 position)
    {
        EntityManager.AddComponent<Disabled>(notDisabledQuery);
        Entity e = Entity.Null;
        if (prefab == null)
            return e;

        if (ObjectToEntity.ContainsKey(prefab))
        {
            e = ObjectToEntity[prefab];
            Entity child;
            while ((child = EntityManager.GetInChildren<Disabled>(e)) != Entity.Null)
                EntityManager.RemoveComponent<Disabled>(child);
        }
        else
        {
            var prefabE = GameObjectConversionUtility.ConvertGameObjectHierarchy(prefab,
            GameObjectConversionSettings.FromWorld(this, blobStore));
            e = EntityManager.Instantiate(prefabE);
            EntityManager.DestroyEntity(prefabE);
            var entities = notDisabledQuery.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                if(!EntityManager.HasComponent<RenderMesh>(entity))
                    continue;

                var rm = EntityManager.GetSharedComponentData<RenderMesh>(entity);
                rm.layer = 5;
                EntityManager.SetSharedComponentData(entity, rm);
            }
            EntityManager.SetComponentData(e, new Translation{Value = position});
            ObjectToEntity[prefab] = e;
        }

        Update();
        return e;
    }

    public new void Dispose()
    {
        blobStore.Dispose();
        base.Dispose();
    }
}
