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
using UnityEngine.UIElements;
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
    private GhostPlacementSettings settings;

    private NativeList<Entity> modifiedEntities;

    private VisualElement border;

    protected override void OnCreate()
    {
        RequireForUpdate<BuilderMode_Destroy>();
        settings = Resources.Load<GhostPlacementSettings>("GhostPlacementSettings");
        modifiedEntities = new NativeList<Entity>(Allocator.Persistent);
    }

    protected override void OnDestroy()
    {
        modifiedEntities.Dispose();
    }

    protected override void OnStartRunning()
    {
        // foreach(var renderer in GhostBuilder.Wireframe.gameObject.GetComponentsInChildren<MeshRenderer>())
        //     if (renderer.gameObject.layer == LayerMask.NameToLayer("Grid"))
        //         renderer.sharedMaterial = settings.GridDestroyMaterial;
        // GhostBuilder.Wireframe.gameObject.SetActive(true);
        border ??= GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q("DestructionBorder");
        border.style.display = DisplayStyle.Flex;
    }

    protected override void OnStopRunning()
    {
        var buffer = GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        Entities.ForEach((Entity e, RenderMesh mesh, in ModifiedRenderMesh mrm) =>
        {
            var m2 = mesh;
            m2.material = mrm.PreviousMat;
            buffer.SetSharedComponentManaged(e, m2);
            buffer.RemoveComponent<ModifiedRenderMesh>(e);
        }).WithoutBurst().Run();
        modifiedEntities.Clear();
        // if(GhostBuilder.Wireframe)
        //     GhostBuilder.Wireframe.gameObject.SetActive(false);
        border.style.display = DisplayStyle.None;
    }

    protected override void OnUpdate()
    {
        // var parent = GetSingleton<BuilderMode_Destroy>().Grid;
        // var grid = GetComponent<Grid>(parent);
        // var gridLtw = GetComponent<LocalToWorld>(parent);
        // var wireframe = GhostBuilder.Wireframe.transform;
        // wireframe.localScale = new float3(Grid.CELL_SIZE);
        // wireframe.rotation = gridLtw.Rotation;
        // wireframe.transform.position = GetComponent<LocalToWorld>(GetSingletonEntity<LocalPlayer>()).Position;

        var buffer = GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        var childBuffer = GetBufferLookup<Child>();
        var cpt = GetComponentLookup<TargetForRemoval>();
        Entities.ForEach((Entity e, RenderMesh mesh, in ModifiedRenderMesh mrm) =>
        {
            if (cpt.HasComponent(mrm.ParentGridItem))
                return;

            var m2 = mesh;
            m2.material = mrm.PreviousMat;
            buffer.SetSharedComponentManaged(e, m2);
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
                    var rm = EntityManager.GetSharedComponentManaged<RenderMesh>(child);
                    buffer.AddComponent(child, new ModifiedRenderMesh{ParentGridItem = e, PreviousMat = rm.material});
                    rm.material = settings.GhostRemovalMaterial;
                    buffer.SetSharedComponentManaged(child, rm);
                }

            if (hasChildrenWithRM)
            {
                modifiedEntities.Add(e);
            }

        }).WithoutBurst().Run();

        if (Input.GetMouseButtonDown(0))
        {
            EntityManager.DestroyEntity(modifiedEntities);
            // TODO
            // EntityManager.CreateEntity(typeof(GridNeedsUpdate));
        }
    }
}

[UpdateBefore(typeof(GridItemTargetCleanupSystem))]
public partial class GridItemTargetAssignmentSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<LocalPlayer>();
        RequireForUpdate<CameraAnchor>();
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
    protected override void OnUpdate()
    {
        var buffer = GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        Entities.ForEach((Entity e, in TargetForRemoval cpt) =>
        {
            if(cpt.ToRemove)
                buffer.RemoveComponent<TargetForRemoval>(e);
        }).Schedule();
    }
}
