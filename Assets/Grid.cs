using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public struct Grid : IComponentData
{
    public enum Size : byte
    {
        Tiny,
        Small,
        Normal,
        Big
    }

    public Size MaxSize;
    public int3 BoundsMin;
    public int3 BoundsMax;

    public static int3 CellFromPosition(Size size, LocalToWorld ltw, float3 worldPosition) => (int3) math.floor(math.mul(math.inverse(ltw.Rotation), worldPosition - ltw.Position) / CellSize(size));
    public static int3 CellFromPosition(Size size, float3 localPosition) => (int3) math.floor(localPosition / CellSize(size));
    public static float3 PositionFromCell(Size size, LocalToWorld ltw, int3 cell) =>  math.mul(ltw.Rotation, (float3)cell * CellSize(size)) + ltw.Position;
    public static float3 PositionFromCell(Size size, int3 cell) =>  (float3)cell * CellSize(size);

    public static float CellSize(Grid.Size size) => size switch
    {
        Size.Tiny => 0.25f,
        Size.Small => 1f,
        Size.Normal => 2f,
        Size.Big => 4f,
        _ => 0
    };
}

public struct OccupationBuffer : IBufferElementData
{
    public GridItem.Side Value;
    public int3 Location;
}

public struct AirtightBuffer : IBufferElementData
{
    public GridItem.Side Value;
    public int3 Location;
}

public struct GridsNeedUpdate : IComponentData { }

public partial class GridSystem : SystemBase
{
    private EndSimulationEntityCommandBufferSystem bufferSystem;


    protected override void OnCreate()
    {
        bufferSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
        RequireForUpdate(GetEntityQuery(typeof(GridsNeedUpdate)));
    }

    protected override void OnUpdate()
    {
        var buffer = bufferSystem.CreateCommandBuffer();
        Debug.Log("Updating Grids...");
        var occupationBuffers = GetBufferFromEntity<OccupationBuffer>();
        var airtightBuffers = GetBufferFromEntity<AirtightBuffer>();
        var grids = GetComponentDataFromEntity<Grid>(false);
        Entities.WithAll<Grid>().ForEach((DynamicBuffer<OccupationBuffer> obuff) =>
        {
            obuff.Clear();
        }).Run();

        Entities.WithAll<Grid>().ForEach((DynamicBuffer<AirtightBuffer> abuff) =>
        {
            abuff.Clear();
        }).Run();

        Entities
            .WithName("CalculateGridBounds")
            .WithAll<GridItem>().ForEach((in Parent parent, in Translation position) =>
            {
                if (!grids.HasComponent(parent.Value))
                    return;
                var grid = grids[parent.Value];
                grid.BoundsMin = math.min(grid.BoundsMin, Grid.CellFromPosition(grid.MaxSize, position.Value));
                grid.BoundsMax = math.max(grid.BoundsMax, Grid.CellFromPosition(grid.MaxSize, position.Value));
                grids[parent.Value] = grid;
            }).Run();

        Entities
            .WithName("CalculateOccupation")
            .ForEach((in Parent parent, in GridItem item) =>
        {
            if (occupationBuffers.HasComponent(parent.Value))
            {
                var min = item.Area.Min;
                var max = item.Area.Max;
                var b = occupationBuffers[parent.Value];
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
                            b[i] = new OccupationBuffer { Location = pos, Value = b[i].Value | item.Occupied };
                            found = true;
                        }
                    }
                    if (!found)
                        b.Add(new OccupationBuffer { Location = pos, Value = item.Occupied });
                }
            }

            if (airtightBuffers.HasComponent(parent.Value))
            {
                var min = item.Area.Min;
                var max = item.Area.Max;
                var b = airtightBuffers[parent.Value];
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
                            b[i] = new AirtightBuffer() {Location = pos, Value = b[i].Value | item.Airtight};
                            found = true;
                        }
                    }

                    if (!found)
                        b.Add(new AirtightBuffer() {Location = pos, Value = item.Airtight});
                }
            }
        }).Run();
        bufferSystem.AddJobHandleForProducer(Dependency);

        EntityManager.DestroyEntity(GetEntityQuery(typeof(GridsNeedUpdate)));
    }
}
