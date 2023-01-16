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
        RequireForUpdate<GhostPlacementBuilder>();
        RequireForUpdate<CameraAnchor>();
        targetCell = int.MaxValue;
    }

    private int3 targetCell;

    protected override void OnUpdate()
    {
        var ghost = GetSingletonEntity<GhostPlacementBuilder>();
        var gridItem = GetComponent<GridItem>(ghost);
        var bgp = GetComponent<GhostPlacementBuilder>(ghost);

        var inScalingMode = gridItem.Scalable && Input.GetMouseButton(0);

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

        var rotation = GetComponent<LocalTransform>(ghost).Rotation;
        if (bgp.Original.Scalable)
        {
            if (inScalingMode)
            {
                var gridLtw = GetComponent<WorldTransform>(bgp.TargetGrid);
                if(GridTools.ViewIntersectsPlaneFromGridCell(
                       new float3x2(Camera.main.transform.position, Camera.main.transform.forward), 
                       gridLtw,  
                       new BoundingBox{ Origin = bgp.DragStartCell }.Center, 
                       bgp.DragStartNormal, out var hit))
                    bgp.DragEndCell = GridStructure.CellFromPosition(gridLtw, hit) + bgp.DragStartNormal * bgp.ScaleUp;
                else
                    bgp.DragEndCell = bgp.DragStartCell;

                var scroll = (int)math.sign(Input.GetAxis("Mouse ScrollWheel"));
                if (scroll != 0)
                    bgp.ScaleUp = math.clamp(bgp.ScaleUp + scroll, 0, 255);
            }
            else
            {
                bgp.DragStartCell = targetCell;
                bgp.DragEndCell = targetCell;
                bgp.DragStartNormal = target.GridHit.Normal;
                bgp.ScaleUp = 0;
            }
            gridItem.Area = bgp.DragArea.AdjustedToObjectRotation(rotation);
            SetComponent(ghost, gridItem.Area.ToScale(rotation));
        }
        else
        {
            gridItem.Area.Origin = targetCell;
            gridItem.Area.Extents = bgp.Original.Area.Extents.Rotated(rotation);
            bgp.DragStartCell = targetCell;
            bgp.DragEndCell = targetCell;
            bgp.DragStartNormal = target.GridHit.Normal;
        }
        
        var canBuild = gridItem.Area.IsValid;
        foreach (var item in occupied)
            canBuild &= !gridItem.Area.Contains(item.Location);

        SetComponent(ghost, new Ghost {Buildable = canBuild});
        SetComponent(ghost,
            LocalTransform.Identity
                          .WithPosition(GridStructure.PositionFromCell(gridItem.Area.Origin))
                          .WithRotation(rotation));
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
        RequireForUpdate<GhostPlacementBuilder>();
        RequireForUpdate<CameraAnchor>();
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
        var areaToDisplay = gpb.Original.Scalable ? gpb.DragArea : gridItem.Area; 
        Transform wireframe = GhostBuilder.Wireframe.transform;
        wireframe.position = GridStructure.PositionFromCell(gridLtw, areaToDisplay.Min);
        wireframe.rotation = gridLtw.Rotation;
        var baseGrid = GhostBuilder.Wireframe.BaseGrid;
        baseGrid.localScale = (float3)GridStructure.CELL_SIZE;
        var objGrid = GhostBuilder.Wireframe.ObjectGrid;
        objGrid.localScale = (float3)areaToDisplay.Size * GridStructure.CELL_SIZE;
        if (Input.GetMouseButton(0))
        {
            GhostBuilder.Wireframe.ShowAxes(false, wireframe.rotation);
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
