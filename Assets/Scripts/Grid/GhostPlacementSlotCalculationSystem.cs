using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor.Graphs;
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

        var inScalingMode = gridItem.Scalable && Input.GetMouseButton(0);
        if (inScalingMode)
        {
            // In which directions do we want to scale?
            bgp.Scaling ^= new bool3(
                Input.GetKeyDown(KeyCode.Alpha1),
                Input.GetKeyDown(KeyCode.Alpha2),
                Input.GetKeyDown(KeyCode.Alpha3));
        }

        var target = GetComponent<Target>(GetSingletonEntity<LocalPlayer>());
        if (!inScalingMode)
        {
            bgp.TargetGrid = target.GridHit.Grid;
            SetSingleton(bgp);
        }

        if (bgp.TargetGrid == Entity.Null)
        {
            SetComponent(ghost, new Ghost {Buildable = false});
            return;
        }

        var occupied = GetBuffer<OccupationBuffer>(bgp.TargetGrid, true);
        var newCell = target.GridHit.Cell + target.GridHit.Normal;
        var targetIsOccupied = false;
        foreach (var item in occupied)
            targetIsOccupied |= math.all(item.Location == newCell);
        targetCell = targetIsOccupied ? int.MaxValue : newCell;

        var rotation = GetComponent<Rotation>(ghost).Value;
        if (bgp.Original.Scalable)
        {
            if (inScalingMode)
            {
                var camera = Camera.main.transform;
                var camPosition = (float3)camera.position;
                var camForward = (float3) camera.forward;
                var gridLtw = GetComponent<LocalToWorld>(bgp.TargetGrid);
                var planeOriginWorld = Grid.GridToWorld(gridLtw, new BoundingBox{Origin = bgp.DragStartCell}.Center);
                var normalWorld = math.mul(gridLtw.Rotation, bgp.DragStartNormal);
                var dragEndCell = bgp.DragStartCell; 
                if (GridTools.RayIntersectsPlane(camForward, camPosition, planeOriginWorld, normalWorld, 100, out var planeHit))
                {
                    dragEndCell = Grid.CellFromPosition(gridLtw, planeHit);
                }
                bgp.ScaleArea.Extents = math.select(0, dragEndCell - bgp.DragStartCell, true);
            }
            else
            {
                bgp.ScaleArea.Origin = targetCell;
                bgp.DragStartCell = targetCell;
                bgp.DragStartNormal = target.GridHit.Normal;
                bgp.ScaleArea.Extents = 0;
            }
            gridItem.Area = bgp.ScaleArea.AdjustedToObjectRotation(rotation);
            SetComponent(ghost, gridItem.Area.ToScale(rotation));
        }
        else
        {
            gridItem.Area.Origin = targetCell;
            gridItem.Area.Extents = bgp.Original.Area.Extents.Rotated(rotation);
            bgp.ScaleArea.Origin = targetCell;
            bgp.ScaleArea.Extents = 0;
            bgp.DragStartCell = targetCell;
            bgp.DragStartNormal = target.GridHit.Normal;
        }
        
        var canBuild = gridItem.Area.IsValid;
        foreach (var item in occupied)
            canBuild &= !gridItem.Area.Contains(item.Location);

        SetComponent(ghost, new Ghost {Buildable = canBuild});
        SetComponent(ghost, new Translation {Value = Grid.PositionFromCell(gridItem.Area.Origin)});
        SetComponent(ghost, gridItem);
        SetSingleton(bgp);
        if (!HasComponent<NeedsUpdate>(ghost))
            EntityManager.AddComponent<NeedsUpdate>(ghost);

    }
}

[UpdateInGroup(typeof(PresentationSystemGroup))]
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
        var areaToDisplay = gpb.Original.Scalable ? gpb.ScaleArea : gridItem.Area; 
        Transform wireframe = GhostBuilder.Wireframe.transform;
        wireframe.position = Grid.PositionFromCell(gridLtw, areaToDisplay.Min);
        wireframe.rotation = gridLtw.Rotation;
        var baseGrid = GhostBuilder.Wireframe.BaseGrid;
        baseGrid.localScale = (float3)Grid.CELL_SIZE;
        var objGrid = GhostBuilder.Wireframe.ObjectGrid;
        objGrid.localScale = (float3)areaToDisplay.Size * Grid.CELL_SIZE;
        if (Input.GetMouseButton(0))
        {
            GhostBuilder.Wireframe.ShowAxes(false, wireframe.rotation);
            GhostBuilder.Wireframe.ShowScaleAxes(gpb.Scaling);
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
