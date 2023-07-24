using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public partial struct RecalculateGridAABBSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        foreach(var (grid, ltw) in SystemAPI.Query<RefRW<GridStructure>, LocalToWorld>())
        {
            var realBoundsMin =  (float3)grid.ValueRW.BoundsMin * GridStructure.CELL_SIZE;
            var realBoundsMax =  (float3)(grid.ValueRW.BoundsMax + 1) * GridStructure.CELL_SIZE;
            var extents = (realBoundsMax - realBoundsMin) * 0.5f;
            var aabb = new AABB
            {
                Extents = extents,
                Center = realBoundsMin + extents
            };
            grid.ValueRW.WorldAABB = AABB.Transform(ltw.Value, aabb);
        }
    }
}
