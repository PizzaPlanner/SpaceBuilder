using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditorInternal;
using UnityEngine;

[UpdateBefore(typeof(TransformSystemGroup))]
[UpdateAfter(typeof(PlayerTargetSystem))]
public partial class CalculateBuildingSlotSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireSingletonForUpdate<GhostPlacementBuilder>();
        RequireSingletonForUpdate<CameraAnchor>();
        targetCell = int.MaxValue;
    }

    private int3 targetCell;

    protected override void OnUpdate()
    {
        var ghost = GetSingletonEntity<GhostPlacementBuilder>();
        var gridItem = GetComponent<GridItem>(ghost);
        var bgp = GetComponent<GhostPlacementBuilder>(ghost);

        if (Input.GetMouseButton(0))
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
                bgp.Area.Extents.x = bgp.Area.Extents.x > 0 ? 0 : (math.abs(gridItem.Area.RotatedExtents.x) + 1);
            if (Input.GetKeyDown(KeyCode.Alpha2))
                bgp.Area.Extents.y = bgp.Area.Extents.y > 0 ? 0 : (math.abs(gridItem.Area.RotatedExtents.y) + 1);
            if (Input.GetKeyDown(KeyCode.Alpha3))
                bgp.Area.Extents.z = bgp.Area.Extents.z > 0 ? 0 : (math.abs(gridItem.Area.RotatedExtents.z) + 1);
        }
        else
        {
            bgp.Area.Extents = int3.zero;
            bgp.TargetGrid = Entity.Null;
        }

        var target = GetComponent<Target>(GetSingletonEntity<LocalPlayer>());
        if (target.Value != Entity.Null && HasComponent<GridItem>(target.Value))
            bgp.TargetGrid = GetComponent<GridItem>(target.Value).Grid;

        if(bgp.TargetGrid != Entity.Null)
        {
            var occupied = GetBuffer<OccupationBuffer>(bgp.TargetGrid, true);
            var cellPath = GetBuffer<CellPathBuffer>(GetSingletonEntity<CellPathBuffer>(), true);
            if (cellPath.Length > 0)
            {
                var lastUnoccupied = 0;
                for (int i = 0; i < cellPath.Length; i++)
                {
                    var targetIsOccupied = false;
                    foreach (var item in occupied)
                        targetIsOccupied |= math.all(item.Location == cellPath[i].Value);
                    if (!targetIsOccupied)
                    {
                        lastUnoccupied = i;
                    }
                    else break;
                }

                targetCell = cellPath[lastUnoccupied].Value;

                if (Input.GetMouseButton(0) && math.any(targetCell != bgp.Area.Origin))
                {
                    bgp.Area.RotatedExtents = (targetCell - bgp.Area.Origin) * bgp.Area.Extents;
                }
                else
                {
                    bgp.Area.Origin = targetCell;
                    bgp.Area.RotatedExtents = int3.zero;
                }

                gridItem.Area.Origin = bgp.Area.Min;
                if (bgp.Original.Scalable)
                {
                    SetComponent(ghost, new NonUniformScale {Value = bgp.Area.Max - bgp.Area.Min + 1});
                    gridItem.Area.RotatedExtents = bgp.Area.Max - bgp.Area.Min;
                }
                var newPos = new Translation { Value = Grid.PositionFromCell(gridItem.Area.Origin) };
                var canBuild = true;
                foreach (var item in occupied)
                    canBuild &= !(gridItem.Area.Contains(item.Location) && (item.Value & gridItem.Occupied) > 0);

                SetComponent(ghost, new Ghost{Buildable = canBuild});
                SetComponent(ghost, gridItem);
                SetComponent(ghost, newPos);
                SetSingleton(bgp);
                if (!HasComponent<NeedsUpdate>(ghost))
                    EntityManager.AddComponent<NeedsUpdate>(ghost);
            }
        }
        else
        {
            EntityManager.DestroyEntity(ghost);
        }

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
        foreach(var renderer in GhostBuilder.Wireframe.gameObject.GetComponentsInChildren<MeshRenderer>())
            if (renderer.gameObject.layer == LayerMask.NameToLayer("Grid"))
                gridRenderers.Add(renderer);
    }

    protected override void OnStopRunning()
    {
        if(GhostBuilder.Wireframe)
            GhostBuilder.Wireframe.gameObject.SetActive(false);
    }

    protected override void OnUpdate()
    {
        var e = GetSingletonEntity<GhostPlacementBuilder>();
        var gpb = GetSingleton<GhostPlacementBuilder>();
        if (gpb.TargetGrid == Entity.Null)
        {
            GhostBuilder.Wireframe.gameObject.SetActive(false);
            return;
        }
        var gridItem = GetComponent<GridItem>(e);
        var ghost = GetComponent<Ghost>(e);
        var gridLtw = GetComponent<LocalToWorld>(gpb.TargetGrid);
        Transform wireframe = GhostBuilder.Wireframe.transform;
        wireframe.position = Grid.PositionFromCell(gridLtw, gridItem.Area.Min);
        wireframe.rotation = gridLtw.Rotation;
        var baseGrid = GhostBuilder.Wireframe.BaseGrid;
        baseGrid.localScale = (float3)Grid.CELL_SIZE;
        var objGrid = GhostBuilder.Wireframe.ObjectGrid;
        objGrid.localScale = (float3)(gridItem.Area.Max - gridItem.Area.Min + 1) * Grid.CELL_SIZE;
        if (Input.GetMouseButton(0))
        {
            GhostBuilder.Wireframe.ShowAxes(false, wireframe.rotation);
            GhostBuilder.Wireframe.ShowScaleAxes(gpb.Area.Extents == 1);
        }
        else
        {
            GhostBuilder.Wireframe.ShowScaleAxes(false);
        }
        // foreach(var renderer in gridRenderers)
        //     renderer.sharedMaterial = ghost.Buildable ? settings.GridBuildMaterial : settings.GridDestroyMaterial;

        GhostBuilder.Wireframe.gameObject.SetActive(ghost.Buildable);
    }
}
