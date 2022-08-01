using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
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

        if (!inScalingMode)
        {
            var target = GetComponent<Target>(GetSingletonEntity<LocalPlayer>());
            if (target.Value != Entity.Null && HasComponent<GridItem>(target.Value))
                bgp.TargetGrid = GetComponent<GridItem>(target.Value).Grid;
            else
                bgp.TargetGrid = Entity.Null;
            SetSingleton(bgp);
        }

        if (bgp.TargetGrid == Entity.Null)
            return;

        var occupied = GetBuffer<OccupationBuffer>(bgp.TargetGrid, true);
        var cellPath = GetBuffer<CellPathBuffer>(GetSingletonEntity<CellPathBuffer>(), true);
        var cellPathLocal = new NativeList<CellPathBuffer>(cellPath.Length, Allocator.Temp);
        cellPathLocal.CopyFrom(cellPath.AsNativeArray());
        if (cellPathLocal.Length == 0 && inScalingMode)
        {
            var cameraLtw = GetComponent<LocalToWorld>(GetSingletonEntity<CameraAnchor>());
            var gridLtw = GetComponent<LocalToWorld>(bgp.TargetGrid);
            if (GridTools.GetLookedAtPointOnPlanesFromObject(cameraLtw, gridLtw, gridItem.Area.Origin, 5000,
                    out var position))
            {
                cellPathLocal.Add(new CellPathBuffer{Value = position});
            }
        }

        if (cellPathLocal.Length == 0)
        {
            return;
        }

        var lastUnoccupied = 0;
        for (int i = 0; i < cellPathLocal.Length; i++)
        {
            var targetIsOccupied = false;
            foreach (var item in occupied)
                targetIsOccupied |= math.all(item.Location == cellPathLocal[i].Value);
            if (!targetIsOccupied)
            {
                lastUnoccupied = i;
            }
            else break;
        }

        targetCell = cellPathLocal[lastUnoccupied].Value;

        var rotation = GetComponent<Rotation>(ghost).Value;
        if (inScalingMode)
        {
            bgp.Area.Extents = math.select(0, targetCell - bgp.Area.Origin, bgp.Scaling);
        }
        else
        {
            bgp.Area.Origin = targetCell;
            bgp.Area.Extents = 0;
        }

        gridItem.Area = bgp.Area.AdjustedToObjectRotation(rotation);
        if(bgp.Original.Scalable)
            SetComponent(ghost, gridItem.Area.ToScale(rotation));

        var newPos = new Translation {Value = Grid.PositionFromCell(gridItem.Area.Origin)};
        var canBuild = true;
        foreach (var item in occupied)
            canBuild &= !(gridItem.Area.Contains(item.Location) && (item.Value & gridItem.Occupied) > 0);

        SetComponent(ghost, new Ghost {Buildable = canBuild});
        SetComponent(ghost, gridItem);
        SetComponent(ghost, newPos);
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
        Transform wireframe = GhostBuilder.Wireframe.transform;
        wireframe.position = Grid.PositionFromCell(gridLtw, gpb.Area.Min);
        wireframe.rotation = gridLtw.Rotation;
        var baseGrid = GhostBuilder.Wireframe.BaseGrid;
        baseGrid.localScale = (float3)Grid.CELL_SIZE;
        var objGrid = GhostBuilder.Wireframe.ObjectGrid;
        objGrid.localScale = (float3)gpb.Area.Size * Grid.CELL_SIZE;
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
