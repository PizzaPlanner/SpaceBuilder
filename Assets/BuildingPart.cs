using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public struct GridItem : IComponentData
{
    [Flags]
    public enum Side : byte
    {
        Up = 1<<0,
        Down = 1<<1,
        Left = 1<<2,
        Right = 1<<3,
        Front = 1<<4,
        Back = 1<<5,
        Center = 1<<6
    }

    public long ItemId;
    public Side Occupied;
    public Side Airtight;
    public BoundingBox Area;
    public Grid.Size Type;
}

public struct BoundingBox
{
    public int3 Origin;
    public int3 Extents;

    public int3 RotatedExtents;
    public int3 Min => math.min(Origin, Origin + RotatedExtents);
    public int3 Max => math.max(Origin, Origin + RotatedExtents);

    public readonly bool Contains(int3 value)
    {
        return value.x >= Min.x && value.x <= Max.x &&
               value.y >= Min.y && value.y <= Max.y &&
               value.z >= Min.z && value.z <= Max.z;
    }
}

public class BuildingPart : MonoBehaviour, IConvertGameObjectToEntity
{

    [SerializeField] [ReadOnly] private long ItemID;
    [SerializeField] private Grid.Size Type;
    [SerializeField] private int3 SizeInGrid;
    [SerializeField] private GridItem.Side Occupied;
    [SerializeField] private GridItem.Side Airtight;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this))
            return;

        var guid = UnityEditor.AssetDatabase.AssetPathToGUID(UnityEditor.AssetDatabase.GetAssetPath(gameObject));
        // ItemID = long.Parse(guid[..16],System.Globalization.NumberStyles.HexNumber);
    }

    private void OnDrawGizmos()
    {
        Gizmos.DrawWireCube((float3)SizeInGrid * 0.5f * Grid.CellSize(Type), (float3)SizeInGrid * Grid.CellSize(Type));
    }
#endif

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponentData(entity, new GridItem
        {
            Area = new BoundingBox{Origin = int3.zero, Extents = SizeInGrid - new int3(1)},
            ItemId = ItemID,
            Occupied = Occupied,
            Airtight = Airtight,
            Type = Type
        });
        dstManager.AddComponentData(entity, new RotationPivot {Value = Grid.CellSize(Type) * 0.5f});
        dstManager.AddComponent<CompositeRotation>(entity);
    }
}
