using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using Material = UnityEngine.Material;

public struct TargetForRemoval : IComponentData
{
    public bool ToRemove;
}


[UpdateAfter(typeof(GridItemTargetAssignmentSystem))]
[UpdateBefore(typeof(GridItemTargetCleanupSystem))]
public partial class GridItemRemovalSystem : SystemBase
{
    public class ModifiedRenderMesh : IComponentData
    {
        public Material PreviousMat;
        public Entity ParentGridItem;
    }

    private EndSimulationEntityCommandBufferSystem bufferSystem;
    private GhostPlacementSettings settings;

    private NativeList<Entity> modifiedEntities;

    protected override void OnCreate()
    {
        RequireSingletonForUpdate<BuilderMode_Destroy>();
        bufferSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
        settings = Resources.Load<GhostPlacementSettings>("GhostPlacementSettings");
        modifiedEntities = new NativeList<Entity>(Allocator.Persistent);
    }

    protected override void OnDestroy()
    {
        modifiedEntities.Dispose();
    }

    protected override void OnStartRunning()
    {
        foreach(var renderer in GhostBuilder.Wireframe.gameObject.GetComponentsInChildren<MeshRenderer>())
            if (renderer.gameObject.layer == LayerMask.NameToLayer("Grid"))
                renderer.sharedMaterial = settings.GridDestroyMaterial;
        GhostBuilder.Wireframe.gameObject.SetActive(true);
    }

    protected override void OnStopRunning()
    {
        var buffer = bufferSystem.CreateCommandBuffer();
        Entities.ForEach((Entity e, RenderMesh mesh, in ModifiedRenderMesh mrm) =>
        {
            var m2 = mesh;
            m2.material = mrm.PreviousMat;
            buffer.SetSharedComponent(e, m2);
            buffer.RemoveComponent<ModifiedRenderMesh>(e);
        }).WithoutBurst().Run();
        bufferSystem.AddJobHandleForProducer(Dependency);
        modifiedEntities.Clear();
        if(GhostBuilder.Wireframe)
            GhostBuilder.Wireframe.gameObject.SetActive(false);
    }

    protected override void OnUpdate()
    {
        var parent = GetSingleton<BuilderMode_Destroy>().Grid;
        var grid = GetComponent<Grid>(parent);
        var gridLtw = GetComponent<LocalToWorld>(parent);
        var wireframe = GhostBuilder.Wireframe.transform;
        wireframe.localScale = new float3(Grid.CellSize(grid.MaxSize));
        wireframe.rotation = gridLtw.Rotation;
        wireframe.transform.position = GetComponent<LocalToWorld>(GetSingletonEntity<LocalPlayer>()).Position;

        var buffer = bufferSystem.CreateCommandBuffer();
        var childBuffer = GetBufferFromEntity<Child>();
        var cpt = GetComponentDataFromEntity<TargetForRemoval>();
        Entities.ForEach((Entity e, RenderMesh mesh, in ModifiedRenderMesh mrm) =>
        {
            if (cpt.HasComponent(mrm.ParentGridItem))
                return;

            var m2 = mesh;
            m2.material = mrm.PreviousMat;
            buffer.SetSharedComponent(e, m2);
            buffer.RemoveComponent<ModifiedRenderMesh>(e);

            var modIdx = modifiedEntities.IndexOf(mrm.ParentGridItem);
            if(modIdx > -1)
                modifiedEntities.RemoveAt(modIdx);
            GhostBuilder.Wireframe.gameObject.SetActive(false);
        }).WithoutBurst().Run();

        Entities.ForEach((Entity e, in LocalToWorld gridLtw, in GridItem gridItem, in TargetForRemoval target) =>
        {
            GhostBuilder.Wireframe.transform.position = gridLtw.Position;
            if (modifiedEntities.Contains(e))
                return;

            var hasChildrenWithRM = false;
            foreach(var child in childBuffer.GetAllChildren(e))
                if (EntityManager.HasComponent<RenderMesh>(child))
                {
                    hasChildrenWithRM = true;
                    var rm = EntityManager.GetSharedComponentData<RenderMesh>(child);
                    buffer.AddComponent(child, new ModifiedRenderMesh{ParentGridItem = e, PreviousMat = rm.material});
                    rm.material = settings.GhostRemovalMaterial;
                    buffer.SetSharedComponent(child, rm);
                }

            if (hasChildrenWithRM)
            {
                modifiedEntities.Add(e);
            }

        }).WithoutBurst().Run();
        bufferSystem.AddJobHandleForProducer(Dependency);

        if (Input.GetMouseButtonDown(0))
        {
            EntityManager.DestroyEntity(modifiedEntities);
            EntityManager.CreateEntity(typeof(GridNeedsUpdate));
        }
    }
}

[UpdateBefore(typeof(GridItemTargetCleanupSystem))]
public partial class GridItemTargetAssignmentSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireSingletonForUpdate<LocalPlayer>();
        RequireSingletonForUpdate<CameraAnchor>();
    }

    protected override void OnStopRunning()
    {
        EntityManager.RemoveComponent<TargetForRemoval>(GetEntityQuery(typeof(TargetForRemoval)));
    }

    protected override void OnUpdate()
    {
        if(!Input.GetKey(KeyCode.LeftShift))
            Entities.ForEach((ref TargetForRemoval target) =>
            {
                target.ToRemove = true;
            }).Run();

        var target = GetComponent<Target>(GetSingletonEntity<LocalPlayer>()).Value;
        if (target == Entity.Null)
            return;

        if (HasComponent<TargetForRemoval>(target))
            SetComponent(target, new TargetForRemoval{ToRemove = false});
        else
            EntityManager.AddComponentData(target, new TargetForRemoval{ToRemove = false});
    }
}

public partial class GridItemTargetCleanupSystem : SystemBase
{
    private EndSimulationEntityCommandBufferSystem bufferSystem;

    protected override void OnCreate()
    {
        bufferSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        var buffer = bufferSystem.CreateCommandBuffer();
        Entities.ForEach((Entity e, in TargetForRemoval cpt) =>
        {
            if(cpt.ToRemove)
                buffer.RemoveComponent<TargetForRemoval>(e);
        }).Schedule();
        bufferSystem.AddJobHandleForProducer(Dependency);
    }
}
