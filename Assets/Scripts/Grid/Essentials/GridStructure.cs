using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct GridStructure : IComponentData
{
    public const float CELL_SIZE = 0.25f;
    public int3 BoundsMin, BoundsMax;
    public float Mass;
    
    // Bounds in World Space
    public AABB WorldAABB;
    public IntAABB LocalAABB => new IntAABB { Min = BoundsMin, Max = BoundsMax };

    public static int3 CellFromPosition(float3 localPosition) => (int3) math.floor(localPosition / CELL_SIZE);
    public static int3 CellFromPosition(LocalToWorld ltw, float3 worldPosition) => (int3) math.floor(math.mul(math.inverse(ltw.Rotation), worldPosition - ltw.Position) / CELL_SIZE);
    public static float3 GridToWorld(LocalToWorld gridLtw, float3 localPosition) =>  math.mul(gridLtw.Rotation, localPosition * CELL_SIZE) + gridLtw.Position;
    public static float3 WorldToGrid(LocalToWorld gridLtw, float3 worldPosition) => math.mul(math.inverse(gridLtw.Rotation), worldPosition - gridLtw.Position) / CELL_SIZE;
    public static float3 CellToLocal(int3 cell) =>  (float3)cell * CELL_SIZE;
    public static float3 CellToWorld(LocalToWorld ltw, int3 cell) =>  math.mul(ltw.Rotation, CellToLocal(cell)) + ltw.Position;
    public static float3 CellCenterToLocal(int3 cell) =>  (float3)cell * CELL_SIZE + CELL_SIZE * 0.5f;
    public static float3 CellCenterToWorld(LocalToWorld ltw, int3 cell) =>  math.mul(ltw.Rotation, CellCenterToLocal(cell)) + ltw.Position;
    public static float3 AreaCenterToLocal(BoundingBox area) => area.Center * CELL_SIZE;
    public static float3 AreaCenterToLocal(LocalToWorld ltw, BoundingBox area) => math.mul(ltw.Rotation, AreaCenterToLocal(area)) + ltw.Position;
}