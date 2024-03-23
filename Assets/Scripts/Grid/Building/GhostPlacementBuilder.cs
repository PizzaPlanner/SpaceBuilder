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

    Identifieable _prefabId;
    public Identifieable PrefabIdToBuild => _prefabId;
    GridItem _prefabPart;
    public GridItem PrefabPart => _prefabPart;

    public int3 DragStartCell;
    public int3 DragEndCell;
    public int DragAreaUpOffset;

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
        Origin = _prefabPart.Scalable 
            ? DragArea.Min 
            : DragArea.Origin,
        Scale = DragArea.Size
    };

    BoundingBox DragArea
    {
        get
        {
            var bb1 = _prefabPart.Scalable ? new BoundingBox
            {
                Origin = DragStartCell,
                Extents = DragEndCell + Target.Normal * DragAreaUpOffset - DragStartCell
            }: new BoundingBox
            {
                Origin = DragStartCell - (new int3(1,0,1) * (int3)PrefabPart.Area.Center).Rotated(TotalRotation),
                Extents = 0 // Not used
            };
            return bb1;
        }
    }

    public void SetPrefab(Identifieable prefabId)
    {
        _prefabId = prefabId;
        _prefabPart = GridItemMappingSystem.GetPrefabFromId(prefabId);
        RebuildGhost = true;
    }
}