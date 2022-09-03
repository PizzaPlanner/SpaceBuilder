using System;
using System.Collections;
using System.Collections.Generic;
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
    public bool Scalable;
    public Entity Grid;
}

public struct BoundingBox
{
    public int3 Origin;

    public int3 Extents;

    public readonly int3 Size => Max - Min + 1;
    public readonly NonUniformScale ToScale(quaternion objectRotation) => new() {Value = math.abs(Size.Rotated(math.inverse(objectRotation)))};

    public readonly BoundingBox AdjustedToObjectRotation(quaternion objectRotation)
    {
        var unitRotation = ((int3)1).Rotated(objectRotation);
        var bbox = this;
        bbox.Origin = math.select(Min, Max, unitRotation != 1);
        bbox.Extents *=
            math.select(1, -1, bbox.Min != Min | bbox.Max != Max);
        return bbox;
    }
    public readonly int3 Min => math.min(Origin, Origin + Extents);
    public readonly int3 Max => math.max(Origin, Origin + Extents);
    public readonly float3 Center => math.lerp(Min, Max + 1, 0.5f);

    public readonly bool Contains(int3 value)
    {
        return value.x >= Min.x && value.x <= Max.x &&
               value.y >= Min.y && value.y <= Max.y &&
               value.z >= Min.z && value.z <= Max.z;
    }

    public static void Draw(BoundingBox box, LocalToWorld gridLtw, int3 normal, Color colorDefault, Color colorNormal)
    {
        var p1 = box.Min;
        var p2 = box.Max + 1;
            
        var p3 = new int3(p1.x, p1.y, p2.z);
        var p4 = new int3(p1.x, p2.y, p1.z);
        var p5 = new int3(p2.x, p1.y, p1.z);

        var p6 = new int3(p2.x, p2.y, p1.z);
        var p7 = new int3(p2.x, p1.y, p2.z);
        var p8 = new int3(p1.x, p2.y, p2.z);
            
        Draw(p1, p3, normal, p1, p2, gridLtw, colorDefault, colorNormal);
        Draw(p1, p4, normal, p1, p2, gridLtw, colorDefault, colorNormal);
        Draw(p1, p5, normal, p1, p2, gridLtw, colorDefault, colorNormal);
        Draw(p2, p6, normal, p1, p2, gridLtw, colorDefault, colorNormal);
        Draw(p2, p7, normal, p1, p2, gridLtw, colorDefault, colorNormal);
        Draw(p2, p8, normal, p1, p2, gridLtw, colorDefault, colorNormal);
        Draw(p8, p3, normal, p1, p2, gridLtw, colorDefault, colorNormal);
        Draw(p8, p4, normal, p1, p2, gridLtw, colorDefault, colorNormal);
        Draw(p4, p6, normal, p1, p2, gridLtw, colorDefault, colorNormal);
        Draw(p5, p6, normal, p1, p2, gridLtw, colorDefault, colorNormal);
        Draw(p5, p7, normal, p1, p2, gridLtw, colorDefault, colorNormal);
        Draw(p7, p3, normal, p1, p2, gridLtw, colorDefault, colorNormal);
    }

    static void Draw(int3 p1, int3 p2, int3 normal, int3 min, int3 max, LocalToWorld gridLtw, Color cd, Color cn)
    {
        var focus = ((normal == 1) & (p1 == max) & (p2 == max)) | ((normal == -1) & (p1 == min) & (p2 == min));
        Debug.DrawLine(Grid.PositionFromCell(gridLtw, p1), Grid.PositionFromCell(gridLtw, p2), math.any(focus) ? cn : cd);
    }
}

[RequireComponent(typeof(IdentifieableAuthoring))]
public class BuildingPart : MonoBehaviour, IConvertGameObjectToEntity
{
    [SerializeField] private uint3 SizeInGrid;
    [SerializeField] private GridItem.Side Occupied;
    [SerializeField] private GridItem.Side Airtight;
    [SerializeField] private bool Scalable;
    private void OnDrawGizmos()
    {
        Gizmos.DrawWireCube((float3)SizeInGrid * 0.5f * Grid.CELL_SIZE, (float3)SizeInGrid * Grid.CELL_SIZE);
    }

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponentData(entity, new GridItem
        {
            Area = new BoundingBox{Origin = int3.zero, Extents = (int3)SizeInGrid - 1},
            Occupied = Occupied,
            Airtight = Airtight,
            Scalable = math.all(SizeInGrid == 1) && Scalable
        });
        dstManager.AddComponentData(entity, new RotationPivot {Value = Grid.CELL_SIZE * 0.5f});
        dstManager.AddComponent<CompositeRotation>(entity);
        dstManager.AddComponent<LocalToParent>(entity);
        dstManager.AddComponent<Parent>(entity);
        dstManager.AddComponent<Rotation>(entity);
        dstManager.AddComponent<Translation>(entity);
    }

    private void OnValidate()
    {
        if (!math.all(SizeInGrid == 1))
            Scalable = false;
    }
}
