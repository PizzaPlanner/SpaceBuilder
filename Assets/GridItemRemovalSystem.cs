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

public struct CurrentPlayerTarget : IComponentData
{
    public bool ToRemove;
}


[UpdateAfter(typeof(GridItemTargetAssignmentSystem))]
[UpdateBefore(typeof(GridItemTargetCleanupSystem))]
public class GridItemRemovalSystem : SystemBase
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

    protected override void OnStartRunning()
    {
        foreach(var renderer in GhostBuilder.WireframeDisplay.gameObject.GetComponentsInChildren<MeshRenderer>())
            if (renderer.gameObject.layer == LayerMask.NameToLayer("Grid"))
                renderer.sharedMaterial = settings.GridDestroyMaterial;
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

        modifiedEntities.Clear();
        if(GhostBuilder.WireframeDisplay)
            GhostBuilder.WireframeDisplay.gameObject.SetActive(false);
    }

    protected override void OnUpdate()
    {
        var buffer = bufferSystem.CreateCommandBuffer();
        var childBuffer = GetBufferFromEntity<Child>();
        var cpt = GetComponentDataFromEntity<CurrentPlayerTarget>();
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
            GhostBuilder.WireframeDisplay.gameObject.SetActive(false);
        }).WithoutBurst().Run();

        Entities.ForEach((Entity e, in LocalToWorld gridLtw, in GridItem gridItem, in CurrentPlayerTarget target) =>
        {
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
                Transform wireframe = GhostBuilder.WireframeDisplay.transform;
                wireframe.position = gridLtw.Position;
                wireframe.rotation = gridLtw.Rotation;
                wireframe.localScale = new float3(gridItem.Size);
                GhostBuilder.WireframeDisplay.gameObject.SetActive(true);
            }

        }).WithoutBurst().Run();

        if (Input.GetMouseButtonDown(0))
        {
            EntityManager.DestroyEntity(modifiedEntities);
        }
    }
}

[UpdateBefore(typeof(GridItemTargetCleanupSystem))]
public class GridItemTargetAssignmentSystem : SystemBase
{
    BuildPhysicsWorld physicsWorld;
    private const float TARGET_MAX_RANGE = 8;
    private CollisionFilter filter;

    protected override void OnCreate()
    {
        RequireSingletonForUpdate<LocalPlayer>();
        RequireSingletonForUpdate<CameraAnchor>();
        physicsWorld = World.GetExistingSystem<BuildPhysicsWorld>();
        filter = CollisionFilter.Default;
        filter.CollidesWith = ~(1u << 3);
        filter.BelongsTo = ~0u;
    }

    protected override void OnStopRunning()
    {
        EntityManager.RemoveComponent<CurrentPlayerTarget>(GetEntityQuery(typeof(CurrentPlayerTarget)));
    }

    protected override void OnUpdate()
    {
        if(!Input.GetKey(KeyCode.LeftShift))
            Entities.ForEach((ref CurrentPlayerTarget target) =>
            {
                target.ToRemove = true;
            }).Run();

        var ltw = GetComponent<LocalToWorld>(GetSingletonEntity<CameraAnchor>());
        var collisionWorld = physicsWorld.PhysicsWorld.CollisionWorld;
        var input = new RaycastInput
        {
            Start = ltw.Position,
            End = ltw.Position + ltw.Forward * TARGET_MAX_RANGE,
            Filter = filter

        };

        var target = Entity.Null;
        if (collisionWorld.CastRay(input, out var hit))
            target = this.FindParentWith<GridItem>(hit.Entity);

        if (target == Entity.Null)
            return;

        if (HasComponent<CurrentPlayerTarget>(target))
            SetComponent(target, new CurrentPlayerTarget{ToRemove = false});
        else
            EntityManager.AddComponentData(target, new CurrentPlayerTarget{ToRemove = false});
    }
}

public class GridItemTargetCleanupSystem : SystemBase
{
    private EndSimulationEntityCommandBufferSystem bufferSystem;

    protected override void OnCreate()
    {
        bufferSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        var buffer = bufferSystem.CreateCommandBuffer();
        Entities.ForEach((Entity e, in CurrentPlayerTarget cpt) =>
        {
            if(cpt.ToRemove)
                buffer.RemoveComponent<CurrentPlayerTarget>(e);
        }).Schedule();
        bufferSystem.AddJobHandleForProducer(Dependency);
    }
}
