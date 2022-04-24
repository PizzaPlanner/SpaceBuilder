using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Networking;

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
    public Grid.Size Size;
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

public struct GridsNeedUpdate : IComponentData { }

[UpdateInGroup(typeof(GridRecalculationSystemGroup), OrderFirst = true)]
public partial class GridRecalculationSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate(GetEntityQuery(typeof(GridsNeedUpdate)));
    }

    protected override void OnUpdate()
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
            ComponentType.Exclude<Ghost>(),
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
                if (e1 == e2) continue;
                if (gridItems[e1].Grid != gridItems[e2].Grid) continue;
                if (gridItems[e1].Type != gridItems[e2].Type) continue;
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
                            b[i] = new OccupationBuffer { Location = pos, Value = b[i].Value | item.Occupied, Size = item.Type};
                            found = true;
                        }
                    }
                    if (!found)
                        b.Add(new OccupationBuffer { Location = pos, Value = item.Occupied, Size = item.Type });
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

        EntityManager.DestroyEntity(GetEntityQuery(typeof(GridsNeedUpdate)));
    }
}
