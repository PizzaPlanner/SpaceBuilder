using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;

[UpdateBefore(typeof(TransformSystemGroup))]
public class CalculateBuildingSlotSystem : SystemBase
{
    BuildPhysicsWorld physicsWorld;
    private GhostPlacementSettings settings;
    private CollisionFilter filter;

    private List<MeshRenderer> gridRenderers;

    protected override void OnCreate()
    {
        RequireSingletonForUpdate<GhostPlacementBuilder>();
        RequireSingletonForUpdate<CameraAnchor>();
        physicsWorld = World.GetExistingSystem<BuildPhysicsWorld>();
        settings = Resources.Load<GhostPlacementSettings>("GhostPlacementSettings");
        filter = CollisionFilter.Default;
        filter.CollidesWith = ~(1u << 3);
        filter.BelongsTo = ~0u;
        gridRenderers = new List<MeshRenderer>();
    }

    protected override void OnStartRunning()
    {
        gridRenderers.Clear();
        foreach(var renderer in GhostBuilder.WireframeDisplay.gameObject.GetComponentsInChildren<MeshRenderer>())
            if (renderer.gameObject.layer == LayerMask.NameToLayer("Grid"))
                gridRenderers.Add(renderer);
    }

    protected override void OnStopRunning()
    {
        if(GhostBuilder.WireframeDisplay)
            GhostBuilder.WireframeDisplay.gameObject.SetActive(false);
    }

    protected override void OnUpdate()
    {
        var ltw = GetComponent<LocalToWorld>(GetSingletonEntity<CameraAnchor>());
        var ghost = GetSingletonEntity<GhostPlacementBuilder>();
        var gridEntity = GetComponent<Parent>(ghost).Value;
        var gridLtw = GetComponent<LocalToWorld>(gridEntity);
        var grid = GetComponent<Grid>(gridEntity);
        var gridItem = GetComponent<GridItem>(ghost);
        var gridItemLtp = GetComponent<LocalToParent>(ghost);
        var bgp = EntityManager.GetComponentData<GhostPlacementBuilder>(ghost);

        var collisionWorld = physicsWorld.PhysicsWorld.CollisionWorld;

        float3 target = ltw.Position + ltw.Forward * Grid.CellSize(grid) * bgp.Range;
        // var input = new RaycastInput
        // {
        //     Start = ltw.Position,
        //     End = ltw.Position + ltw.Forward * Grid.CellSize(grid) * bgp.Range,
        //     Filter = filter
        // };
        // if (collisionWorld.CastRay(input, out var hit))
        // {
        //     var occupied = Grid.CellFromPosition(gridLtw, hit.Position, gridItem.Size);
        //     var me = Grid.CellFromPosition(gridLtw, ltw.Position, gridItem.Size);
        //     var diff = occupied - me;
        //     var diffAbs = math.abs(diff);
        //
        //     bgp.CurrentSlot = occupied += new int3(
        //         diffAbs.x >= diffAbs.y && diffAbs.x >= diffAbs.z ? (int)math.sign(diff.x) : 0,
        //         diffAbs.y >= diffAbs.x && diffAbs.y >= diffAbs.z ? (int)math.sign(diff.y) : 0,
        //         diffAbs.z >= diffAbs.x && diffAbs.z >= diffAbs.y ? (int)math.sign(diff.z) : 0
        //     );
        //     Debug.Log("Diff> "+diff);
        // }
        // else
        {
            gridItem.Area.Origin = Grid.CellFromPosition(grid, gridLtw, target);
        }

        var newPos = new Translation { Value = Grid.PositionFromCell(grid, new LocalToWorld(), gridItem.Area.Origin) };

        SetComponent(ghost, newPos);
        SetComponent(ghost, gridItem);

        var canBuild = true;
        foreach (var item in GetBuffer<OccupationBuffer>(gridEntity))
            canBuild &= !(gridItem.Area.Contains(item.Location) && (item.Value & gridItem.Occupied) > 0);
        bgp.Buildable = canBuild;
        SetSingleton(bgp);

        Transform wireframe = GhostBuilder.WireframeDisplay.transform;
        wireframe.position = Grid.PositionFromCell(grid, gridLtw, gridItem.Area.Origin);
        wireframe.rotation = gridLtw.Rotation;
        wireframe.localScale = new float3(Grid.CellSize(grid));
        foreach(var renderer in gridRenderers)
            renderer.sharedMaterial = canBuild ? settings.GridBuildMaterial : settings.GridDestroyMaterial;

        GhostBuilder.WireframeDisplay.gameObject.SetActive(true);

        if (canBuild && Input.GetMouseButtonDown(0))
        {
            EntityManager.DestroyEntity(GetSingletonEntity<GhostPlacementBuilder>());
            var e = World.GetExistingSystem<GhostPlacementSystem>().Build(gridEntity, newPos.Value, false);
            if(e != Entity.Null)
            {
                // SetComponent(e, gridItem);
                SetComponent(e, newPos);
                EntityManager.CreateEntity(typeof(GridsNeedUpdate));
#if UNITY_EDITOR
                EntityManager.SetName(e, $"[{gridItem.Area.Origin}]");
#endif
            }
        }
    }
}
