using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Networking;
using MeshCollider = Unity.Physics.MeshCollider;

public struct Grid : IComponentData
{
    public const float CELL_SIZE = 0.25f;
    public int3 BoundsMin;
    public int3 BoundsMax;

    public static int3 CellFromPosition(LocalToWorld ltw, float3 worldPosition) => (int3) math.floor(math.mul(math.inverse(ltw.Rotation), worldPosition - ltw.Position) / CELL_SIZE);
    public static int3 CellFromPosition(float3 localPosition) => (int3) math.floor(localPosition / CELL_SIZE);
    public static float3 PositionFromCell(LocalToWorld ltw, int3 cell) =>  math.mul(ltw.Rotation, (float3)cell * CELL_SIZE) + ltw.Position;
    public static float3 PositionFromCell(int3 cell) =>  (float3)cell * CELL_SIZE;
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

public struct ConnectionBuffer : IBufferElementData
{
    public Entity Value;
    public ConnectionType Type;
    public GridItem.Side Side;

    public static bool Connects(ConnectionBuffer connection, GridItem.Side mySide, GridItem.Side theirSide)
    {
        int positive = (int)(GridItem.Side.Front | GridItem.Side.Up | GridItem.Side.Right);
        int negative = (int)(GridItem.Side.Back | GridItem.Side.Down | GridItem.Side.Left);
        int shared = (int)connection.Side & (int)mySide & (positive | negative);
        int theirs = (int)theirSide & (positive | negative);
        return (
            ((shared & positive) << 1) & theirs & negative) +
            (shared & negative & ((theirs & positive) << 1)) != 0;
    }
}

[System.Flags]
public enum ConnectionType
{
    Related = 1<<0,
    Logic = 1<<1,
    Power = 1<<2,
    Gas = 1<<3
}

public struct ConnectionBufferSided : IBufferElementData
{
    public Entity Value;
    public GridItem.Side Side;
}

public struct GridNeedsUpdate : IComponentData { }

[UpdateInGroup(typeof(GridRecalculationSystemGroup), OrderFirst = true)]
public partial class GridRecalculationSystem : SystemBase
{
    private EndInitializationEntityCommandBufferSystem bufferSystem;
    private Mesh Cube;
    protected override void OnCreate()
    {
        bufferSystem = World.GetExistingSystem<EndInitializationEntityCommandBufferSystem>();
        RequireForUpdate(GetEntityQuery(typeof(GridNeedsUpdate)));

        Cube = new Mesh();

        Vector3[] vertices = {
            new Vector3 (0, 0, 0) * Grid.CELL_SIZE,
            new Vector3 (1, 0, 0) * Grid.CELL_SIZE,
            new Vector3 (1, 1, 0) * Grid.CELL_SIZE,
            new Vector3 (0, 1, 0) * Grid.CELL_SIZE,
            new Vector3 (0, 1, 1) * Grid.CELL_SIZE,
            new Vector3 (1, 1, 1) * Grid.CELL_SIZE,
            new Vector3 (1, 0, 1) * Grid.CELL_SIZE,
            new Vector3 (0, 0, 1) * Grid.CELL_SIZE,
        };

        int[] triangles = {
            0, 2, 1, //face front
            0, 3, 2,
            2, 3, 4, //face top
            2, 4, 5,
            1, 2, 5, //face right
            1, 5, 6,
            0, 7, 4, //face left
            0, 4, 3,
            5, 4, 7, //face back
            5, 7, 6,
            0, 6, 7, //face bottom
            0, 1, 6
        };
        Cube.vertices = vertices;
        Cube.triangles = triangles;
    }

    protected unsafe override void OnUpdate()
    {
        Debug.Log("Recalculating Grids!");
        Entities.WithAll<Grid>().ForEach((DynamicBuffer<OccupationBuffer> obuff) =>
        {
            obuff.Clear();
        }).Run();

        Entities.WithAll<Grid>().ForEach((DynamicBuffer<AirtightBuffer> abuff) =>
        {
            abuff.Clear();
        }).Run();

        var gridItemEntities = GetEntityQuery(
            ComponentType.Exclude<Blueprinted>(),
            ComponentType.ReadOnly<GridItem>(),
            ComponentType.ReadWrite<ConnectionBuffer>());

        var entities = gridItemEntities.ToEntityArray(Allocator.Temp);
        var gridItems = gridItemEntities.ToComponentDataArray<GridItem>(Allocator.Temp);
        for (int e1 = 0; e1 < entities.Length; e1++)
        {
            var nbbuffer = GetBuffer<ConnectionBuffer>(entities[e1], false);
            nbbuffer.Clear();
            for (int e2 = 0; e2 < entities.Length; e2++)
            {
                if (gridItems[e1].Grid != gridItems[e2].Grid) continue;
                if (e1 == e2) continue;
                var distance = gridItems[e2].Area.Origin - gridItems[e1].Area.Origin;
                if (math.dot(distance, distance) != 1) continue;
                GridItem.Side side = 0;
                if (distance.x == 1) side |= GridItem.Side.Right;
                else if (distance.x == -1) side |= GridItem.Side.Left;
                else if (distance.y == 1) side |= GridItem.Side.Up;
                else if (distance.y == -1) side |= GridItem.Side.Down;
                else if (distance.z == 1) side |= GridItem.Side.Front;
                else if (distance.z == -1) side |= GridItem.Side.Back;
                if(side != 0)
                    nbbuffer.Add(new ConnectionBuffer {Value = entities[e2], Side = side});
            }
        }

        var grids = GetComponentDataFromEntity<Grid>(false);
        Entities
            .WithName("CalculateGridBounds")
            .ForEach((in GridItem item) =>
            {
                if (!grids.HasComponent(item.Grid))
                    return;
                var grid = grids[item.Grid];
                grid.BoundsMin = math.min(grid.BoundsMin, item.Area.Origin);
                grid.BoundsMax = math.max(grid.BoundsMax, item.Area.Origin);
                grids[item.Grid] = grid;
            }).Run();

        var occupationBuffers = GetBufferFromEntity<OccupationBuffer>();
        var airtightBuffers = GetBufferFromEntity<AirtightBuffer>();
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

            if (airtightBuffers.HasComponent(item.Grid))
            {
                var min = item.Area.Min;
                var max = item.Area.Max;
                var b = airtightBuffers[item.Grid];
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

        var buffer = bufferSystem.CreateCommandBuffer();
        buffer.DestroyEntitiesForEntityQuery(GetEntityQuery(typeof(GridNeedsUpdate)));
        bufferSystem.AddJobHandleForProducer(Dependency);
    }
}
