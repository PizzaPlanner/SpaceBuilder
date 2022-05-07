using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;

[UpdateBefore(typeof(TransformSystemGroup))]
public partial class CalculateBuildingSlotSystem : SystemBase
{
    BuildPhysicsWorld physicsWorld;
    private CollisionFilter filter;

    protected override void OnCreate()
    {
        RequireSingletonForUpdate<GhostPlacementBuilder>();
        RequireSingletonForUpdate<CameraAnchor>();
        physicsWorld = World.GetExistingSystem<BuildPhysicsWorld>();
        filter = CollisionFilter.Default;
        filter.CollidesWith = ~(1u << 3);
        filter.BelongsTo = ~0u;
    }

    protected override void OnUpdate()
    {
        var ghost = GetSingletonEntity<GhostPlacementBuilder>();
        var gridItem = GetComponent<GridItem>(ghost);
        var gridEntity = gridItem.Grid;
        var gridLtw = GetComponent<LocalToWorld>(gridEntity);
        var bgp = EntityManager.GetComponentData<GhostPlacementBuilder>(ghost);

        var collisionWorld = physicsWorld.PhysicsWorld.CollisionWorld;

        var camera = Camera.main.transform;
        float3 target = camera.position + camera.forward * bgp.Range;
        var input = new RaycastInput
        {
            Start = camera.position,
            End = target,
            Filter = filter
        };
        if (collisionWorld.CastRay(input, out var hit))
            target = hit.Position;

        var targetCell = Grid.CellFromPosition(gridItem.Type, gridLtw, target);

        if (Input.GetKeyDown(KeyCode.Alpha1))
            bgp.Area.Extents.x = 1 - bgp.Area.Extents.x;
        if (Input.GetKeyDown(KeyCode.Alpha2))
            bgp.Area.Extents.y = 1 - bgp.Area.Extents.y;
        if (Input.GetKeyDown(KeyCode.Alpha3))
            bgp.Area.Extents.z = 1 - bgp.Area.Extents.z;

        if (Input.GetMouseButton(0) && math.any(targetCell != bgp.Area.Origin))
        {
            bgp.Area.RotatedExtents = (targetCell - bgp.Area.Origin) * bgp.Area.Extents;
        }
        else
        {
            bgp.Area.Origin  = targetCell;
            bgp.Area.RotatedExtents = int3.zero;
        }
        SetSingleton(bgp);

        gridItem.Area.Origin = bgp.Area.Origin;

        var newPos = new Translation { Value = Grid.PositionFromCell(gridItem.Type, new LocalToWorld(), gridItem.Area.Origin) };
        var canBuild = true;
        foreach (var item in GetBuffer<OccupationBuffer>(gridEntity))
            canBuild &= !(item.Size == gridItem.Type && gridItem.Area.Contains(item.Location) && (item.Value & gridItem.Occupied) > 0);

        SetComponent(ghost, new Ghost{Buildable = canBuild});
        SetComponent(ghost, newPos);
        SetComponent(ghost, gridItem);
    }
}

[UpdateInGroup(typeof(LateSimulationSystemGroup))]
public partial class BuildingWireframeDisplaySystem : SystemBase
{
    private List<MeshRenderer> gridRenderers;
    private GhostPlacementSettings settings;

    protected override void OnCreate()
    {
        RequireSingletonForUpdate<GhostPlacementBuilder>();
        RequireSingletonForUpdate<CameraAnchor>();
        gridRenderers = new List<MeshRenderer>();
        settings = Resources.Load<GhostPlacementSettings>("GhostPlacementSettings");
    }

    protected override void OnStartRunning()
    {
        gridRenderers.Clear();
        foreach(var renderer in BuildingPartManagementSystem.WireframeDisplay.gameObject.GetComponentsInChildren<MeshRenderer>())
            if (renderer.gameObject.layer == LayerMask.NameToLayer("Grid"))
                gridRenderers.Add(renderer);
    }

    protected override void OnStopRunning()
    {
        if(BuildingPartManagementSystem.WireframeDisplay)
            BuildingPartManagementSystem.WireframeDisplay.gameObject.SetActive(false);
    }

    protected override void OnUpdate()
    {
        var e = GetSingletonEntity<GhostPlacementBuilder>();
        var gpb = GetSingleton<GhostPlacementBuilder>();
        var gridItem = GetComponent<GridItem>(e);
        var ghost = GetComponent<Ghost>(e);
        var gridEntity = gridItem.Grid;
        var gridLtw = GetComponent<LocalToWorld>(gridEntity);
        Transform wireframe = BuildingPartManagementSystem.WireframeDisplay.transform;
        wireframe.position = Grid.PositionFromCell(gridItem.Type, gridLtw, gpb.Area.Min);
        wireframe.rotation = gridLtw.Rotation;
        if (Input.GetMouseButton(0))
        {
            wireframe.localScale = (float3)(gpb.Area.Max - gpb.Area.Min + new int3(1)) * Grid.CellSize(gridItem.Type);
            BuildingPartManagementSystem.WireframeDisplay.ShowAxes(false, wireframe.rotation);
            BuildingPartManagementSystem.WireframeDisplay.ShowScaleAxes(gpb.Area.Extents == 1);
        }
        else
        {
            wireframe.localScale = (float3)Grid.CellSize(gridItem.Type);
            BuildingPartManagementSystem.WireframeDisplay.ShowScaleAxes(false);
        }
        foreach(var renderer in gridRenderers)
            renderer.sharedMaterial = ghost.Buildable ? settings.GridBuildMaterial : settings.GridDestroyMaterial;

        BuildingPartManagementSystem.WireframeDisplay.gameObject.SetActive(true);
    }
}
