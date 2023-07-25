using Unity.Entities;
using Unity.Mathematics;

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

    public quaternion CurrentRotation;

    public quaternion PlaneRotation => Target.Grid == Entity.Null
        ? quaternion.identity
        : quaternion.LookRotation(Target.Normal.yzx, Target.Normal);

    public quaternion TotalRotation => math.mul(PlaneRotation, CurrentRotation);

    public BuildingPartInfo ToPartInfo => new BuildingPartInfo
    {
        Id = PrefabIdToBuild,
        Orientation = new RightAngleRotation(TotalRotation),
        IsGhost = true,
        Origin = DragArea.Min,
        Scale = DragArea.Size
    };

    public BoundingBox DragArea
    {
        get
        {
            var bb1 = new BoundingBox
            {
                Origin = DragStartCell,
                Extents = DragEndCell - DragStartCell
            };
            return bb1;
        }
    }

    public void SetPrefab(ulong prefabId)
    {
        _prefabId = prefabId;
        _prefabPart = GridItemMappingSystem.GetPrefabFromId(prefabId);
        RebuildGhost = true;
    }
}