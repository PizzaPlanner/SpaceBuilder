using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public struct GhostPlacementBuilder : IComponentData
{
    public bool RebuildGhost;

    GridHitInfo _grid;
    public GridHitInfo Target
    {
        get => _grid;
        set
        {
            RebuildGhost |= _grid.Grid != value.Grid;
            _grid = value;
        }
    }

    ulong _prefabId;
    public ulong PrefabIdToBuild => _prefabId;
    GridItem _prefabPart;
    public GridItem PrefabPart => _prefabPart;

    public int3 DragStartCell;
    public int3 DragEndCell;

    RightAngleRotation _currentRotation;
    public quaternion CurrentRotation
    {
        get => _currentRotation;
        set
        {
            var rar = new RightAngleRotation(value);
            RebuildGhost |= _currentRotation != rar;
            _currentRotation = rar;
        }
    }

    public BoundingBox Area =>
        PrefabPart.Scalable ? 
            DragArea : 
            new BoundingBox { Origin = DragStartCell, Extents = PrefabPart.Area.Extents }.AdjustedToObjectRotation(CurrentRotation);
    
    BoundingBox DragArea
    {
        get
        {
            var bb1 = new BoundingBox
            {
                Origin = DragStartCell, 
                Extents = DragEndCell - DragStartCell
            };
            var bb2 = new BoundingBox { Origin = bb1.Min, Extents = bb1.Size - 1 };
            return bb2;
        }
    }

    public void SetPrefab(ulong prefabId, GridItem part)
    {
        _prefabId = prefabId;
        _prefabPart = part;
        RebuildGhost = true;
    }
}