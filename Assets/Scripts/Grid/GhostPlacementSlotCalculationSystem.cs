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
        var anchorLtw = GetComponent<LocalToWorld>(GetSingletonEntity<CameraAnchor>());
        var ghost = GetSingletonEntity<GhostPlacementBuilder>();
        var gridEntity = GetComponent<Parent>(ghost).Value;
        var gridLtw = GetComponent<LocalToWorld>(gridEntity);
        var grid = GetComponent<Grid>(gridEntity);
        var gridItem = GetComponent<GridItem>(ghost);
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
        {
            var occupied = Grid.CellFromPosition(gridItem.Type, gridLtw, hit.Position);
            var me = Grid.CellFromPosition(gridItem.Type, gridLtw, input.Start);
            var diff = me - occupied;
            var diffAbs = math.abs(diff);

            gridItem.Area.Origin = occupied;// + new int3((int)math.sign(diff.x), (int)math.sign(diff.y),(int)math.sign(diff.z));
        }
        else
        {
            gridItem.Area.Origin = Grid.CellFromPosition(gridItem.Type, gridLtw, target);
        }

        var newPos = new Translation { Value = Grid.PositionFromCell(gridItem.Type, new LocalToWorld(), gridItem.Area.Origin) };

        SetComponent(ghost, newPos);
        SetComponent(ghost, gridItem);

        var canBuild = true;
        foreach (var item in GetBuffer<OccupationBuffer>(gridEntity))
            canBuild &= !(item.Size == gridItem.Type && gridItem.Area.Contains(item.Location) && (item.Value & gridItem.Occupied) > 0);
        bgp.Buildable = canBuild;
        SetSingleton(bgp);

        Transform wireframe = BuildingPartManagementSystem.WireframeDisplay.transform;
        wireframe.position = Grid.PositionFromCell(gridItem.Type, gridLtw, gridItem.Area.Origin);
        wireframe.rotation = gridLtw.Rotation;
        wireframe.localScale = new float3(Grid.CellSize(gridItem.Type));
        foreach(var renderer in gridRenderers)
            renderer.sharedMaterial = canBuild ? settings.GridBuildMaterial : settings.GridDestroyMaterial;

        BuildingPartManagementSystem.WireframeDisplay.gameObject.SetActive(true);
    }
}
