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

public struct NeighbourBuffer : IBufferElementData
{
    public Entity Value;
}

public struct GridsNeedUpdate : IComponentData { }

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class GridRecalculationSystem : SystemBase
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
        Entities.WithAll<Grid>().ForEach((DynamicBuffer<OccupationBuffer> obuff) =>
        {
            obuff.Clear();
        }).Run();

        Entities.WithAll<Grid>().ForEach((DynamicBuffer<AirtightBuffer> abuff) =>
        {
            abuff.Clear();
        }).Run();

        Entities.WithNone<NeighbourBuffer>().WithAll<GridItem>().ForEach((Entity e) =>
        {
            EntityManager.AddBuffer<NeighbourBuffer>(e);
        }).WithStructuralChanges().WithoutBurst().Run();

        var gridItemEntities = GetEntityQuery(
            ComponentType.ReadOnly<GridItem>(),
            ComponentType.ReadOnly<Parent>(),
            ComponentType.ReadOnly<Translation>(),
            ComponentType.ReadWrite<NeighbourBuffer>());

        var entities = gridItemEntities.ToEntityArray(Allocator.Temp);
        var parents = gridItemEntities.ToComponentDataArray<Parent>(Allocator.Temp);
        var gridItems = gridItemEntities.ToComponentDataArray<GridItem>(Allocator.Temp);
        var translations = gridItemEntities.ToComponentDataArray<Translation>(Allocator.Temp);
        for (int e1 = 0; e1 < entities.Length; e1++)
        {
            var nbbuffer = GetBuffer<NeighbourBuffer>(entities[e1], false);
            nbbuffer.Clear();
            for (int e2 = 0; e2 < entities.Length; e2++)
            {
                if (e1 == e2) continue;
                if (parents[e1].Value != parents[e2].Value) continue;
                if (gridItems[e1].Type != gridItems[e2].Type) continue;
                if (math.distancesq(translations[e1].Value, translations[e2].Value) >
                    Grid.CellSize(gridItems[e1].Type) * Grid.CellSize(gridItems[e1].Type)) continue;
                GetBuffer<NeighbourBuffer>(entities[e1], false).Add(new NeighbourBuffer {Value = entities[e2]});
            }
        }

        var grids = GetComponentDataFromEntity<Grid>(false);
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

        var occupationBuffers = GetBufferFromEntity<OccupationBuffer>();
        var airtightBuffers = GetBufferFromEntity<AirtightBuffer>();
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
