using System;
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
        Right = 1<<2,
        Left = 1<<3,
        Front = 1<<4,
        Back = 1<<5,
        Center = 1<<6
    }

    public Side Occupied;
    public Side Airtight;
    public BoundingBox Area;
    public Grid.Size Type;
    public Entity Grid;
}

public struct BoundingBox
{
    public int3 Origin;
    public int3 Extents;

    public int3 RotatedExtents;
    public readonly int3 Min => math.min(Origin, Origin + RotatedExtents);
    public readonly int3 Max => math.max(Origin, Origin + RotatedExtents);

    public readonly bool Contains(int3 value)
    {
        return value.x >= Min.x && value.x <= Max.x &&
               value.y >= Min.y && value.y <= Max.y &&
               value.z >= Min.z && value.z <= Max.z;
    }
}

[RequireComponent(typeof(IdentifieableAuthoring))]
public class BuildingPart : MonoBehaviour, IConvertGameObjectToEntity
{
    [SerializeField] private Grid.Size Type;
    [SerializeField] private int3 SizeInGrid;
    [SerializeField] private GridItem.Side Occupied;
    [SerializeField] private GridItem.Side Airtight;

    private void OnDrawGizmos()
    {
        Gizmos.DrawWireCube((float3)SizeInGrid * 0.5f * Grid.CellSize(Type), (float3)SizeInGrid * Grid.CellSize(Type));
    }

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponentData(entity, new GridItem
        {
            Area = new BoundingBox{Origin = int3.zero, Extents = SizeInGrid - new int3(1)},
            Occupied = Occupied,
            Airtight = Airtight,
            Type = Type
        });
        dstManager.AddComponentData(entity, new RotationPivot {Value = Grid.CellSize(Type) * 0.5f});
        dstManager.AddComponent<CompositeRotation>(entity);
        dstManager.AddBuffer<ConnectionBuffer>(entity);
    }
}
