using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public partial class RecalculateGridAABBSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities.ForEach((ref GridStructure grid, in LocalToWorld ltw) =>
        {
            var realBoundsMin =  (float3)grid.BoundsMin * GridStructure.CELL_SIZE;
            var realBoundsMax =  (float3)(grid.BoundsMax + 1) * GridStructure.CELL_SIZE;
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
