using System.Collections.Generic;
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

        // var occupied = GetComponent<CellMap>(bgp.TargetGrid);
        var newCell = target.GridHit.Cell + target.GridHit.Normal;
        // var targetIsOccupied = occupied.IsOccupied(newCell);
        // targetCell = targetIsOccupied ? int.MaxValue : newCell;

        var rotation = GetComponent<LocalTransform>(ghost).Rotation;
        if (bgp.Original.Scalable)
        {
            if (inScalingMode)
            {
                var gridLtw = GetComponent<LocalToWorld>(bgp.TargetGrid);
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
        
        var canBuild = gridItem.Area.IsValid; // && !occupied.IsAnyOccupied(gridItem.Area);

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
