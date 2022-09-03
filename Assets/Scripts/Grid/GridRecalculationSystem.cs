using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public struct Grid : IComponentData
{
    public const float CELL_SIZE = 0.25f;
    public int3 BoundsMin;
    public int3 BoundsMax;

    public AABB AABB;

    public static int3 CellFromPosition(LocalToWorld ltw, float3 worldPosition) => (int3) math.floor(math.mul(math.inverse(ltw.Rotation), worldPosition - ltw.Position) / CELL_SIZE);
    public static int3 CellFromPosition(float3 localPosition) => (int3) math.floor(localPosition / CELL_SIZE);
    public static float3 PositionFromCell(LocalToWorld ltw, int3 cell) =>  math.mul(ltw.Rotation, (float3)cell * CELL_SIZE) + ltw.Position;
    public static float3 GridToWorld(LocalToWorld gridLtw, float3 localPosition) =>  math.mul(gridLtw.Rotation, localPosition * CELL_SIZE) + gridLtw.Position;
    public static float3 WorldToGrid(LocalToWorld gridLtw, float3 worldPosition) => math.mul(math.inverse(gridLtw.Rotation), worldPosition - gridLtw.Position) / CELL_SIZE;
    public static float3 PositionFromCell(int3 cell) =>  (float3)cell * CELL_SIZE;
}

public struct OccupationBuffer : IBufferElementData
{
    public GridItem.Side Value;
    public int3 Location;
}

public struct NeedsUpdate : IComponentData { }
public struct GridNeedsUpdate : IComponentData { }

[UpdateInGroup(typeof(GridRecalculationSystemGroup), OrderFirst = true)]
public partial class GridRecalculationSystem : SystemBase
{
    private EndInitializationEntityCommandBufferSystem bufferSystem;
    protected override void OnCreate()
    {
        bufferSystem = World.GetExistingSystem<EndInitializationEntityCommandBufferSystem>();
        RequireForUpdate(GetEntityQuery(typeof(GridNeedsUpdate)));
    }

    protected override unsafe void OnUpdate()
    {
        Debug.Log("Recalculating Grids!");
        Entities.WithAll<Grid>().ForEach((DynamicBuffer<OccupationBuffer> obuff) =>
        {
            obuff.Clear();
        }).Run();

        Entities.ForEach((ref Grid grid) =>
        {
            grid.BoundsMax = int.MinValue;
            grid.BoundsMin = int.MaxValue;
        }).Run();

        var occupationBuffers = GetBufferFromEntity<OccupationBuffer>();
        var grids = GetComponentDataFromEntity<Grid>();
        Entities
            .WithName("CalculateOccupation")
            .ForEach((in GridItem item) =>
        {
            if (occupationBuffers.HasComponent(item.Grid))
            {
                var min = item.Area.Min;
                var max = item.Area.Max;
                var b = occupationBuffers[item.Grid];
                for (int x = min.x; x <= max.x; x++)
                for (int y = min.y; y <= max.y; y++)
                for (int z = min.z; z <= max.z; z++)
                {
                    var pos = new int3(x, y, z);
                    bool found = false;
                    for (int i = 0; i < b.Length; i++)
                    {
                        if (math.all(b[i].Location == pos))
                        {
                            b[i] = new OccupationBuffer { Location = pos, Value = b[i].Value | item.Occupied};
                            found = true;
                        }
                    }
                    if (!found)
                        b.Add(new OccupationBuffer { Location = pos, Value = item.Occupied});

                }
            }

            if (grids.HasComponent(item.Grid))
            {
                var gTemp = grids[item.Grid];
                gTemp.BoundsMin = math.min(gTemp.BoundsMin, item.Area.Min);
                gTemp.BoundsMax = math.max(gTemp.BoundsMax, item.Area.Max);
                grids[item.Grid] = gTemp;
            }
        }).Run();

        var buffer = bufferSystem.CreateCommandBuffer();
        buffer.DestroyEntitiesForEntityQuery(GetEntityQuery(typeof(GridNeedsUpdate)));
        bufferSystem.AddJobHandleForProducer(Dependency);
    }
}
