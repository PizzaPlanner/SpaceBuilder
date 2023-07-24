using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public struct BoundingBox
{
    public int3 Origin;

    public int3 Extents;

    public readonly int3 Size => Max - Min + 1;

    public readonly PostTransformMatrix ToScale(quaternion objectRotation) => new()
    {
        Value = float4x4.TRS(GridStructure.CELL_SIZE * 0.5f, objectRotation, Size)
    }; // float4x4.Scale(math.abs(Size.Rotated(math.inverse(objectRotation))))};

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

    public readonly bool IsValid => math.all(Max < short.MaxValue) &&
                                    math.all(Min > short.MinValue) && 
                                    math.length(Size) < 20*20*20;
    
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
        Debug.DrawLine(GridStructure.CellToWorld(gridLtw, p1), GridStructure.CellToWorld(gridLtw, p2), math.any(focus) ? cn : cd);
    }
}