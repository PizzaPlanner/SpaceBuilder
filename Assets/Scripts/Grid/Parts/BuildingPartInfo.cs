using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public struct BuildingPartInfo : IBufferElementData
{
    public ulong Id
    {
        get => _id;
        set
        {
            _id = value;
            _prefab = GridItemMappingSystem.GetPrefabFromId(value);
        }
    }
    public int3 Origin;
    public uint3 Scale;
    public RightAngleRotation Orientation;
    public Color Color;
    public bool IsGhost;

    ulong _id;
    GridItem _prefab;

    public GridItem ToGridItem
    {
        get
        {
            var item = _prefab;
            item.Area = new BoundingBox
            {
                Origin = Origin,
                Extents = _prefab.Scalable ? (int3)Scale - 1 : _prefab.Area.Extents.Rotated(Orientation)
            };
            return item;
        }
    }

    public LocalTransform ToTransform => LocalTransform.FromPositionRotation(
         math.select( 
             GridStructure.CellCenterToLocal(ToGridItem.Area.Pivot),
             GridStructure.CellToLocal(ToGridItem.Area.Pivot), 
             ToGridItem.Area.Size % 2 == 0),
         Orientation);

    public PostRotationScale ToScale =>
        new PostRotationScale { Value = _prefab.Scalable ? Scale : 1 };
    
    public PostRotationScale ToScaleForGhostBox =>
        new PostRotationScale { Value = _prefab.Scalable ? Scale : _prefab.Area.Extents };
}