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
            item.Area = ToArea;
            item.Mass = _prefab.Scalable ? _prefab.Mass * Scale.x * Scale.y * Scale.z : _prefab.Mass;
            return item;
        }
    }

    public BoundingBox ToArea => _prefab.Scalable
        ? new BoundingBox
        {
            Origin = Origin,
            Extents = (int3)Scale - 1
        }
        : new BoundingBox
        {
            Origin = Origin,
            Extents = _prefab.Area.Extents.Rotated(Orientation)
        };

    public LocalTransform ToTransform => LocalTransform.FromPositionRotation(
        GridStructure.AreaCenterToLocal(ToArea),
         Orientation);

    public PostRotationScale ToScale =>
        new PostRotationScale { Value = _prefab.Scalable ? Scale : 1 };
    
    public PostRotationScale ToScaleForGhostBox =>
        new PostRotationScale { Value = ToArea.Size };
}