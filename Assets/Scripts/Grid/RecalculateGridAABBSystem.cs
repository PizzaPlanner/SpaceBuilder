using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public partial class RecalculateGridAABBSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities.ForEach((ref Grid grid, in LocalToWorld ltw) =>
        {
            var realBoundsMin =  (float3)grid.BoundsMin * Grid.CELL_SIZE;
            var realBoundsMax =  (float3)(grid.BoundsMax + 1) * Grid.CELL_SIZE;
            var extents = (realBoundsMax - realBoundsMin) * 0.5f;
            var aabb = new AABB
            {
                Extents = extents,
                Center = realBoundsMin + extents
            };
            grid.AABB = AABB.Transform(ltw.Value, aabb);
        }).Run();
    }
}
